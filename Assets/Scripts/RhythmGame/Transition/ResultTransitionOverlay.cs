using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 곡 끝 → 결과 화면 전환 연출 A (레코드 착지). 시안: 바탕화면 메인허브_시안\전환연출 (곡끝_결과_A.mp4, end2result.html ?v=A)
// 곡 선택 → 인게임 전환 A(SongTransitionOverlay)를 거꾸로 돌린 짝이다. 씬이 바뀌어도 살아 있다(DontDestroyOnLoad).
//
// 타임라인 (FinishSong이 불린 순간 0초, Time.unscaledDeltaTime 기준)
//   0.35 ~ 1.7   FULL COMBO(또는 CLEAR) 배너 + 링 (인게임 화면 위)
//   1.5  ~ 2.1   (C2) 빛의 벽이 게이트 → 판정선으로 돌아오며 하이웨이를 지움, 인게임 화면 어두워지며 사라짐
//   2.05 ~ 2.45  (C2) 판정선 한 줄이 위아래로 벌어지며 홀로그램 레코드가 맺힘 (민트·줄무늬·RGB 분리 → 원래 색)
//   2.3  ~ 2.8   판정선에 누워 있던 레코드가 일어나며 가운데로 (회전 감속, 회전 잔상)
//   2.75 ~ 3.0   레코드 → 정사각 재킷
//   2.95         결과 씬 활성화 (미리 불러 둔 것). 검은 화면이 0.5초 동안 걷히며 결과 화면이 떠오른다
//   3.0  ~ 3.55  재킷이 결과 화면 재킷 자리(96,200 / 380px)로 내려앉음
//   3.55 ~ 4.65  착지 효과 (외곽선 퍼짐은 여기서, 나머지는 ResultV2Controller가 그린다) → 오버레이 끝
// 결과 씬 로딩이 늦으면 3.0초(정사각 재킷, 가운데)에서 살짝 떠 있는 채로 기다렸다가 이어 간다.
// 좌표는 시안 좌표(1600 x 900)로 계산하고, CanvasScaler(기준 1600 x 900)가 실제 화면에 맞춰 늘린다.
public class ResultTransitionOverlay : MonoBehaviour
{
    public const float R0 = 2.95f;          // 결과 씬을 켜는 시각
    public const float HoldT = 3.0f;        // 로딩이 늦으면 여기서 기다린다
    public const float LandT = 3.55f;       // 착지
    public const float EffectLength = 1.1f; // 착지 효과 길이 (끝나면 오버레이 삭제)
    public const float ScreenCoveredT = 2.1f; // 인게임 화면이 완전히 가려지는 시각
    private const float ResultFadeIn = 0.5f;

    // 요청서 글: "판정선에 누워 있던 레코드가 일어나며 가운데로" (중심 JY → 430, 크기 JW x 0.98 → 400).
    // 시안 HTML(end2result.html)은 같은 줄 아래에서 위치를 다시 덮어써서 처음부터 가운데(430, 400px)에 나온다.
    // false로 바꾸면 시안 영상과 똑같이 가운데에서 일어난다.
    public static bool RiseFromJudgeLine = true;

    public static ResultTransitionOverlay Active { get; private set; }

    public class StartInfo
    {
        public ChartAutoLoader.SongEntry song;
        public PlayResult result;
        public string resultSceneName = "RhythmResultScene_v2";
        public RhythmPlayFieldV2 playField;   // 하이웨이 위치 (없으면 시안 카메라 값으로 계산)
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Active = null;
    }

    public static bool TryStart(StartInfo startInfo)
    {
        if (Active != null || startInfo == null || startInfo.result == null) return false;
        if (string.IsNullOrEmpty(startInfo.resultSceneName) || !Application.CanStreamedLevelBeLoaded(startInfo.resultSceneName)) return false;
        var go = new GameObject("Result Transition Overlay (A)");
        DontDestroyOnLoad(go);
        var overlay = go.AddComponent<ResultTransitionOverlay>();
        overlay.Begin(startInfo);
        return true;
    }

    // 결과 재킷과 똑같은 그림·크롭 (ResultV2Controller도 같은 규칙으로 고른다)
    public static Texture2D JacketFor(ChartAutoLoader.SongEntry song)
    {
        if (song != null && song.albumCover != null) return song.albumCover;
        return SongTransitionSettings.Load().noCover;
    }

    // ── 결과 화면(ResultV2Controller)과 주고받는 값 ──
    public bool ScreenCovered { get { return t >= ScreenCoveredT; } }
    public bool ResultActivated { get; private set; }
    // 결과 화면 시간 r 기준으로 재킷이 내려앉는 순간 (정상이면 0.6, 로딩이 늦었으면 조금 짧다)
    public float LandResultTime { get; private set; }

    // 결과 화면이 진짜 재킷을 켠 프레임에 부른다 → 오버레이 재킷을 숨기고 외곽선 퍼짐을 시작
    public void NotifyLanded()
    {
        if (landedAt < 0f) landedAt = Mathf.Max(t, LandT);
    }

    // 결과 화면에서 건너뛰기를 누르면 바로 정리한다
    public void FinishNow()
    {
        if (this != null) Destroy(gameObject);
    }

    // ── 상태 ──
    private StartInfo info;
    private SongTransitionSettings settings;
    private float t;
    private float holdTime;
    private float activatedAt = -1f;
    private float landedAt = -1f;
    private float startRealtime;
    private AsyncOperation loadOp;
    private bool activationRequested;
    private bool fullCombo;
    private Texture jacketTex;
    private Rect jacketUV = new Rect(0, 0, 1, 1);
    private float lastGlitchFrame = -1f;

    // 하이웨이
    private RhythmPlayFieldV2 playField;
    private Camera gameCamera;
    private float judgeZ = 1.15f, gateZ = 19.85f;
    private float judgeY = 650f, judgeW = 760f;

    // ── 화면 조각 ──
    private Canvas canvas;
    private RectTransform stage;
    private Image black;
    private RectTransform bannerRoot;
    private TextMeshProUGUI bannerMain, bannerRed, bannerCyan, stripText;
    private CanvasGroup bannerAlpha;
    private readonly List<Image> slices = new List<Image>();
    private RawImage jkShadow;
    private PerspectiveQuadGraphic jk, grooves, label, sheen;
    private Material jkMat, groovesMat, labelMat, sheenMat;
    private ScreenShapeGraphic shapes, shapesGlow;
    private readonly List<Vector2> pts = new List<Vector2>();

    private static readonly Color Mint = IngameV2Art.Hex(0x7FE8CF);
    private static readonly Color MintHi = IngameV2Art.Hex(0xC6FBEC);
    private static readonly Color Pale = IngameV2Art.Hex(0xC9FFF0);

    // ── 시작 ──
    private void Begin(StartInfo startInfo)
    {
        Active = this;
        info = startInfo;
        settings = SongTransitionSettings.Load();
        startRealtime = Time.realtimeSinceStartup;
        LandResultTime = LandT - R0;

        var r = info.result;
        int total = r.perfectCount + r.greatCount + r.goodCount + r.missCount;
        fullCombo = r.isFullCombo && total > 0;

        jacketTex = JacketFor(info.song);
        jacketUV = jacketTex != null ? SongSelectRow.SquareCrop(jacketTex) : new Rect(0, 0, 1, 1);

        playField = info.playField;
        gameCamera = Camera.main;
        if (playField != null)
        {
            judgeZ = playField.JudgeZ;
            gateZ = playField.FarGateZ;
        }
        Vector2 l = Floor(-HalfWidth(judgeZ), judgeZ), rr = Floor(HalfWidth(judgeZ), judgeZ);
        judgeY = (l.y + rr.y) * 0.5f;
        judgeW = rr.x - l.x;

        Build(total);

        // 결과 씬을 미리 불러 둔다 (SongPreloader와 같은 방식). R0에 켠다
        loadOp = SceneManager.LoadSceneAsync(info.resultSceneName, LoadSceneMode.Single);
        if (loadOp != null) loadOp.allowSceneActivation = false;
        SceneManager.sceneLoaded += OnSceneLoaded;

        Render();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != info.resultSceneName || ResultActivated) return;
        ResultActivated = true;
        activatedAt = t;
        LandResultTime = Mathf.Max(0.05f, LandT - t);
        // 인게임 씬이 사라졌으니 하이웨이·카메라는 더 쓰지 않는다
        playField = null;
        gameCamera = null;
    }

    // ── 만들기 ──
    private void Build(int total)
    {
        IngameV2Art.SetKoreanFallback(settings.notoSansKr);
        var fKorean = IngameV2Art.MakeFont(settings.notoSansKr, null);
        var fLabel = IngameV2Art.MakeFont(settings.oxaniumBold, fKorean);
        var fStrip = IngameV2Art.MakeFont(settings.oxaniumExtraBold != null ? settings.oxaniumExtraBold : settings.oxaniumBold, fLabel);
        var fNumber = IngameV2Art.MakeFont(settings.sairaCondensed, fLabel);

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        // 결과 화면(ResultV2Controller)과 같은 맞춤 방식이어야 16:10 같은 화면에서도 착지 자리가 정확히 겹친다
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        // 화면 밖까지 덮는 검은 판 (16:10 등에서도 가장자리가 비치지 않게 화면 전체)
        black = IngameV2Art.Img("Black", transform, null, new Color(0, 0, 0, 0));
        var brt = black.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        black.raycastTarget = true; // 인게임 쪽(0 ~ 결과 씬 켜짐)에서는 클릭을 막는다

        stage = IngameV2Art.NewRect("Stage", transform);
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
        stage.pivot = new Vector2(0f, 1f);
        stage.sizeDelta = new Vector2(1600, 900);
        stage.anchoredPosition = new Vector2(-800, 450);

        BuildBanner(total, fNumber, fStrip);

        // 재킷 / 레코드 (SongTransitionOverlay와 같은 셰이더·텍스처)
        jkShadow = Raw("Jacket Shadow", stage, TransitionArt.SoftSquare(), new Color(0, 0, 0, 0));
        jkMat = DiscMaterial();
        jk = Quad("Jacket", jacketTex, jkMat);
        groovesMat = DiscMaterial();
        grooves = Quad("Record Grooves", TransitionArt.Grooves(), groovesMat);
        labelMat = DiscMaterial();
        label = Quad("Record Label", jacketTex, labelMat);
        sheenMat = DiscMaterial();
        sheen = Quad("Record Sheen", TransitionArt.Sheen(), sheenMat);
        var uv = new Vector4(jacketUV.x, jacketUV.y, jacketUV.width, jacketUV.height);
        jkMat.SetVector("_UVRect", uv);
        labelMat.SetVector("_UVRect", uv);
        labelMat.SetFloat("_Radius", 0.5f);
        labelMat.SetFloat("_Hole", 0.04f);
        groovesMat.SetFloat("_Radius", 0.5f);
        sheenMat.SetFloat("_Radius", 0.5f);
        label.localRect = new Rect(0.31f, 0.31f, 0.38f, 0.38f);

        for (int i = 0; i < 7; i++)
        {
            var s = IngameV2Art.NewRect("Glitch " + i, stage).gameObject.AddComponent<Image>();
            s.raycastTarget = false;
            s.color = Color.clear;
            slices.Add(s);
        }

        shapesGlow = IngameV2Art.NewRect("Shapes Glow", stage).gameObject.AddComponent<ScreenShapeGraphic>();
        Full0(shapesGlow.rectTransform);
        shapesGlow.texture = IngameV2Art.GlowProfile();
        shapesGlow.raycastTarget = false;
        shapes = IngameV2Art.NewRect("Shapes", stage).gameObject.AddComponent<ScreenShapeGraphic>();
        Full0(shapes.rectTransform);
        shapes.raycastTarget = false;
    }

    private void BuildBanner(int total, TMP_FontAsset fNumber, TMP_FontAsset fStrip)
    {
        // 시안: 가운데 위 y=330부터 118px 글자, 아래 6px 띄우고 민트 띠
        bannerRoot = IngameV2Art.NewRect("Result Banner", stage);
        IngameV2Art.PlaceCenter(bannerRoot, 800, 400, 1600, 160);
        bannerAlpha = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerAlpha.blocksRaycasts = false;

        string word = fullCombo ? "FULL COMBO" : "CLEAR";
        bannerMain = IngameV2Art.GlitchText("Title", bannerRoot, fNumber, word, 118, Color.white, TextAlignmentOptions.Center, 4f, true, 6f,
            out bannerRed, out bannerCyan);
        var trt = (RectTransform)bannerMain.transform.parent;
        // bannerRoot 왼쪽 위 = 시안 (0, 320)
        IngameV2Art.PlaceCenter(trt, 800, 389 - 320, 1600, 130);
        IngameV2Art.Glow(bannerMain, new Color(Mint.r, Mint.g, Mint.b, 0.9f), 0.8f);

        var r = info.result;
        string strip;
        if (fullCombo) strip = total + " / " + total + " NOTES";
        else
        {
            strip = "PERFECT " + r.perfectCount + " · GREAT " + r.greatCount;
            if (r.goodCount > 0) strip += " · GOOD " + r.goodCount;
            if (r.missCount > 0) strip += " · MISS " + r.missCount;
        }
        var stripRoot = IngameV2Art.NewRect("Strip", bannerRoot);
        stripText = IngameV2Art.Text("Text", stripRoot, fStrip, strip, 14, IngameV2Art.Hex(0x06201B), TextAlignmentOptions.Center, 60f);
        float w = stripText.preferredWidth + 44f, h = 23f;
        IngameV2Art.PlaceCenter(stripRoot, 800, 454 - 320 + h * 0.5f, w, h);
        var glow = Raw("Glow", stripRoot, TransitionArt.SoftSquare(), new Color(Mint.r, Mint.g, Mint.b, 0.55f));
        Stretch(glow.rectTransform, -22f);
        var bg = IngameV2Art.Img("BG", stripRoot, null, Mint);
        Stretch(bg.rectTransform, 0f);
        Stretch(stripText.rectTransform, 0f);
        // 시안 패딩 4px 18px 4px 26px (자간 때문에 글자가 오른쪽으로 치우친 것을 맞춘다)
        stripText.rectTransform.offsetMin = new Vector2(26f, 0f);
        stripText.rectTransform.offsetMax = new Vector2(-18f, 0f);
        stripText.transform.SetAsLastSibling();

        bannerAlpha.alpha = 0f;
    }

    private Material DiscMaterial()
    {
        Shader sh = settings.discShader != null ? settings.discShader : Shader.Find("CapDemo/UI/TransitionDisc");
        var m = new Material(sh);
        m.hideFlags = HideFlags.DontSave;
        return m;
    }

    private PerspectiveQuadGraphic Quad(string name, Texture tex, Material mat)
    {
        var q = IngameV2Art.NewRect(name, stage).gameObject.AddComponent<PerspectiveQuadGraphic>();
        Full0(q.rectTransform);
        q.texture = tex;
        if (mat != null) q.material = mat;
        q.raycastTarget = false;
        q.color = new Color(1, 1, 1, 0);
        return q;
    }

    // ── 매 프레임 ──
    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        // 결과 씬이 아직 안 켜졌으면 HoldT(정사각 재킷, 가운데)에서 시간을 멈추고 기다린다
        if (!ResultActivated && t + dt >= HoldT)
        {
            if (t < HoldT) t = HoldT;
            holdTime += dt;
        }
        else t += dt;

        // R0: 로딩이 끝났으면 결과 씬을 켠다 (테스트 지연: SongTransitionSettings.testExtraLoadSeconds)
        if (!activationRequested && t >= R0 && loadOp != null && loadOp.progress >= 0.9f
            && Time.realtimeSinceStartup - startRealtime >= settings.testExtraLoadSeconds)
        {
            activationRequested = true;
            loadOp.allowSceneActivation = true;
        }

        Render();

        // 끝: 착지 효과가 끝나면. 결과 화면이 착지를 알려 주지 않는 경우(화면을 못 찾음)에도 멈춰 있지 않게
        if (landedAt >= 0f && t >= landedAt + EffectLength) Destroy(gameObject);
        else if (landedAt < 0f && ResultActivated && t >= LandT + 0.4f) NotifyLanded();
    }

    // ── 그리기 (end2result.html render(t), ?v=A 와 같은 값) ──
    private static float Cl(float x) { return Mathf.Clamp01(x); }
    private static float Seg(float tt, float a, float b) { return Cl((tt - a) / (b - a)); }
    private static float Eio(float u) { return u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f; }
    private static float Back(float u, float s) { return 1f + (s + 1f) * Mathf.Pow(u - 1f, 3f) + s * Mathf.Pow(u - 1f, 2f); }
    private static float Hash(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

    private void Render()
    {
        shapes.Begin();
        shapesGlow.Begin();

        // 검은 판: 인게임 화면을 어둡게 하며 지우고(1.5 ~ 2.1), 결과 씬이 켜지면 0.5초에 걸쳐 걷는다
        float fo = Eio(Seg(t, 1.5f, 2.1f));
        float blackA = Seg(t, 1.5f, 2.1f) * 0.6f + Seg(t, 1.9f, 2.1f) * 0.4f;
        if (ResultActivated) blackA = 1f - Seg(t, activatedAt, activatedAt + ResultFadeIn);
        black.color = new Color(0, 0, 0, blackA);
        black.raycastTarget = !ResultActivated;

        RenderBanner();
        RenderScanBack(fo);
        RenderRecord();
        RenderGlitch(t > 1.5f && t < 1.72f);
        RenderLandOutline();

        shapes.End();
        shapesGlow.End();
    }

    private void RenderBanner()
    {
        if (t <= 0.35f || t >= 1.7f) { bannerAlpha.alpha = 0f; }
        else
        {
            bannerAlpha.alpha = 1f - Seg(t, 1.45f, 1.7f);
            float s = Mathf.LerpUnclamped(1.6f, 1f, Back(Seg(t, 0.35f, 0.6f), 1.4f));
            bannerRoot.localScale = Vector3.one * s;
        }

        // 링: (800, 390)에서 퍼지는 민트 타원
        if (t > 0.35f && t < 0.85f)
        {
            float k = Seg(t, 0.35f, 0.85f);
            Vector2 c = new Vector2(800f, 390f);
            float rx = 200f + k * 500f, ry = 40f + k * 90f;
            shapes.Ellipse(c, rx, ry, 3f * (1f - k) + 1f, new Color(Mint.r, Mint.g, Mint.b, 1f - k));
            shapesGlow.Ellipse(c, rx, ry, 14f, new Color(Mint.r, Mint.g, Mint.b, 0.5f * (1f - k)));
        }
    }

    // (C2) 빛의 벽이 게이트에서 판정선까지 돌아오며 지나간 자리(벽 뒤쪽)의 하이웨이를 지운다. 판정선은 남는다
    private void RenderScanBack(float fo)
    {
        if (!(t > 1.5f && t < 2.2f)) return;
        float zf = Mathf.Lerp(gateZ, judgeZ, fo);
        Color dark = new Color(0.01f, 0.03f, 0.035f, 0.95f);
        // 지운 자리 덮개 (벽 ~ 게이트 너머). 벽이 도착한 뒤(검은 판이 다 덮음)에는 레코드를 가리지 않게 그리지 않는다
        if (fo < 1f) {
        float z1 = gateZ + 0.6f;
        pts.Clear();
        pts.Add(Floor(-HalfWidth(zf) - 0.5f, zf));
        pts.Add(Floor(HalfWidth(zf) + 0.5f, zf));
        pts.Add(Floor(HalfWidth(z1) + 0.5f, z1));
        pts.Add(Floor(-HalfWidth(z1) - 0.5f, z1));
        shapes.Polygon(pts, dark);
        pts.Clear();
        pts.Add(Point(-HalfWidth(gateZ) - 0.4f, -0.05f, gateZ));
        pts.Add(Point(HalfWidth(gateZ) + 0.4f, -0.05f, gateZ));
        pts.Add(Point(HalfWidth(gateZ) + 0.4f, 0.8f, gateZ));
        pts.Add(Point(-HalfWidth(gateZ) - 0.4f, 0.8f, gateZ));
        shapes.Polygon(pts, dark);
        }

        // 빛의 벽 (돌아올수록 높아짐)
        if (fo > 0f && fo < 1f)
        {
            float hgt = 1.4f * fo + 0.2f;
            Vector2 l = Floor(-HalfWidth(zf), zf), r = Floor(HalfWidth(zf), zf);
            Vector2 lt = Point(-HalfWidth(zf), hgt, zf), rt = Point(HalfWidth(zf), hgt, zf);
            shapes.Gradient(l, r, rt, lt, new Color(Pale.r, Pale.g, Pale.b, 0.75f), new Color(Pale.r, Pale.g, Pale.b, 0f));
            shapes.Line(l, r, 4f, Color.white);
            shapesGlow.Line(l, r, 26f, new Color(Pale.r, Pale.g, Pale.b, 0.7f));
        }

        // 판정선: 벽이 도착하면 한 번 밝아졌다가 레코드로 벌어진다
        Vector2 jl = new Vector2(800f - judgeW / 2f, judgeY), jr = new Vector2(800f + judgeW / 2f, judgeY);
        float la = 0.6f + 0.4f * Seg(t, 1.95f, 2.1f);
        shapes.Line(jl, jr, 3f + fo * 3f, new Color(1f, 1f, 1f, la));
        shapesGlow.Line(jl, jr, 18f + fo * 8f, new Color(1f, 1f, 1f, 0.6f * la));
    }

    private void RenderRecord()
    {
        float op = 0f, x = 800f, y = 430f, sz = 400f, rx = 0f, rz = 0f, spin = 0f, omega = 0f, m = 1f;
        float holoK = 0f;
        if (t >= 2.05f)
        {
            float u = Eio(Seg(t, 2.3f, 2.8f));
            if (RiseFromJudgeLine)
            {
                y = Mathf.Lerp(judgeY, 430f, u);
                sz = Mathf.Lerp(judgeW * 0.98f, 400f, u);
            }
            // (C2) 판정선 한 줄이 위아래로 벌어지며 홀로그램 레코드가 맺힌다 (곡 선택 → 인게임의 "지직 꺼짐"을 거꾸로)
            holoK = 1f - Seg(t, 2.05f, 2.45f);
            rx = Mathf.Lerp(78f + 12f * Eio(Seg(holoK, 0.35f, 0.8f)), 0f, u);
            op = 1f - Seg(holoK, 0.75f, 1f);
            if (holoK > 0f) op *= 0.75f + 0.25f * Hash(Mathf.Floor(t * 30f));

            // 감속 회전: 각도 = 2400 x (d - d²/2), 각속도 = 2400 x (1 - d), 1초 뒤 멈춤
            float d = Mathf.Min(t - 2.05f, 1f);
            spin = 2400f * (d - d * d * 0.5f);
            omega = 2400f * (1f - d);
            m = 1f - Seg(t, 2.75f, 3.0f);

            // 착지
            float f = Eio(Seg(t, HoldT, LandT));
            if (t >= HoldT)
            {
                x = Mathf.Lerp(800f, 286f, f);
                y = Mathf.Lerp(430f, 390f, f);
                sz = Mathf.Lerp(400f, 380f, f);
                rz = Mathf.Sin(f * Mathf.PI) * -5f;
            }
            // 로딩 대기: 살짝 떠 있기
            if (!ResultActivated && holdTime > 0f) y += Mathf.Sin(holdTime * 2.4f) * 5f;
            // 결과 화면이 진짜 재킷을 켰으면 숨긴다
            if (landedAt >= 0f) op = 0f;
        }

        foreach (var q in new[] { jk, grooves, label, sheen })
        {
            q.center = new Vector2(x, y);
            q.size = sz;
            q.rotX = rx; q.rotY = 0f; q.rotZ = rz;
        }

        float spinRad = -spin * Mathf.Deg2Rad; // CSS rotate(+) = 시계방향
        jkMat.SetFloat("_Radius", 0.5f * Eio(m));
        jk.color = new Color(1, 1, 1, op * (1f - m * m));
        groovesMat.SetFloat("_Spin", spinRad);
        labelMat.SetFloat("_Spin", spinRad);
        labelMat.SetFloat("_BlurArc", m > 0f ? SongTransitionOverlay.SpinBlurArc(omega) : 0f);
        float split = 0.025f * Mathf.Sin(Mathf.Min(1f, holoK * 1.4f) * Mathf.PI);
        foreach (var hm in new[] { groovesMat, labelMat, sheenMat })
        {
            hm.SetFloat("_Holo", holoK);
            hm.SetFloat("_Split", hm == groovesMat ? split : 0f);
        }
        grooves.color = new Color(1, 1, 1, op * m);
        label.color = new Color(1, 1, 1, op * m);
        sheen.color = new Color(1, 1, 1, op * m);
        foreach (var q in new[] { jk, grooves, label, sheen })
        {
            q.SetMaterialDirty();
            q.Refresh();
        }

        // 외곽: 레코드일 때(m > 0.3) 민트 링 + 글로우, 정사각에 가까우면(모서리 < 5%) 민트 테두리 + 아래 그림자
        float br = 50f * Eio(m);
        jkShadow.color = new Color(0, 0, 0, 0);
        if (op <= 0f) return;
        if (m > 0.3f)
        {
            pts.Clear();
            for (int k = 0; k <= 64; k++)
            {
                float ang = k / 64f * Mathf.PI * 2f;
                pts.Add(jk.Project(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            shapes.Polyline(pts, 2f, new Color(Mint.r, Mint.g, Mint.b, 0.5f * op));
            shapesGlow.Polyline(pts, 36f, new Color(Mint.r, Mint.g, Mint.b, 0.45f * op));
        }
        else if (br < 5f)
        {
            float e = 2f / Mathf.Max(1f, sz); // 테두리 2px를 판 바깥쪽에
            pts.Clear();
            pts.Add(jk.Project(-e, -e));
            pts.Add(jk.Project(1f + e, -e));
            pts.Add(jk.Project(1f + e, 1f + e));
            pts.Add(jk.Project(-e, 1f + e));
            shapes.Polyline(pts, 2f, new Color(Mint.r, Mint.g, Mint.b, 0.9f * op), true);

            Vector2 c0 = jk.Project(0.5f, 0.5f);
            float w = Vector2.Distance(jk.Project(0f, 0.5f), jk.Project(1f, 0.5f));
            float h = Vector2.Distance(jk.Project(0.5f, 0f), jk.Project(0.5f, 1f));
            IngameV2Art.PlaceCenter(jkShadow.rectTransform, c0.x, c0.y + 20f, w + 100f, h + 100f);
            jkShadow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rz);
            jkShadow.color = new Color(0, 0, 0, 0.6f * op);
        }
    }

    // 착지 순간 재킷 둘레 사각 외곽선이 바깥으로 30px 퍼지며 사라짐 (land ~ land+0.5)
    private void RenderLandOutline()
    {
        if (landedAt < 0f) return;
        float since = t - landedAt;
        if (since >= 0.5f) return;
        float k = Seg(t, landedAt, landedAt + 0.35f);
        float x0 = 96f - k * 30f, y0 = 200f - k * 30f, s = 380f + k * 60f;
        pts.Clear();
        pts.Add(new Vector2(x0, y0));
        pts.Add(new Vector2(x0 + s, y0));
        pts.Add(new Vector2(x0 + s, y0 + s));
        pts.Add(new Vector2(x0, y0 + s));
        Color c = new Color(MintHi.r, MintHi.g, MintHi.b, 1f - k);
        shapes.Polyline(pts, 3f * (1f - k) + 1f, c, true);
        shapesGlow.Polyline(pts, 16f, new Color(c.r, c.g, c.b, 0.6f * (1f - k)), true);
    }

    private void RenderGlitch(bool on)
    {
        if (!on)
        {
            for (int i = 0; i < slices.Count; i++) slices[i].color = Color.clear;
            return;
        }
        float f = Mathf.Floor(t * 30f);
        if (f == lastGlitchFrame) return;
        lastGlitchFrame = f;
        for (int i = 0; i < slices.Count; i++)
        {
            float y = Hash(f * 11 + i) * 900f, h = 2f + Hash(f * 3 + i) * 14f;
            float gx = Hash(f * 5 + i) * 1200f - 200f, w = 150f + Hash(f * 7 + i) * 700f;
            IngameV2Art.Place(slices[i].rectTransform, gx, y, w, h);
            slices[i].color = i % 3 != 0 ? new Color(1f, 0.30f, 0.37f, 0.55f) : new Color(0.50f, 0.91f, 0.81f, 0.45f);
        }
    }

    // ── 하이웨이 좌표: 인게임 카메라가 있으면 그걸로, 없으면 시안 카메라 값(PJ)으로 ──
    private float HalfWidth(float z)
    {
        if (playField != null) return playField.HalfWidthAtZ(z);
        return 5.8f + (2.35f - 5.8f) * z / 20f;
    }

    private Vector2 Floor(float x, float z)
    {
        if (playField != null && gameCamera != null)
        {
            Vector3 s = gameCamera.WorldToScreenPoint(playField.transform.TransformPoint(new Vector3(x, 0f, z)));
            // 화면 → 시안 좌표 (Expand 맞춤, 가운데 1600 x 900 무대 기준)
            float k = Mathf.Max(0.0001f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            return new Vector2(800f + (s.x - Screen.width * 0.5f) / k, 450f - (s.y - Screen.height * 0.5f) / k);
        }
        return SongTransitionOverlay.PJ(x, 0f, z);
    }

    private Vector2 Point(float x, float y, float z)
    {
        if (playField != null && gameCamera != null)
        {
            Vector3 s = gameCamera.WorldToScreenPoint(playField.transform.TransformPoint(new Vector3(x, y, z)));
            float k = Mathf.Max(0.0001f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            return new Vector2(800f + (s.x - Screen.width * 0.5f) / k, 450f - (s.y - Screen.height * 0.5f) / k);
        }
        return SongTransitionOverlay.PJ(x, y, z);
    }

    // ── 도우미 ──
    private static RawImage Raw(string name, Transform parent, Texture tex, Color color)
    {
        var r = IngameV2Art.NewRect(name, parent).gameObject.AddComponent<RawImage>();
        r.texture = tex;
        r.color = color;
        r.raycastTarget = false;
        return r;
    }

    private static void Stretch(RectTransform rt, float outset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
    }

    // 시안 좌표를 그대로 쓰는 그래픽 (Stage 왼쪽 위가 원점)
    private static void Full0(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1600, 900);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Active == this) Active = null;
        // 결과 씬을 켜기 전에 사라지면(예외 상황) 미리 불러 둔 씬이 묶여 있지 않게 켜 준다
        if (loadOp != null && !loadOp.allowSceneActivation) loadOp.allowSceneActivation = true;
        foreach (var mat in new[] { jkMat, groovesMat, labelMat, sheenMat })
        {
            if (mat != null) Destroy(mat);
        }
    }
}
