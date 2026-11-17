using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 곡 선택 v2 → 인게임 v2 전환 연출 A (레코드 회전). 시안: 바탕화면 메인허브_시안\전환연출 (전환_A.mp4, transition.html)
// 플레이를 누른 순간부터 인게임 첫 박까지 화면 맨 위에서 이어지는 오버레이다. 씬이 바뀌어도 살아 있다(DontDestroyOnLoad).
// 로딩(SongPreloader)을 재킷 홀드 구간 안에 숨긴다.
//
// 타임라인 (플레이 누른 순간 0초, 모든 시간은 Time.unscaledDeltaTime 기준)
//   0   ~ 0.6     버튼 흰 플래시, 미리듣기 페이드아웃
//   0.6 ~ 1.0     재킷 흡입 (디스크 자리 → 홀드 자리) + 글리치 + 스캔 띠
//   1.0 ~ H1      재킷 홀드 + 로딩 (테두리 네온 = 진행률, 곡 정보 타이핑, 터미널 로그, 밴드 대사)
//   H1 ~ H1+1.25  재킷 → 레코드판 → 회전 → 판정선 위에 눕기 + 충격파, 인게임 등장
//   X  ~ X+1.8    드럼 카운트인 3, 2, 1
//   X+1.8 (GO)    첫 박: LIVE! 플래시, 곡 재생 시작 (OnFirstBeat)
//   H1 = 1.0 + max(1.8, 로딩 시간),  X = H1 + 1.25
// 좌표는 시안 좌표(1600 x 900)로 계산하고, CanvasScaler(기준 1600 x 900)가 실제 화면(1920 x 1080 등)에 맞춰 늘린다.
public class SongTransitionOverlay : MonoBehaviour
{
    // ── 바꿀 수 있는 상수 ──
    public const float H0 = 1.0f;
    public const float MinHold = 1.8f;
    public const float ExitLength = 1.25f;
    // 카운트 간격(초). 곡 BPM에 맞추려면 60f / BPM x 박수 로 바꾼다 (설정 SO의 countInterval이 우선)
    public static float CountInterval = 0.6f;
    // RhythmClock이 음악을 0.2초 뒤에 예약 재생하므로 그만큼 먼저 시작 신호를 보낸다
    private const float AudioLead = 0.2f;

    public static SongTransitionOverlay Active { get; private set; }

    // 첫 박에 인게임이 곡을 시작하도록 보내는 신호
    public event Action OnFirstBeat;
    public bool FirstBeatFired { get; private set; }
    public SongPreloader Preloader { get; private set; }
    public string SongName { get { return info.song != null ? info.song.DisplayName : ""; } }

    public class StartInfo
    {
        public ChartAutoLoader.SongEntry song;
        public string sceneName = "RhythmScene_v2";
        public string difficulty = "NORMAL";
        public int level;
        public RectTransform discRect;      // 곡 선택의 원형 디스크(커버) 자리
        public RectTransform playButtonRect;
        public AudioSource preview;          // 미리듣기 (0.4초에 걸쳐 끈다)
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Active = null;
    }

    // 곡 선택 화면에서 미리(한가할 때) 그림·글꼴을 만들어 둔다. 플레이를 누른 순간 화면이 멈칫하지 않게.
    // 여러 프레임에 나눠 만든다. 이미 만들었으면 바로 끝난다.
    public static IEnumerator Prewarm()
    {
        var st = SongTransitionSettings.Load();
        IngameV2Art.SetKoreanFallback(st.notoSansKr);
        yield return null;
        var korean = IngameV2Art.MakeFont(st.notoSansKr, null);
        var label = IngameV2Art.MakeFont(st.oxaniumBold, korean);
        yield return null;
        var title = IngameV2Art.MakeFont(st.oxaniumExtraBold != null ? st.oxaniumExtraBold : st.oxaniumBold, label);
        var number = IngameV2Art.MakeFont(st.sairaCondensed, label);
        var mono = IngameV2Art.MakeFont(st.jetBrainsMono, label);
        yield return null;
        // 자주 쓰는 글자를 미리 글꼴 그림에 넣어 둔다
        const string ascii = " !#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_abcdefghijklmnopqrstuvwxyz{|}~·…“”";
        foreach (var f in new[] { label, title, number, mono })
        {
            if (f != null) f.TryAddCharacters(ascii);
            yield return null;
        }
        if (korean != null) korean.TryAddCharacters("배속싱크노트일시정지" + ascii);
        yield return null;
        TransitionArt.Grid(); yield return null;
        TransitionArt.Grooves(); yield return null;
        TransitionArt.Sheen(); yield return null;
        TransitionArt.Dim(); TransitionArt.Hole(); TransitionArt.Sweep(); yield return null;
        TransitionArt.Flash(); TransitionArt.SoftSquare(); TransitionArt.TriangleRight(); yield return null;
        IngameV2Art.GlowProfile(); IngameV2Art.SoftDot(); IngameV2Art.ScanlineTile(0.14f);
        yield return null;

        // 인게임 v2 HUD에서 쓰는 글꼴·그림도 미리 (씬이 켜질 때 멈칫하지 않게)
        var ingameRegular = IngameV2Art.MakeFont(st.oxaniumSemiBold != null ? st.oxaniumSemiBold : st.oxaniumBold, korean);
        if (ingameRegular != null) ingameRegular.TryAddCharacters(ascii);
        yield return null;
        float k = IngameV2Art.K;
        int[,] panels = { { 420, 96 }, { 520, 46 }, { 330, 64 }, { 300, 100 } };
        for (int i = 0; i < panels.GetLength(0); i++)
        {
            IngameV2Art.GlassPanel(Mathf.RoundToInt(panels[i, 0] * k), Mathf.RoundToInt(panels[i, 1] * k));
            yield return null;
        }
        IngameV2Art.Vignette(); yield return null;
        IngameV2Art.Parallelogram(110, 22, 7);
        IngameV2Art.Parallelogram(Mathf.RoundToInt(136 * k), Mathf.RoundToInt(32 * k), 10);
        IngameV2Art.Parallelogram(Mathf.RoundToInt(136 * k), Mathf.RoundToInt(32 * k), 10, true);
        IngameV2Art.Parallelogram(32, 18, 0, true);
        IngameV2Art.Parallelogram(Mathf.RoundToInt(13.4f * k), Mathf.RoundToInt(9 * k), 4);
        yield return null;
        IngameV2Art.RingTexture(); IngameV2Art.SoftBar(); IngameV2Art.Beam(); IngameV2Art.Star();
    }

    // 곡 선택이 플레이 순간 부른다. 연출을 시작했으면 true (그 뒤 씬 이동은 오버레이가 맡는다)
    public static bool TryStart(StartInfo startInfo)
    {
        if (Active != null || startInfo == null || startInfo.song == null) return false;
        var go = new GameObject("Song Transition Overlay (A)");
        DontDestroyOnLoad(go);
        var overlay = go.AddComponent<SongTransitionOverlay>();
        overlay.Begin(startInfo);
        return true;
    }

    // ── 상태 ──
    private StartInfo info;
    private SongTransitionSettings settings;
    private float t;                 // 연출 시간
    private float h1 = 1e6f;         // 로딩이 끝나야 정해진다
    private float shownProgress;
    private bool built;
    private bool clicked0, clicked1, clicked2;
    private float lastGlitchFrame = -1;

    // 시안 좌표 값
    private Vector2 discCenter = new Vector2(390, 426);
    private float discSize = 300f;
    private Rect pressRect = new Rect(1286, 752, 250, 58);
    private static readonly Vector2 HoldPos = new Vector2(620, 440);
    private static readonly Vector2 CenterPos = new Vector2(800, 430);

    // ── 화면 조각 ──
    private RectTransform stage;
    private Canvas canvas;
    private RectTransform selectGroup;
    private RawImage snapSharp, snapBlur;
    private RenderTexture snapTexture;
    private RenderTexture blurTexture;
    private RawImage press, hole, dim, grid, flash, scan;
    private readonly List<Image> slices = new List<Image>();
    private RectTransform infoGroup;
    private CanvasGroup infoAlpha;
    private TextMeshProUGUI syncLabel, titleMain, titleRed, titleCyan, artist, opts, bestLabel, bestValue, rankLabel, rankValue;
    private RawImage syncIcon;
    private Image titleCursor;
    private readonly List<RectTransform> chips = new List<RectTransform>();
    private readonly List<CanvasGroup> chipAlpha = new List<CanvasGroup>();
    private TextMeshProUGUI chipBpm, chipLength, chipNotes;
    private RectTransform bestGroup;
    private CanvasGroup optsAlpha, bestAlpha;
    private TextMeshProUGUI term, pct;
    private CanvasGroup termAlpha;
    private RectTransform dlgGroup;
    private CanvasGroup dlgAlpha;
    private TextMeshProUGUI dlgLine, dlgWho;
    private Image dlgTag;
    private TextMeshProUGUI countMain, countRed, countCyan, countLabel, liveText;
    private RectTransform countRoot;

    private RawImage jkShadow, jkGlow;
    private PerspectiveQuadGraphic jk, rgbR, rgbB, sweep, grooves, label, sheen;
    private Material jkMat, rgbRMat, rgbBMat, groovesMat, labelMat, sheenMat;
    private ScreenShapeGraphic shapes, shapesGlow;
    private RawImage ringDot;

    private string title, artistText, bandLine, bandSpeaker, slug;
    private Texture jacketTex;
    private Rect jacketUV = new Rect(0, 0, 1, 1);
    private AudioSource sfx;
    private RhythmPlayFieldV2 gamePlayField;
    private float judgeY = 650f, judgeW = 745f;
    private Vector2[] floorQuad;

    // ── 시작 ──
    private void Begin(StartInfo startInfo)
    {
        Active = this;
        info = startInfo;
        settings = SongTransitionSettings.Load();
        if (settings.countInterval > 0.05f) CountInterval = settings.countInterval;

        Preloader = gameObject.AddComponent<SongPreloader>();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.ignoreListenerPause = true;
        sfx.spatialBlend = 0f;

        title = RhythmGameUIV2.PrettyTitle(info.song.DisplayName);
        // 로그용 이름: 영문 소문자 + 밑줄 (일본어 등 글꼴에 없는 글자 빼기)
        var sbSlug = new System.Text.StringBuilder();
        foreach (char ch in title.ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sbSlug.Append(ch);
            else if (ch == ' ' || ch == '-' || ch == '_') { if (sbSlug.Length > 0 && sbSlug[sbSlug.Length - 1] != '_') sbSlug.Append('_'); }
        }
        slug = sbSlug.ToString().Trim('_');
        if (slug.Length == 0) slug = "track";
        artistText = settings.artistText;
        var line = settings.LineFor(info.song.DisplayName);
        bandSpeaker = line != null ? line.speaker : "BASS";
        bandLine = line != null ? line.line : "";

        // 곡 선택 화면의 디스크·버튼 위치를 시안 좌표로
        Rect r;
        if (TryDesignRect(info.discRect, out r))
        {
            discCenter = r.center;
            discSize = Mathf.Min(r.width, r.height);
        }
        if (TryDesignRect(info.playButtonRect, out r)) pressRect = r;

        // 재킷: 정사각형 원본 (아니면 가운데 1:1)
        jacketTex = info.song.albumCover != null ? (Texture)info.song.albumCover : settings.noCover;
        jacketUV = jacketTex != null ? SongSelectRow.SquareCrop(jacketTex) : new Rect(0, 0, 1, 1);

        if (info.preview != null) StartCoroutine(FadePreview(info.preview));
        StartCoroutine(CaptureThenBuild());
        Preloader.Begin(info.song, info.sceneName, settings.testExtraLoadSeconds);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static bool TryDesignRect(RectTransform rt, out Rect rect)
    {
        rect = default(Rect);
        if (rt == null) return false;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var c = rt.GetComponentInParent<Canvas>();
        Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 da = ScreenToDesign(a), db = ScreenToDesign(b);
        rect = Rect.MinMaxRect(Mathf.Min(da.x, db.x), Mathf.Min(da.y, db.y), Mathf.Max(da.x, db.x), Mathf.Max(da.y, db.y));
        return true;
    }

    private static Vector2 ScreenToDesign(Vector2 s)
    {
        return new Vector2(s.x * 1600f / Screen.width, (Screen.height - s.y) * 900f / Screen.height);
    }

    private IEnumerator FadePreview(AudioSource src)
    {
        float from = src.volume;
        for (float k = 0f; k < 0.4f && src != null; k += Time.unscaledDeltaTime)
        {
            src.volume = Mathf.Lerp(from, 0f, k / 0.4f);
            yield return null;
        }
        if (src != null) src.Stop();
    }

    // 지금 곡 선택 화면을 한 장 찍어서 오버레이 맨 아래에 깐다 (흐리게·어둡게 만들 재료)
    private IEnumerator CaptureThenBuild()
    {
        yield return new WaitForEndOfFrame();
        // GPU 안에서만 복사한다 (CPU로 읽어 오면 화면이 잠깐 멈춘다).
        // 프로젝트가 Linear 색 공간이라 sRGB 렌더 텍스처에 받아야 색이 하얗게 뜨지 않는다.
        int w = Mathf.Max(8, Screen.width), h = Mathf.Max(8, Screen.height);
        snapTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        snapTexture.wrapMode = TextureWrapMode.Clamp;
        ScreenCapture.CaptureScreenshotIntoRenderTexture(snapTexture);
        // 1/8로 줄여 늘리면 블러 약 7px 느낌
        blurTexture = new RenderTexture(Mathf.Max(8, w / 8), Mathf.Max(8, h / 8), 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var half = RenderTexture.GetTemporary(w / 2, h / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var quarter = RenderTexture.GetTemporary(w / 4, h / 4, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(snapTexture, half);
        Graphics.Blit(half, quarter);
        Graphics.Blit(quarter, blurTexture);
        RenderTexture.ReleaseTemporary(half);
        RenderTexture.ReleaseTemporary(quarter);
        // 화면 조각을 여러 프레임에 나눠 만든다 (한 프레임에 다 만들면 40ms쯤 멈칫한다)
        yield return StartCoroutine(Build());
        built = true;
        Render();
        canvas.enabled = true;
    }

    // ── 만들기 ──
    private TMP_FontAsset fLabel, fTitle, fNumber, fMono, fKorean;

    private IEnumerator Build()
    {
        IngameV2Art.SetKoreanFallback(settings.notoSansKr);
        fKorean = IngameV2Art.MakeFont(settings.notoSansKr, null);
        fLabel = IngameV2Art.MakeFont(settings.oxaniumBold, fKorean);
        fTitle = IngameV2Art.MakeFont(settings.oxaniumExtraBold != null ? settings.oxaniumExtraBold : settings.oxaniumBold, fLabel);
        fNumber = IngameV2Art.MakeFont(settings.sairaCondensed, fLabel);
        fMono = IngameV2Art.MakeFont(settings.jetBrainsMono, fLabel);

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        // 여러 프레임에 나눠 만드는 동안 덜 만든 글씨가 보이지 않게, 다 만들 때까지 숨긴다
        canvas.enabled = false;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        stage = NewRect("Stage", transform);
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
        stage.pivot = new Vector2(0f, 1f);
        stage.sizeDelta = new Vector2(1600, 900);
        stage.anchoredPosition = new Vector2(-800, 450);

        // 0) 곡 선택 화면 (찍은 사진)
        selectGroup = NewRect("Song Select (snapshot)", stage);
        Full(selectGroup);
        selectGroup.pivot = new Vector2(0.5f, 0.5f);
        snapSharp = Raw("Sharp", selectGroup, snapTexture, Color.white);
        Full(snapSharp.rectTransform);
        snapSharp.raycastTarget = true; // 연출 중 클릭 막기
        snapBlur = Raw("Blur", selectGroup, blurTexture, new Color(1, 1, 1, 0));
        Full(snapBlur.rectTransform);
        // DirectX 등에서는 화면 캡처가 위아래로 뒤집혀 들어온다
        if (SystemInfo.graphicsUVStartsAtTop)
        {
            snapSharp.uvRect = new Rect(0, 1, 1, -1);
            snapBlur.uvRect = new Rect(0, 1, 1, -1);
        }

        press = Raw("Press Flash", stage, IngameV2Art.Parallelogram(250, 58, 14).texture, new Color(1, 1, 1, 0));
        Place(press.rectTransform, pressRect.x, pressRect.y, pressRect.width, pressRect.height);

        hole = Raw("Disc Hole", stage, TransitionArt.Hole(), new Color(1, 1, 1, 0));
        PlaceCenter(hole.rectTransform, discCenter.x, discCenter.y, discSize, discSize);

        dim = Raw("Dim", stage, TransitionArt.Dim(), new Color(1, 1, 1, 0));
        Full(dim.rectTransform);
        grid = Raw("Grid", stage, TransitionArt.Grid(), new Color(1, 1, 1, 0));
        Full(grid.rectTransform);

        for (int i = 0; i < 7; i++)
        {
            var s = NewRect("Glitch " + i, stage).gameObject.AddComponent<Image>();
            s.raycastTarget = false;
            s.color = Color.clear;
            slices.Add(s);
        }
        yield return null;

        BuildInfo();
        yield return null;
        BuildTerminal();
        yield return null;

        // 재킷 / 레코드
        jkShadow = Raw("Jacket Shadow", stage, TransitionArt.SoftSquare(), new Color(0, 0, 0, 0));
        jkGlow = Raw("Jacket Glow", stage, TransitionArt.SoftSquare(), new Color(0.64f, 0.92f, 0.84f, 0f));
        jkMat = DiscMaterial(false);
        jk = Quad("Jacket", jacketTex, jkMat);
        rgbRMat = DiscMaterial(true);
        rgbR = Quad("Jacket RGB R", jacketTex, rgbRMat);
        rgbR.color = new Color(1f, 0.30f, 0.37f, 0f);
        rgbBMat = DiscMaterial(true);
        rgbB = Quad("Jacket RGB C", jacketTex, rgbBMat);
        rgbB.color = new Color(0.35f, 0.91f, 1f, 0f);
        sweep = Quad("Scan Sweep", TransitionArt.Sweep(), null);
        groovesMat = DiscMaterial(false);
        grooves = Quad("Record Grooves", TransitionArt.Grooves(), groovesMat);
        labelMat = DiscMaterial(false);
        label = Quad("Record Label", jacketTex, labelMat);
        sheenMat = DiscMaterial(false);
        sheen = Quad("Record Sheen", TransitionArt.Sheen(), sheenMat);
        foreach (var m in new[] { jkMat, rgbRMat, rgbBMat }) m.SetVector("_UVRect", new Vector4(jacketUV.x, jacketUV.y, jacketUV.width, jacketUV.height));
        labelMat.SetVector("_UVRect", new Vector4(jacketUV.x, jacketUV.y, jacketUV.width, jacketUV.height));
        labelMat.SetFloat("_Radius", 0.5f);
        labelMat.SetFloat("_Hole", 0.04f);
        groovesMat.SetFloat("_Radius", 0.5f);
        sheenMat.SetFloat("_Radius", 0.5f);
        jkMat.SetFloat("_Scan", 1f);
        yield return null;

        shapesGlow = NewRect("Shapes Glow", stage).gameObject.AddComponent<ScreenShapeGraphic>();
        Full0(shapesGlow.rectTransform);
        shapesGlow.texture = IngameV2Art.GlowProfile();
        shapesGlow.raycastTarget = false;
        shapes = NewRect("Shapes", stage).gameObject.AddComponent<ScreenShapeGraphic>();
        Full0(shapes.rectTransform);
        shapes.raycastTarget = false;
        ringDot = Raw("Ring Dot", stage, IngameV2Art.SoftDotTexture(), new Color(1, 1, 1, 0));

        BuildDialogue();
        yield return null;
        BuildCountdown();
        yield return null;

        flash = Raw("First Beat Flash", stage, TransitionArt.Flash(), new Color(1, 1, 1, 0));
        Full(flash.rectTransform);
        flash.material = AdditiveUIMaterial();

        scan = Raw("Scanlines", stage, IngameV2Art.ScanlineTile(0.14f), Color.white);
        Full(scan.rectTransform);
        scan.uvRect = new Rect(0, 0, 1, 900f / 3f);

        UpdateJudgeGeometry();
    }

    private Material DiscMaterial(bool additive)
    {
        Shader sh = settings.discShader != null ? settings.discShader : Shader.Find("CapDemo/UI/TransitionDisc");
        var m = new Material(sh);
        m.hideFlags = HideFlags.DontSave;
        if (additive)
        {
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }
        return m;
    }

    private Material additiveUI;
    private Material AdditiveUIMaterial()
    {
        if (additiveUI != null) return additiveUI;
        additiveUI = DiscMaterial(true);
        return additiveUI;
    }

    private PerspectiveQuadGraphic Quad(string name, Texture tex, Material mat)
    {
        var q = NewRect(name, stage).gameObject.AddComponent<PerspectiveQuadGraphic>();
        Full0(q.rectTransform);
        q.texture = tex;
        if (mat != null) q.material = mat;
        q.raycastTarget = false;
        q.color = new Color(1, 1, 1, 0);
        return q;
    }

    private void BuildInfo()
    {
        infoGroup = NewRect("Song Info", stage);
        Place(infoGroup, 880, 256, 620, 320);
        infoAlpha = infoGroup.gameObject.AddComponent<CanvasGroup>();
        Color mintLabel = new Color(0.64f, 0.92f, 0.84f, 0.75f);
        Color mute = new Color(0.93f, 0.97f, 0.96f, 0.55f);

        syncIcon = Raw("Sync Icon", infoGroup, TransitionArt.TriangleRight(), mintLabel);
        Place(syncIcon.rectTransform, 0, 2, 9, 10);
        syncLabel = Text("Sync Label", infoGroup, fLabel, "STAGE SYNC", 11, mintLabel, TextAlignmentOptions.TopLeft, 30f);
        Place(syncLabel.rectTransform, 15, 0, 300, 16);

        titleMain = IngameV2Art.GlitchText("Title", infoGroup, fTitle, "", 68, Color.white, TextAlignmentOptions.TopLeft, 3f, false, 0f,
            out titleRed, out titleCyan);
        Place((RectTransform)titleMain.transform.parent, 0, 24, 900, 80);
        IngameV2Art.Glow(titleMain, new Color(0.64f, 0.92f, 0.84f, 0.7f), 0.8f);
        titleCursor = new GameObject("Cursor", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        titleCursor.transform.SetParent(infoGroup, false);
        titleCursor.raycastTarget = false;
        titleCursor.color = Color.white;
        Place(titleCursor.rectTransform, 0, 34, 22, 56);

        artist = Text("Artist", infoGroup, fLabel, "", 16, mute, TextAlignmentOptions.TopLeft, 24f);
        Place(artist.rectTransform, 0, 108, 600, 22);

        // 칩: 난이도, BPM, LENGTH, NOTES
        float x = 0f;
        x = Chip("Difficulty", info.difficulty, info.level > 0 ? info.level.ToString() : "", x, true, out _);
        x = Chip("BPM", "BPM", "--", x, false, out chipBpm);
        x = Chip("Length", "LENGTH", FormatTime(info.song.audioClip != null ? info.song.audioClip.length : 0f), x, false, out chipLength);
        Chip("Notes", "NOTES", "--", x, false, out chipNotes);

        opts = Text("Options", infoGroup, fKorean, "", 13, mute, TextAlignmentOptions.TopLeft, 14f);
        opts.richText = true;
        Place(opts.rectTransform, 0, 206, 620, 20);
        optsAlpha = opts.gameObject.AddComponent<CanvasGroup>();
        float speed = GameSettings.NoteSpeed;
        int sync = GameSettings.SyncMs;
        opts.text = "배속 <color=#EEF7F4><b>" + speed.ToString("0.0") + "</b></color>  ·  싱크 <color=#EEF7F4><b>" + (sync >= 0 ? "+" : "") + sync
            + "ms</b></color>  ·  노트 <color=#EEF7F4><b>MINT / RED</b></color>";

        bestGroup = NewRect("Best", infoGroup);
        Place(bestGroup, 0, 252, 620, 60);
        bestAlpha = bestGroup.gameObject.AddComponent<CanvasGroup>();
        var rec = RhythmBestRecords.Get(info.song.DisplayName, info.difficulty);
        bestLabel = Text("Best Label", bestGroup, fLabel, "BEST · " + info.difficulty, 11, mute, TextAlignmentOptions.TopLeft, 30f);
        Place(bestLabel.rectTransform, 0, 0, 220, 16);
        bestValue = Text("Best Value", bestGroup, fNumber, rec.HasRecord ? rec.score.ToString("N0") : "—", 34, Color.white, TextAlignmentOptions.TopLeft, 0f, true);
        Place(bestValue.rectTransform, 0, 18, 220, 40);
        rankLabel = Text("Rank Label", bestGroup, fLabel, "RANK", 11, mute, TextAlignmentOptions.TopLeft, 30f);
        Place(rankLabel.rectTransform, Mathf.Max(150, bestValue.preferredWidth + 30), 0, 100, 16);
        rankValue = Text("Rank Value", bestGroup, fNumber, rec.HasRecord ? rec.rank : "—", 34, new Color(0.64f, 0.92f, 0.84f), TextAlignmentOptions.TopLeft, 0f, true);
        Place(rankValue.rectTransform, Mathf.Max(150, bestValue.preferredWidth + 30), 18, 100, 40);
    }

    private float Chip(string name, string key, string value, float x, bool difficulty, out TextMeshProUGUI valueText)
    {
        var root = NewRect("Chip " + name, infoGroup);
        var g = root.gameObject.AddComponent<CanvasGroup>();
        var keyText = Text("Key", root, fLabel, key, 14, difficulty ? new Color(0.1f, 0.02f, 0.03f) : new Color(0.93f, 0.97f, 0.96f, 0.9f),
            TextAlignmentOptions.MidlineLeft, 12f);
        valueText = Text("Value", root, fNumber, value, 20, difficulty ? new Color(0.1f, 0.02f, 0.03f) : Color.white, TextAlignmentOptions.MidlineLeft, 0f, true);
        float kw = keyText.preferredWidth, vw = Mathf.Max(valueText.preferredWidth, value == "--" ? 22 : 0);
        float w = 14 + kw + (value.Length > 0 ? 6 + vw + 8 : 0) + 14;
        if (!difficulty && name == "Notes") w = Mathf.Max(w, 112);
        if (!difficulty && name == "BPM") w = Mathf.Max(w, 90);
        int iw = Mathf.CeilToInt(w * 1.2f), ih = Mathf.CeilToInt(32 * 1.2f);
        var bg = Raw("BG", root, IngameV2Art.Parallelogram(iw, ih, 10).texture,
            difficulty ? IngameV2Art.DifficultyColor(info.difficulty) : new Color(0.64f, 0.92f, 0.84f, 0.06f));
        Full(bg.rectTransform);
        bg.transform.SetAsFirstSibling();
        if (!difficulty)
        {
            var line = Raw("Line", root, IngameV2Art.Parallelogram(iw, ih, 10, true).texture, new Color(0.64f, 0.92f, 0.84f, 0.35f));
            Full(line.rectTransform);
            line.transform.SetSiblingIndex(1);
        }
        Place(keyText.rectTransform, 14, 0, kw + 4, 32);
        Place(valueText.rectTransform, 14 + kw + 6, 0, vw + 30, 32);
        Place(root, x, 154, w, 32);
        chips.Add(root);
        chipAlpha.Add(g);
        return x + w + 10;
    }

    private void BuildTerminal()
    {
        term = Text("Terminal", stage, fMono, "", 12.5f, new Color(0.64f, 0.92f, 0.84f, 0.72f), TextAlignmentOptions.BottomLeft, 0f);
        term.richText = true;
        term.lineSpacing = 42f;
        Place(term.rectTransform, 64, 900 - 118 - 200, 700, 200);
        termAlpha = term.gameObject.AddComponent<CanvasGroup>();
        pct = Text("Percent", stage, fLabel, "", 13, new Color(0.64f, 0.92f, 0.84f), TextAlignmentOptions.BottomLeft, 30f);
        pct.richText = true;
        Place(pct.rectTransform, 64, 900 - 84 - 30, 500, 30);
    }

    private void BuildDialogue()
    {
        dlgGroup = NewRect("Band Line", stage);
        dlgAlpha = dlgGroup.gameObject.AddComponent<CanvasGroup>();
        dlgTag = new GameObject("Tag", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        dlgTag.transform.SetParent(dlgGroup, false);
        dlgTag.color = new Color(0.64f, 0.92f, 0.84f);
        dlgTag.raycastTarget = false;
        var tagGlow = Raw("Tag Glow", dlgTag.rectTransform, IngameV2Art.SoftDotTexture(), new Color(0.64f, 0.92f, 0.84f, 0.35f));
        tagGlow.rectTransform.anchorMin = Vector2.zero;
        tagGlow.rectTransform.anchorMax = Vector2.one;
        tagGlow.rectTransform.offsetMin = new Vector2(-16, -14);
        tagGlow.rectTransform.offsetMax = new Vector2(16, 14);
        dlgWho = Text("Who", dlgTag.rectTransform, fTitle, bandSpeaker, 12, new Color(0.03f, 0.07f, 0.07f), TextAlignmentOptions.Center, 26f);
        dlgWho.rectTransform.anchorMin = Vector2.zero;
        dlgWho.rectTransform.anchorMax = Vector2.one;
        dlgWho.rectTransform.offsetMin = dlgWho.rectTransform.offsetMax = Vector2.zero;
        dlgLine = Text("Line", dlgGroup, fKorean, "", 22, Color.white, TextAlignmentOptions.MidlineLeft, 0f);
        IngameV2Art.Glow(dlgLine, new Color(0, 0, 0, 0.9f), 0.5f);

        // 가운데 정렬: 전체 문장 길이로 미리 자리를 잡는다
        dlgLine.text = Open + bandLine + Close;
        float lw = dlgLine.preferredWidth;
        float tw = dlgWho.preferredWidth + 24f;
        float total = tw + 16f + lw;
        float x0 = 800f - total * 0.5f;
        Place(dlgTag.rectTransform, x0, 900 - 64 - 30, tw, 22);
        Place(dlgLine.rectTransform, x0 + tw + 16f, 900 - 64 - 34, lw + 40, 30);
        dlgLine.text = "";
        Place(dlgGroup, 0, 0, 1600, 900);
    }

    private void BuildCountdown()
    {
        countRoot = NewRect("Count", stage);
        PlaceCenter(countRoot, 800, 410, 400, 240);
        countMain = IngameV2Art.GlitchText("Number", countRoot, fNumber, "", 220, Color.white, TextAlignmentOptions.Center, 4f, true, 0f,
            out countRed, out countCyan);
        var nrt = (RectTransform)countMain.transform.parent;
        nrt.anchorMin = Vector2.zero;
        nrt.anchorMax = Vector2.one;
        nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        IngameV2Art.Glow(countMain, new Color(0.64f, 0.92f, 0.84f, 0.6f), 0.6f);
        countLabel = Text("Count Label", stage, fLabel, "DRUMS · COUNT-IN", 13, new Color(0.64f, 0.92f, 0.84f), TextAlignmentOptions.Center, 50f);
        PlaceCenter(countLabel.rectTransform, 800, 522, 500, 20);
        liveText = Text("LIVE", stage, fNumber, "LIVE!", 120, Color.white, TextAlignmentOptions.Center, 8f, true);
        PlaceCenter(liveText.rectTransform, 800, 410, 900, 220);
        IngameV2Art.Glow(liveText, new Color(0.64f, 0.92f, 0.84f, 0.7f), 0.8f);
    }

    // ── 매 프레임 ──
    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (!built) return;

        // 레코드가 판정선에 눕기 전에 인게임 씬이 준비되지 않았으면 시간을 잠깐 멈춘다 (화면이 비지 않게)
        bool waitGame = t >= h1 + 0.9f && !GameReady();
        if (!waitGame) t += dt;

        // 진행률: 화면에서는 부드럽게 따라간다
        if (t >= H0)
        {
            float target = Preloader.Progress;
            float speed = Mathf.Max(0.35f, (target - shownProgress) * 3f);
            shownProgress = Mathf.MoveTowards(shownProgress, target, speed * dt);
        }

        // H1: 홀드 최소 시간 + 로딩 완료 둘 다
        if (h1 > 1e5f && t >= H0 + MinHold && Preloader.IsDone && shownProgress >= 0.999f)
        {
            h1 = t;
            shownProgress = 1f;
        }

        // 인게임 씬을 켜는 순간(씬 통합)은 에디터에서 0.3초쯤 멈칫한다.
        // 요청서는 레코드가 눕기 시작할 때(H1+0.78)였지만, 그때 멈추면 회전이 끊겨 보여서
        // 퇴장 글리치가 튀는 H1 순간으로 당겼다 (멈칫해도 글리치처럼 보인다)
        if (t >= h1) Preloader.ActivateScene();

        float x = h1 + ExitLength;
        // 카운트 효과음
        if (!clicked0 && t >= x) { clicked0 = true; Click(); }
        if (!clicked1 && t >= x + CountInterval) { clicked1 = true; Click(); }
        if (!clicked2 && t >= x + CountInterval * 2f) { clicked2 = true; Click(); }

        float go = x + CountInterval * 3f;
        if (!FirstBeatFired && t >= go - AudioLead)
        {
            FirstBeatFired = true;
            if (OnFirstBeat != null) OnFirstBeat();
        }

        Render();

        if (t >= go + 0.6f) Destroy(gameObject);
    }

    private bool GameReady()
    {
        return Preloader.SceneActivated && SceneManager.GetActiveScene().name == info.sceneName;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != info.sceneName) return;
        gamePlayField = FindAnyObjectByType<RhythmPlayFieldV2>();
        UpdateJudgeGeometry();
    }

    private void Click()
    {
        // 재이 결정(2026-11-17): 카운트다운 소리는 넣지 않는다. 설정에 소리를 직접 넣었을 때만 난다
        if (settings.countInClick != null) sfx.PlayOneShot(settings.countInClick, settings.countInVolume);
    }

    // 판정선 화면 위치: 인게임 카메라가 있으면 그걸로, 없으면 같은 카메라 값으로 계산
    private void UpdateJudgeGeometry()
    {
        Vector2 l, r, gl, gr;
        float judgeZ = 1.15f, gateZ = 19.85f;
        float hwJ = Mathf.Lerp(5.8f, 2.35f, judgeZ / 20f), hwG = Mathf.Lerp(5.8f, 2.35f, gateZ / 20f);
        Camera cam = Camera.main;
        if (gamePlayField != null && cam != null)
        {
            judgeZ = gamePlayField.JudgeZ;
            gateZ = gamePlayField.FarGateZ;
            hwJ = gamePlayField.HalfWidthAtZ(judgeZ);
            hwG = gamePlayField.HalfWidthAtZ(gateZ);
            Func<Vector3, Vector2> P = w => ScreenToDesign(cam.WorldToScreenPoint(gamePlayField.transform.TransformPoint(w)));
            l = P(new Vector3(-hwJ, 0, judgeZ)); r = P(new Vector3(hwJ, 0, judgeZ));
            gl = P(new Vector3(-hwG, 0, gateZ)); gr = P(new Vector3(hwG, 0, gateZ));
        }
        else
        {
            l = PJ(-hwJ, 0, judgeZ); r = PJ(hwJ, 0, judgeZ);
            gl = PJ(-hwG, 0, gateZ); gr = PJ(hwG, 0, gateZ);
        }
        judgeY = (l.y + r.y) * 0.5f;
        judgeW = r.x - l.x;
        floorQuad = new[] { l, r, gr, gl };
    }

    // transition.html의 PJ와 같은 계산 (카메라 (0,8,-9.2) → (0,0,8.2), 세로 FOV 55°, 1600x900)
    private static Vector2 PJ(float x, float y, float z)
    {
        float tn = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad);
        Vector3 c = new Vector3(0, 8, -9.2f), target = new Vector3(0, 0, 8.2f);
        Vector3 f = (target - c).normalized;
        Vector3 rgt = Vector3.Cross(Vector3.up, f).normalized;
        Vector3 up = Vector3.Cross(f, rgt);
        Vector3 d = new Vector3(x, y, z) - c;
        float zc = Vector3.Dot(d, f);
        return new Vector2(800f + Vector3.Dot(d, rgt) / zc / tn * 450f, 450f - Vector3.Dot(d, up) / zc / tn * 450f);
    }

    // ── 그리기 (transition.html render(t)와 같은 값) ──
    private static float Cl(float x) { return Mathf.Clamp01(x); }
    private static float Seg(float tt, float a, float b) { return Cl((tt - a) / (b - a)); }
    private static float Eio(float u) { return u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f; }
    private static float Eo(float u) { return 1f - Mathf.Pow(1f - u, 3f); }
    private static float Eback(float u) { return 1f + 2.2f * Mathf.Pow(u - 1f, 3f) + 1.2f * Mathf.Pow(u - 1f, 2f); }
    private static float Hash(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

    private void Render()
    {
        if (!built) return;
        float H1 = h1, X = H1 + ExitLength, GO = X + CountInterval * 3f;

        // 0. 버튼 플래시
        press.color = new Color(1, 1, 1, (t > 0.3f && t < 0.75f) ? 0.55f * (1f - Seg(t, 0.35f, 0.75f)) : 0f);

        // 1. 곡 선택 화면: 블러·어둡게·살짝 확대 → 인게임 등장 때 사라짐
        float s1 = Seg(t, 0.55f, 1.0f);
        float gIn = Seg(t, H1 + 0.95f, H1 + 1.4f);
        float selA = (t < X + 0.4f ? 1f : 1f - Seg(t, X + 0.4f, X + 0.9f)) * (1f - gIn);
        float bright = 1f - s1 * 0.55f;
        selectGroup.localScale = Vector3.one * (1f + s1 * 0.04f);
        snapSharp.color = new Color(bright, bright, bright, selA * (1f - s1));
        snapBlur.color = new Color(bright, bright, bright, selA * s1);
        // 오버레이 아래 실제 씬이 비치지 않게: 사진이 사라지기 전까지는 클릭도 막는다
        snapSharp.raycastTarget = gIn < 1f;
        hole.color = new Color(1, 1, 1, Seg(t, 0.55f, 0.7f) * (1f - Seg(t, 1.0f, 1.4f)) * (1f - gIn));
        dim.color = new Color(1, 1, 1, s1 * 0.9f * (1f - Seg(t, X + 0.2f, X + 0.8f)));
        grid.color = new Color(1, 1, 1, Seg(t, 0.9f, 1.4f) * 0.9f * (1f - Seg(t, X - 0.1f, X + 0.3f)));

        // 글리치 슬라이스
        bool burst = (t > 0.58f && t < 0.95f) || (t > H1 - 0.05f && t < H1 + 0.12f);
        bool loadingGlitch = t > H0 && t < H1 && Hash(Mathf.Floor(t * 12f)) > 0.93f;
        if (burst || loadingGlitch)
        {
            float f = Mathf.Floor(t * 30f);
            if (f != lastGlitchFrame)
            {
                lastGlitchFrame = f;
                for (int i = 0; i < slices.Count; i++)
                {
                    float y = Hash(f * 11 + i) * 900f, h = 2f + Hash(f * 3 + i) * 14f;
                    float gx = Hash(f * 5 + i) * 1200f - 200f, w = 150f + Hash(f * 7 + i) * 700f;
                    Place(slices[i].rectTransform, gx, y, w, h);
                    slices[i].color = i % 3 != 0 ? new Color(1f, 0.30f, 0.37f, 0.55f) : new Color(0.64f, 0.92f, 0.84f, 0.45f);
                }
            }
        }
        else
        {
            for (int i = 0; i < slices.Count; i++) slices[i].color = Color.clear;
        }

        RenderJacket(H1, X, burst);
        RenderInfo(H1);
        RenderTerminal(H1);
        RenderDialogue(H1);
        RenderFx(H1, X, GO);
    }

    private void RenderJacket(float H1, float X, bool burst)
    {
        float x, y, sz, ry = 0f, rz = 0f, rx = 0f, br = 0f, op = 1f;
        float sx = discCenter.x, sy = discCenter.y, ssz = discSize;
        float hx = HoldPos.x, hy = HoldPos.y;
        if (t < 0.6f) { x = sx; y = sy; sz = ssz; br = 50f; op = 0f; }
        else if (t < H0)
        {
            float u = Eback(Seg(t, 0.6f, H0));
            x = Mathf.LerpUnclamped(sx, hx, u); y = Mathf.LerpUnclamped(sy, hy, u); sz = Mathf.LerpUnclamped(ssz, 400f, u);
            br = Mathf.Lerp(50f, 0f, Eo(Seg(t, 0.6f, 0.85f)));
            ry = Mathf.LerpUnclamped(0f, 16f, u); rz = Mathf.LerpUnclamped(0f, -2f, u);
        }
        else
        {
            float k = t - H0;
            x = hx + Mathf.Sin(k * 1.6f) * 4f; y = hy + Mathf.Sin(k * 2.1f) * 6f; sz = 400f;
            ry = 16f + Mathf.Sin(k * 1.3f) * 2f; rz = -2f;
        }
        if (t >= H1)
        {
            float u = Eio(Seg(t, H1, H1 + 0.3f));
            x = Mathf.Lerp(x, CenterPos.x, u); y = Mathf.Lerp(y, CenterPos.y, u);
            ry = Mathf.Lerp(ry, 0f, u); rz = Mathf.Lerp(rz, 0f, u);
        }

        float spin = 0f, m = 0f;
        if (t >= H1)
        {
            m = Seg(t, H1 + 0.25f, H1 + 0.5f);
            br = 50f * Eio(m);
            float sp0 = H1 + 0.4f;
            if (t > sp0) { float d = t - sp0; spin = d * d * 2400f; }
            float le = Eio(Seg(t, H1 + 0.78f, H1 + 1.12f));
            x = Mathf.Lerp(x, 800f, le); y = Mathf.Lerp(y, judgeY, le);
            rx = Mathf.Lerp(0f, 78f, le); sz = Mathf.Lerp(400f, judgeW * 0.98f, le);
            op = 1f - Seg(t, H1 + 1.05f, H1 + 1.22f);
        }
        bool show = t >= 0.6f;
        float alpha = show ? op : 0f;

        // 판 변형 (모든 판이 같은 위치·회전)
        foreach (var q in new[] { jk, rgbR, rgbB, sweep, grooves, label, sheen })
        {
            q.center = new Vector2(x, y);
            q.size = sz;
            q.rotX = rx; q.rotY = ry; q.rotZ = rz;
        }

        float radius = br / 100f; // CSS % → 0~0.5
        float spinRad = -spin * Mathf.Deg2Rad; // CSS rotate(+) = 시계방향
        jkMat.SetFloat("_Radius", radius);
        jk.color = new Color(1, 1, 1, alpha * (1f - m * m));

        // RGB 분리 (판 평면 안에서 ±6px)
        float rs = burst ? 1f : 0f;
        rgbRMat.SetFloat("_Radius", radius);
        rgbBMat.SetFloat("_Radius", radius);
        rgbR.offset = new Vector2(-6f * rs, 0f);
        rgbB.offset = new Vector2(6f * rs, 0f);
        rgbR.color = new Color(1f, 0.30f, 0.37f, alpha * rs * 0.5f);
        rgbB.color = new Color(0.35f, 0.91f, 1f, alpha * rs * 0.5f);

        // 스캔 띠 (판 위를 위에서 아래로)
        float sw = Seg(t, 0.85f, 1.35f);
        if (sw > 0f && sw < 1f && m <= 0f)
        {
            float top = Mathf.Lerp(-70f, 400f, Eio(sw)) / 400f;
            float a0 = Mathf.Clamp01(top), a1 = Mathf.Clamp01(top + 70f / 400f);
            sweep.localRect = new Rect(0, a0, 1, Mathf.Max(0.001f, a1 - a0));
            sweep.color = new Color(1, 1, 1, alpha);
        }
        else sweep.color = new Color(1, 1, 1, 0);

        // 레코드: 홈, 라벨(38%, 가운데 구멍), 고정 광택
        groovesMat.SetFloat("_Spin", spinRad);
        grooves.color = new Color(1, 1, 1, alpha * m);
        label.localRect = new Rect(0.31f, 0.31f, 0.38f, 0.38f);
        labelMat.SetFloat("_Spin", spinRad);
        label.SetMaterialDirty();
        grooves.SetMaterialDirty();
        label.color = new Color(1, 1, 1, alpha * m);
        sheen.color = new Color(1, 1, 1, alpha * m);

        foreach (var q in new[] { jk, rgbR, rgbB, sweep, grooves, label, sheen }) q.Refresh();

        // 그림자·글로우 (사각 재킷일 때)
        bool square = br < 5f && t > 0.8f;
        float glowA = square ? 0.25f * (1f - Seg(t, H1, H1 + 0.3f)) : 0f;
        Vector2 c0 = jk.Project(0.5f, 0.5f);
        float wScreen = Vector2.Distance(jk.Project(0f, 0.5f), jk.Project(1f, 0.5f));
        float hScreen = Vector2.Distance(jk.Project(0.5f, 0f), jk.Project(0.5f, 1f));
        PlaceCenter(jkShadow.rectTransform, c0.x, c0.y + 30f, wScreen + 120f, hScreen + 120f);
        jkShadow.color = new Color(0, 0, 0, square ? 0.6f * alpha : (br > 40f && t > H1 ? 0.6f * alpha : 0f));
        PlaceCenter(jkGlow.rectTransform, c0.x, c0.y, wScreen + 80f, hScreen + 80f);
        jkGlow.color = new Color(0.64f, 0.92f, 0.84f, glowA * alpha);
    }

    private readonly List<Vector2> pts = new List<Vector2>();

    private void RenderFx(float H1, float X, float GO)
    {
        shapes.Begin();
        shapesGlow.Begin();
        Color mint = new Color(0.64f, 0.92f, 0.84f);
        Color pale = new Color(0.79f, 1f, 0.94f);

        // 로딩 테두리 네온 (재킷 판 위, 12시에서 시계방향)
        float p = shownProgress;
        float ringA = (t > H0 - 0.1f ? 1f : 0f) * (1f - Seg(t, H1 + 0.15f, H1 + 0.3f));
        ringDot.color = new Color(1, 1, 1, 0);
        if (ringA > 0f && jk.color.a > 0f)
        {
            float a = ringA * (jk.color.a > 0 ? 1f : 0f);
            float i0 = 4f / 400f, i1 = 1f - i0;
            // 테두리 경로 (판 좌표 u, v)
            var path = new[] { new Vector2(0.5f, i0), new Vector2(i1, i0), new Vector2(i1, i1), new Vector2(i0, i1), new Vector2(i0, i0), new Vector2(0.5f, i0) };
            float[] lens = { 196, 392, 392, 392, 196 };
            pts.Clear();
            for (int k = 0; k < path.Length; k++) pts.Add(jk.Project(path[k].x, path[k].y));
            shapes.Polyline(pts, 2f, new Color(mint.r, mint.g, mint.b, 0.22f * a));

            Color lineC = p >= 1f ? Color.white : mint;
            float remain = 1584f * p;
            Vector2 end = pts[0];
            for (int k = 0; k < lens.Length && remain > 0f; k++)
            {
                float use = Mathf.Min(remain, lens[k]);
                Vector2 uvA = path[k], uvB = Vector2.Lerp(path[k], path[k + 1], use / lens[k]);
                Vector2 A = jk.Project(uvA.x, uvA.y), B = jk.Project(uvB.x, uvB.y);
                shapes.Line(A, B, 4f, new Color(lineC.r, lineC.g, lineC.b, a));
                shapesGlow.Line(A, B, 22f, new Color(lineC.r, lineC.g, lineC.b, 0.55f * a));
                end = B;
                remain -= use;
            }
            if (p > 0f && p < 1f)
            {
                PlaceCenter(ringDot.rectTransform, end.x, end.y, 30f, 30f);
                ringDot.color = new Color(1, 1, 1, a);
            }
        }

        // 레코드 테두리: 민트 링 2px + 글로우
        float jr = jkMat.GetFloat("_Radius");
        if (jr > 0.4f && t > H1 && grooves.color.a > 0f)
        {
            float a = grooves.color.a;
            pts.Clear();
            for (int k = 0; k <= 64; k++)
            {
                float ang = k / 64f * Mathf.PI * 2f;
                pts.Add(jk.Project(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            shapes.Polyline(pts, 2f, new Color(mint.r, mint.g, mint.b, 0.5f * a));
            shapesGlow.Polyline(pts, 36f, new Color(mint.r, mint.g, mint.b, 0.45f * a));
        }

        // 충격파 + 판정선 번쩍임
        float sk = Seg(t, H1 + 1.05f, H1 + 1.45f);
        if (sk > 0f && sk < 1f)
        {
            Color c = new Color(pale.r, pale.g, pale.b, 1f - sk);
            shapes.Ellipse(new Vector2(800f, judgeY), judgeW / 2f * (1f + sk * 0.5f), 18f + sk * 30f, 4f * (1f - sk) + 1f, c);
            shapesGlow.Ellipse(new Vector2(800f, judgeY), judgeW / 2f * (1f + sk * 0.5f), 18f + sk * 30f, 16f, new Color(pale.r, pale.g, pale.b, 0.5f * (1f - sk)));
            JudgeLine(Color.white, 1f - sk);
        }

        // 카운트인: 숫자, 링, 판정선 펄스
        string n = "";
        float cu = 0f;
        for (int i = 0; i < 3; i++)
        {
            float c = X + i * CountInterval;
            if (t >= c && t < c + CountInterval) { n = (3 - i).ToString(); cu = (t - c) / CountInterval; }
        }
        if (countMain.text != n) countMain.text = countRed.text = countCyan.text = n;
        float nA = n.Length > 0 ? 1f - Seg(cu, 0.6f, 1f) : 0f;
        float nS = n.Length > 0 ? 1.35f - 0.35f * Eo(Cl(cu * CountInterval / 0.25f)) : 1f;
        countRoot.localScale = Vector3.one * nS;
        SetAlpha(countMain, nA); SetAlpha(countRed, nA * 0.75f); SetAlpha(countCyan, nA * 0.75f);
        SetAlpha(countLabel, t >= X && t < GO ? 1f : 0f);
        if (n.Length > 0)
        {
            float k = Cl(cu * CountInterval / 0.3f);
            shapes.Ellipse(new Vector2(800f, 410f), 110f + k * 120f, 110f + k * 120f, 2f, new Color(mint.r, mint.g, mint.b, (1f - k) * 0.8f));
            shapesGlow.Ellipse(new Vector2(800f, 410f), 110f + k * 120f, 110f + k * 120f, 12f, new Color(mint.r, mint.g, mint.b, (1f - k) * 0.4f));
            JudgeLine(pale, (1f - Cl(cu * CountInterval / 0.5f)) * 0.9f);
        }

        // 첫 박: 플래시, 바닥 빛, LIVE!
        float fl = Seg(t, GO, GO + 0.45f);
        flash.color = new Color(1, 1, 1, t >= GO ? (1f - Eo(fl)) * 0.7f : 0f);
        if (t >= GO && fl < 1f && floorQuad != null)
        {
            shapes.Polygon(floorQuad, new Color(pale.r, pale.g, pale.b, 0.5f * (1f - fl)));
            liveText.fontSize = 120f + fl * 30f;
            SetAlpha(liveText, 1f - fl);
        }
        else SetAlpha(liveText, 0f);

        shapes.End();
        shapesGlow.End();
    }

    private void JudgeLine(Color c, float a)
    {
        Vector2 l = new Vector2(800f - judgeW / 2f, judgeY), r = new Vector2(800f + judgeW / 2f, judgeY);
        shapes.Line(l, r, 3f, new Color(c.r, c.g, c.b, a));
        shapesGlow.Line(l, r, 18f, new Color(c.r, c.g, c.b, 0.6f * a));
    }

    private void RenderInfo(float H1)
    {
        float iA = Seg(t, H0, H0 + 0.25f) * (1f - Seg(t, H1, H1 + 0.25f));
        infoAlpha.alpha = iA;
        float slide = (1f - Eo(Seg(t, H0, H0 + 0.3f))) * 40f + Seg(t, H1, H1 + 0.25f) * 60f;
        infoGroup.anchoredPosition = new Vector2(880f + slide, -256f);

        int nt = Mathf.FloorToInt(Cl((t - H0 - 0.1f) / 0.45f) * title.Length);
        string shown = title.Substring(0, nt);
        if (titleMain.text != shown) titleMain.text = titleRed.text = titleCyan.text = shown;
        bool cursorOn = nt < title.Length && t > H0;
        titleCursor.enabled = cursorOn && (Mathf.Repeat(t, 0.5f) < 0.3f || nt < title.Length);
        if (cursorOn) titleCursor.rectTransform.anchoredPosition = new Vector2(titleMain.preferredWidth + 4f, -34f);

        int na = Mathf.FloorToInt(Cl((t - H0 - 0.55f) / 0.35f) * artistText.Length);
        string a = artistText.Substring(0, na);
        if (artist.text != a) artist.text = a;

        for (int i = 0; i < chips.Count; i++)
        {
            float u = Seg(t, H0 + 0.75f + i * 0.08f, H0 + 0.9f + i * 0.08f);
            chipAlpha[i].alpha = u;
            var pos = chips[i].anchoredPosition;
            chips[i].anchoredPosition = new Vector2(pos.x, -154f - (1f - u) * 8f);
        }
        if (Preloader.ChartDone)
        {
            chipBpm.text = Mathf.RoundToInt(Preloader.Bpm).ToString();
            chipNotes.text = Preloader.NoteCount.ToString();
        }
        optsAlpha.alpha = Seg(t, H0 + 1.1f, H0 + 1.3f);
        bestAlpha.alpha = Seg(t, H0 + 1.2f, H0 + 1.4f);

        bool ready = shownProgress >= 1f;
        syncLabel.text = ready ? "READY" : "STAGE SYNC";
        syncLabel.color = ready ? Color.white : new Color(0.64f, 0.92f, 0.84f, 0.75f);
        syncIcon.texture = ready ? IngameV2Art.SoftDotTexture() : TransitionArt.TriangleRight();
        syncIcon.color = syncLabel.color;
    }

    private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

    private void RenderTerminal(float H1)
    {
        float tA = Seg(t, H0, H0 + 0.2f) * (1f - Seg(t, H1, H1 + 0.25f));
        termAlpha.alpha = tA;
        SetAlpha(pct, tA);
        if (tA <= 0f) return;
        float p = shownProgress;
        var pl = Preloader;
        string folder = "/songs/" + slug;
        var lines = new (float at, string head, string result, bool done)[]
        {
            (0f, "> mount  " + folder, null, pl.MountDone),
            (0.12f, "> load   audio  " + slug + ".ogg  ", pl.AudioMegabytes.ToString("0.0") + "MB OK", pl.AudioDone),
            (0.38f, "> parse  chart  " + slug + ".json  ", pl.NoteCount + " notes OK", pl.ChartDone),
            (0.62f, "> build  stage  holo_lane_06 · " + settings.stageBackgroundName + "  ", "OK", pl.SceneDone),
            (0.82f, "> apply  speed " + GameSettings.NoteSpeed.ToString("0.0") + " · offset " + (GameSettings.SyncMs >= 0 ? "+" : "") + GameSettings.SyncMs + "ms  ", "OK", pl.ApplyDone),
            (0.97f, "> stage synced. ", "READY", pl.IsDone),
        };
        sb.Length = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (p < l.at) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(l.head);
            if (l.result != null)
            {
                bool done = l.done && (p >= Mathf.Min(1f, l.at + 0.1f) || l.at >= 0.97f);
                if (done) sb.Append(l.at >= 0.97f ? "<color=#FF4D5E>" : "<color=#FFFFFF>").Append(l.result).Append("</color>");
                else sb.Append("…");
            }
        }
        string text = sb.ToString();
        if (term.text != text) term.text = text;
        pct.text = (p >= 1f ? "STAGE READY" : "SYNCING STAGE") + "  <color=#FFFFFF><size=22><i>" + Mathf.RoundToInt(p * 100f) + "%</i></size></color>";
    }

    // 「」는 지금 한글 글꼴(웹용 부분 글꼴)에 없어서 “ ”로 대신한다. 전체 Noto Sans KR로 바꾸면 「」로 돌려도 된다
    private const string Open = "“", Close = "”";

    private void RenderDialogue(float H1)
    {
        float dS = H0 + 0.45f, dE = Mathf.Max(H1 - 0.05f, dS + 1.5f);
        float dA = Seg(t, dS, dS + 0.2f) * (1f - Seg(t, dE, dE + 0.2f));
        dlgAlpha.alpha = dA;
        if (string.IsNullOrEmpty(bandLine)) { dlgAlpha.alpha = 0f; return; }
        int nl = Mathf.FloorToInt(Cl((t - dS - 0.1f) / 1.0f) * bandLine.Length);
        string s = Open + bandLine.Substring(0, nl) + (nl < bandLine.Length ? "" : Close);
        if (dlgLine.text != s) dlgLine.text = s;
    }

    // ── 도우미 ──
    private static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RawImage Raw(string name, Transform parent, Texture tex, Color color)
    {
        var r = NewRect(name, parent).gameObject.AddComponent<RawImage>();
        r.texture = tex;
        r.color = color;
        r.raycastTarget = false;
        return r;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        TextAlignmentOptions align, float spacing, bool italic = false)
    {
        return IngameV2Art.Text(name, parent, font, text, size, color, align, spacing, italic);
    }

    // 부모 왼쪽 위 기준 (시안 좌표)
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void PlaceCenter(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, -cy);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Full(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
        if (snapTexture != null) { snapTexture.Release(); Destroy(snapTexture); }
        if (blurTexture != null) { blurTexture.Release(); Destroy(blurTexture); }
        foreach (var m in new[] { jkMat, rgbRMat, rgbBMat, groovesMat, labelMat, sheenMat, additiveUI })
        {
            if (m != null) Destroy(m);
        }
    }
}
