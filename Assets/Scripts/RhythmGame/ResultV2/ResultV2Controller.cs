using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 리듬게임 결과 화면 v2 (시안 v19). RhythmResultScene_v2에서만 쓴다.
//
// 붙는 방법 (씬 파일은 고치지 않는다):
//  - RhythmResultScene_v2가 열리면 이 스크립트가 원래 결과 화면(RhythmResultSceneController, OnGUI)을 끄고
//    빈 오브젝트를 만들어 자기를 붙인다. 원래 스크립트·원본 결과 씬은 그대로다.
//  - 배경 사진, 폴라로이드(사진 + 문구 묶음)는 Resources/ResultV2Settings.asset에서 랜덤으로 고른다.
//
// 옛 최고 기록 읽기:
//  - RhythmBestRecords는 결과 씬이 열릴 때(sceneLoaded) 새 기록을 저장한다. 그 전에 옛 기록을 읽어야
//    "NEW BEST / 이전 최고 / 차이"를 보여 줄 수 있다. 그래서 시작할 때 RhythmBestRecords의 sceneLoaded 처리기를
//    우리 처리기 뒤로 옮긴다(빼고 다시 붙이기). 둘 다 SubsystemRegistration에서 붙으므로 어느 쪽이 먼저 실행돼도
//    결과적으로 우리 것이 먼저 불린다.
//
// 화면은 모두 코드로 만든다 (기준 1600 x 900, 시안 좌표 그대로).
[DisallowMultipleComponent]
public class ResultV2Controller : MonoBehaviour
{
    public const string TargetScene = "RhythmResultScene_v2";
    private const string GameplayScene = "RhythmScene_v2";

    // ── 타임라인 (초) ──
    private const float P2 = 0.65f;          // 2단계(앞쪽 화면) 시작
    private const float IntroEnd = P2 + 2.4f; // 이 전에 클릭/Enter/Space → 끝 상태로 건너뛰기
    private const float SkipTo = P2 + 3.2f;

    // ── 시작 연결 ──
    private static RhythmBestRecords.Record oldBest;
    private static bool bestHookOrdered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // RhythmBestRecords의 처리기를 우리 뒤로 옮겨, 새 기록이 저장되기 전에 옛 기록을 읽는다
        bestHookOrdered = false;
        try
        {
            MethodInfo mi = typeof(RhythmBestRecords).GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Static);
            if (mi != null)
            {
                var best = (UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(typeof(UnityAction<Scene, LoadSceneMode>), mi);
                SceneManager.sceneLoaded -= best;
                SceneManager.sceneLoaded += best;
                bestHookOrdered = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[결과 v2] 최고 기록 순서 맞추기 실패 (NEW BEST 표시가 부정확할 수 있음): " + e.Message);
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TargetScene) return;

        string song = RhythmResultContext.SongTitle;
        oldBest = string.IsNullOrEmpty(song)
            ? new RhythmBestRecords.Record { rank = "" }
            : RhythmBestRecords.Get(song, RhythmBestRecords.DefaultDifficulty);

        string songSelect = "SongSelectScene_v2", hub = "HoloHubScene";
        RhythmSceneTransition transition = null;
        var old = FindAnyObjectByType<RhythmResultSceneController>();
        if (old != null)
        {
            songSelect = ReadString(old, "songSelectSceneName", songSelect);
            hub = ReadString(old, "hubSceneName", hub);
            transition = old.GetComponent<RhythmSceneTransition>();
            // 끄면 OnGUI(화면·버튼)가 멈추고, 아직 Start 전이라 Start(페이드)도 불리지 않는다
            old.enabled = false;
        }
        else
        {
            transition = FindAnyObjectByType<RhythmSceneTransition>();
        }

        // 곡 끝 → 결과 전환 연출 A(레코드 착지)로 들어왔으면: 옛 검은 화면(OnGUI)은 오버레이를 덮으므로 바로 걷고,
        // 재킷은 노이즈 → 팝 대신 "착지" 방식으로 켠다
        var landing = ResultTransitionOverlay.Active;
        if (landing != null && transition != null) transition.HideImmediately();

        var go = new GameObject("Result V2 (시안 v19)");
        SceneManager.MoveGameObjectToScene(go, scene);
        var c = go.AddComponent<ResultV2Controller>();
        c.songSelectSceneName = songSelect;
        c.hubSceneName = hub;
        c.transition = transition;
        c.landingOverlay = landing;
        c.landingMode = landing != null;
    }

    private static string ReadString(object target, string field, string fallback)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        string value = f != null ? f.GetValue(target) as string : null;
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    // ── 설정·데이터 ──
    private string songSelectSceneName = "SongSelectScene_v2";
    private string hubSceneName = "HoloHubScene";
    private RhythmSceneTransition transition;

    private ResultV2Settings settings;
    private TMP_FontAsset fNem, fOx, fOxB, fNum, fKr, fNoto;

    private PlayResult result;
    private string title = "NO TRACK";
    private bool story;
    private Texture2D cover;
    private float bpm, length;
    private int track;
    private string songName;      // 곡 목록 이름 (다시하기용)
    private bool canRetry;
    private string diffName = "NORMAL";
    private int diffLevel;
    private string rank = "D";
    private float accuracy;
    private bool isNewBest, hasOldBest;
    private int finalScore, bestShown, delta;
    private bool fullCombo;

    // ── 화면 ──
    private RectTransform canvasRect, decorStage, fgStage;
    private RawImage scanOverlay;

    private class DecorPart { public Graphic g; public RawImage raw; public float w; public Rect uv; }
    private class Decor { public RectTransform frame; public float delay; public readonly List<DecorPart> parts = new List<DecorPart>(); public CanvasGroup fade; }
    private readonly List<Decor> decors = new List<Decor>();

    private class Marquee { public RectTransform rt; public float unit; public float speed; public int dir; public float baseX; }
    private readonly List<Marquee> marquees = new List<Marquee>();
    private RectTransform watermark, ghost;
    private CanvasGroup watermarkGroup, ghostGroup;
    private Marquee watermarkMq, ghostMq;

    private class Group { public RectTransform rt; public CanvasGroup cg; public float at; public Vector2 basePos; }
    private readonly List<Group> groups = new List<Group>();
    private CanvasGroup sigGroup;
    private CanvasGroup buttonsGroup;

    // 재킷
    private RectTransform jacketRoot;
    private RawImage jacketImg, noiseImg, tintImg, scanImg;
    private Image jacketBright, jacketWash, jacketGlow;
    private readonly List<Graphic> jacketFrame = new List<Graphic>();
    private readonly List<RawImage> tears = new List<RawImage>();
    private float tearTimer;
    private ResultV2Noise noise;
    private RawImage beamGlow;
    private RectTransform emitter;
    private AudioSource sfx;
    private bool popped;
    // 재킷 그림: 곡 재킷, 없으면 전환 연출과 같은 noCover 그림 (착지 순간 이음새가 안 보이게 같은 그림·같은 크롭)
    private Texture2D jacketTex;

    // 곡 끝 → 결과 전환 A(레코드 착지)로 들어온 경우
    private ResultTransitionOverlay landingOverlay;
    private bool landingMode;
    private float landR = ResultTransitionOverlay.LandT - ResultTransitionOverlay.R0; // 결과 시간 기준 착지 순간 (기본 0.6)

    // 점수
    private TextMeshProUGUI scoreText, scoreRed, scoreMint;
    private RectTransform scoreRoot, scoreBar, shine;
    private Image scoreFlash;
    private CanvasGroup bestRow;

    // 판정 줄
    private class Row
    {
        public RectTransform root;
        public CanvasGroup cg;
        public Image bright;
        public RawImage scan;
        public TextMeshProUGUI count;
        public int value;
        public float delay;
        public int kind;
    }
    private readonly Row[] rows = new Row[4];
    private TextMeshProUGUI comboValue, accValue;

    // 랭크·폴라로이드
    private RectTransform rankRoot;
    private CanvasGroup rankGroup;
    private RectTransform polRoot;
    private CanvasGroup polGroup;
    private float polBaseY;
    private TextMeshProUGUI caption;
    private string captionText = "";
    private bool hasPolaroid;

    // 손글씨 타이핑
    private int typed;          // 다 쓴 글자 수
    private int glyphFrames;    // 지금 글자 앞에 보여 줄 가짜 글자 수
    private char glyph;
    private float nextTypeAt = -1f;
    private bool typingDone;
    private const string GlitchGlyphs = "#%&@/\\<>_=+*01ABXZ";

    // 진행
    private float t;
    private bool skipped, leaving;

    private void Start()
    {
        settings = ResultV2Settings.Load();
        LoadData();
        MakeFonts();
        Build();
        // 착지 방식이면 검은 화면은 오버레이가 걷는다 (옛 페이드는 쓰지 않음)
        if (transition != null && !landingMode) StartCoroutine(transition.RevealScene());
        Render(0f);
    }

    private void OnDestroy()
    {
        ResultV2Fx.ReleaseAll();
    }

    // ───────────────────────── 데이터 ─────────────────────────
    private void LoadData()
    {
        result = RhythmResultContext.Result;
        title = RhythmResultContext.SongTitle;
        story = RhythmResultContext.Mode == RhythmEntryMode.Story;

        bool preview = result == null;
        if (preview)
        {
            // 결과 씬을 바로 Play한 경우: 화면 확인용 예시 값
            result = new PlayResult { score = 559900, maxCombo = 188, perfectCount = 412, greatCount = 38, goodCount = 6, missCount = 2, isFullCombo = false };
        }

        if (RhythmLaunchExtrasV2.HasValue)
        {
            diffName = RhythmLaunchExtrasV2.Difficulty;
            diffLevel = RhythmLaunchExtrasV2.Level;
        }

        LookupSong(preview);
        if (string.IsNullOrWhiteSpace(title)) title = "NO TRACK";

        int total = result.perfectCount + result.greatCount + result.goodCount + result.missCount;
        accuracy = total > 0 ? (result.perfectCount + result.greatCount * 0.7f + result.goodCount * 0.4f) / total : 0f;
        rank = RhythmBestRecords.RankOf(result);
        finalScore = Mathf.Max(0, result.score);
        fullCombo = result.isFullCombo && total > 0;

        if (preview)
        {
            hasOldBest = true;
            bestShown = 528800;
        }
        else if (bestHookOrdered)
        {
            hasOldBest = oldBest.HasRecord;
            bestShown = oldBest.score;
        }
        else
        {
            // 순서를 못 맞췄으면 저장된 값으로 추측 (같은 점수면 이번이 첫 기록으로 본다)
            var stored = RhythmBestRecords.Get(RhythmResultContext.SongTitle ?? "", RhythmBestRecords.DefaultDifficulty);
            hasOldBest = stored.HasRecord && stored.score > finalScore;
            bestShown = stored.score;
        }
        isNewBest = !hasOldBest || finalScore > bestShown;
        delta = finalScore - bestShown;
    }

    // 곡 목록(ChartAutoLoader)에서 재킷·BPM·길이·트랙 번호를 찾는다
    private void LookupSong(bool preview)
    {
        var loader = gameObject.AddComponent<ChartAutoLoader>();
        try
        {
            List<ChartAutoLoader.SongEntry> songs = loader.GetAvailableSongs();
            int index = -1;
            ChartData chart;
            if (preview && songs.Count > 0) index = 0;
            for (int i = 0; index < 0 && i < songs.Count; i++)
                if (string.Equals(songs[i].DisplayName, title, StringComparison.OrdinalIgnoreCase)) index = i;
            for (int i = 0; index < 0 && i < songs.Count; i++)
            {
                if (!songs[i].HasChart) continue;
                if (loader.TryLoadSong(songs[i], out chart) && chart != null
                    && string.Equals(chart.title, title, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                }
            }
            if (index < 0) return;

            var song = songs[index];
            cover = song.albumCover;
            length = song.audioClip != null ? song.audioClip.length : 0f;
            track = index + 1;
            songName = song.DisplayName;
            canRetry = !string.IsNullOrEmpty(songName) && Application.CanStreamedLevelBeLoaded(GameplayScene);
            if (preview) title = song.DisplayName;
            if (loader.TryLoadSong(song, out chart) && chart != null) bpm = chart.SafeBpm;
        }
        finally
        {
            Destroy(loader);
        }
    }

    private void MakeFonts()
    {
        if (settings.koreanFallback) IngameV2Art.SetKoreanFallback(settings.koreanFallback);
        fNoto = IngameV2Art.MakeFont(settings.koreanFallback, null);
        fOx = IngameV2Art.MakeFont(settings.oxaniumSemiBold, fNoto);
        fOxB = IngameV2Art.MakeFont(settings.oxaniumBold != null ? settings.oxaniumBold : settings.oxaniumSemiBold, fOx);
        fNum = IngameV2Art.MakeFont(settings.numberFont, fOxB);
        fNem = IngameV2Art.MakeFont(settings.displayFont, fOxB);
        // NEMESYS에 없는 글자(기호 등)는 Oxanium으로
        if (fNem != null && fOxB != null && fNem != fOxB)
        {
            if (fNem.fallbackFontAssetTable == null) fNem.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!fNem.fallbackFontAssetTable.Contains(fOxB)) fNem.fallbackFontAssetTable.Insert(0, fOxB);
        }
        fKr = settings.koreanTitle != null ? settings.koreanTitle : fNoto;
    }

    // ───────────────────────── 만들기 ─────────────────────────
    private void Build()
    {
        if (Camera.main == null && FindAnyObjectByType<Camera>() == null)
        {
            var cam = new GameObject("Result Camera", typeof(Camera)).GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ResultV2Fx.Bg;
            cam.cullingMask = 0;
        }
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var go = new GameObject("Result V2 Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // 16:10에서도 잘리지 않게
        canvasRect = (RectTransform)go.transform;

        BuildBackground();
        decorStage = MakeStage("Decor");
        BuildDecor();
        BuildWatermark();
        fgStage = MakeStage("Foreground");
        BuildTopBar();
        BuildJacket();
        BuildScore();
        BuildRows();
        BuildRank();
        BuildPolaroid();
        BuildButtons();

        scanOverlay = ResultV2Fx.Raw("Scanlines", canvasRect, ResultV2Fx.TileY(3, 1, Color.white), new Color(1f, 1f, 1f, 0.024f), 0, 0, 0, 0);
        IngameV2Art.Stretch(scanOverlay.rectTransform);

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
    }

    private RectTransform MakeStage(string name)
    {
        var rt = IngameV2Art.NewRect(name, canvasRect);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1600, 900);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    // 1. 배경: 흐리고 어두운 사진 + 민트 빛
    private void BuildBackground()
    {
        var solid = IngameV2Art.Img("Bg Solid", canvasRect, null, ResultV2Fx.Bg);
        IngameV2Art.Stretch(solid.rectTransform);

        Sprite bgSprite = settings.PickBackground();
        Texture source = bgSprite != null ? bgSprite.texture : (Texture)cover;
        float aspect = bgSprite != null ? bgSprite.rect.width / Mathf.Max(1f, bgSprite.rect.height)
            : (cover != null ? cover.width / (float)Mathf.Max(1, cover.height) : 16f / 9f);
        if (source != null)
        {
            var holder = IngameV2Art.NewRect("Bg Holder", canvasRect);
            IngameV2Art.Stretch(holder);
            holder.offsetMin = new Vector2(-30, -30);   // 가장자리 30px 밖까지
            holder.offsetMax = new Vector2(30, 30);
            var photo = IngameV2Art.NewRect("Bg Photo", holder).gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false;
            photo.texture = Blurred(source);
            photo.color = new Color(0.38f, 0.38f, 0.38f, 1f);
            IngameV2Art.Stretch(photo.rectTransform);
            var fit = photo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = aspect;
        }

        var stage = MakeStage("Bg Glow Stage");
        var glow = ResultV2Fx.Img("Mint Glow", stage, IngameV2Art.SoftDot(), new Color(40 / 255f, 140 / 255f, 120 / 255f, 0.18f), 0, 0, 0, 0);
        IngameV2Art.PlaceCenter(glow.rectTransform, 480, 450, 1500, 1150);
    }

    // 작은 그림으로 여러 번 줄였다가 늘려서 흐리게 만든다 (블러 대신)
    private Texture Blurred(Texture source)
    {
        int w = source.width, h = source.height;
        Texture current = source;
        while (w > 128)
        {
            w = Mathf.Max(1, w / 2);
            h = Mathf.Max(1, h / 2);
            var rt = ResultV2Fx.Keep(new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32));
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(current, rt);
            current = rt;
        }
        return current;
    }

    // 2. 배경 장식 (모두 -15도, 왼쪽 → 오른쪽으로 쓸며 나타남)
    private void BuildDecor()
    {
        Color mint = ResultV2Fx.Mint;
        Texture2D white = Texture2D.whiteTexture;
        Texture2D dash = ResultV2Fx.TileX(6, 3, Color.white);
        Texture2D stripes = ResultV2Fx.TileX(14, 3, Color.white);

        var slab = NewDecor("Slab", 520, -330, 1700, 300, 0.05f);
        AddPart(slab, ResultV2Fx.GradientH((0f, Color.clear), (0.3f, ResultV2Fx.A(mint, 0.07f)), (1f, ResultV2Fx.A(mint, 0.12f))), Color.white, 0, 0, 1700, 300);
        AddPart(slab, white, ResultV2Fx.A(mint, 0.35f), 0, 298, 1700, 2);

        var slab2 = NewDecor("Slab 2", -400, 900, 1500, 260, 0.22f);
        AddPart(slab2, ResultV2Fx.GradientH((0f, new Color(8 / 255f, 28 / 255f, 26 / 255f, 0.85f)), (1f, ResultV2Fx.A(mint, 0.06f))), Color.white, 0, 0, 1500, 260);
        AddPart(slab2, white, ResultV2Fx.A(mint, 0.3f), 0, 0, 1500, 2);

        var rails = NewDecor("Rails", -200, 300, 2300, 34, 0.12f);
        AddPart(rails, white, ResultV2Fx.A(mint, 0.25f), 0, 0, 2300, 1);
        AddPart(rails, dash, ResultV2Fx.A(mint, 0.2f), 0, 33, 2300, 1, true);

        var hatch = NewDecor("Hatch", 300, 812, 760, 22, 0.32f);
        AddPart(hatch, stripes, ResultV2Fx.A(mint, 0.35f), 0, 0, 760, 22, true);
        var hatch2 = NewDecor("Hatch 2", 1050, 70, 520, 14, 0.2f);
        AddPart(hatch2, stripes, ResultV2Fx.A(mint, 0.35f * 0.7f), 0, 0, 520, 14, true);

        var chip1 = NewDecor("Chip 1", 40, 705, 180, 10, 0.4f);
        AddPart(chip1, white, ResultV2Fx.A(mint, 0.7f), 0, 0, 180, 10);
        var chip2 = NewDecor("Chip 2", 1380, 388, 110, 8, 0.36f);
        AddPart(chip2, white, ResultV2Fx.A(mint, 0.5f), 0, 0, 110, 8);

        var l1 = NewDecor("Line 1", -300, 180, 1500, 1, 0.08f);
        AddPart(l1, white, ResultV2Fx.A(mint, 0.22f), 0, 0, 1500, 1);
        var l2 = NewDecor("Line 2", 200, 610, 2000, 1, 0.3f);
        AddPart(l2, dash, ResultV2Fx.A(mint, 0.28f), 0, 0, 2000, 1, true);
        var l3 = NewDecor("Line 3", -200, 860, 1200, 3, 0.16f);
        AddPart(l3, white, ResultV2Fx.A(mint, 0.3f), 0, 0, 1200, 1);
        AddPart(l3, white, ResultV2Fx.A(mint, 0.3f), 0, 2, 1200, 1);
        var l4 = NewDecor("Line 4", 900, 520, 1100, 2, 0.24f);
        AddPart(l4, white, ResultV2Fx.A(mint, 0.18f), 0, 0, 1100, 2);
        var l5 = NewDecor("Line 5", -100, 420, 700, 1, 0.34f);
        AddPart(l5, white, new Color(239 / 255f, 1f, 250 / 255f, 0.3f), 0, 0, 700, 1);

        // 흐르는 글씨 1: 곡 정보 (오른쪽으로)
        string info = diffName + (diffLevel > 0 ? " " + diffLevel : "") + " // "
            + (bpm > 0 ? bpm.ToString("0.#", CultureInfo.InvariantCulture) + " BPM // " : "")
            + (length > 0 ? Clock(length) + " // " : "")
            + (track > 0 ? "TRACK " + track.ToString("00") + " // " : RhythmGameUIV2.PrettyTitle(title).ToUpperInvariant() + " // ");
        AddMarquee("Strip 1", -260, 560, 22, info, fOx, 15, ResultV2Fx.A(mint, 0.3f), 30f, 50f, 1, 0.14f, null);

        // 흐르는 글씨 2: 테두리만 있는 큰 글씨 (왼쪽으로)
        string mode = story ? "STORY MODE" : "SINGLE PLAY";
        AddMarquee("Strip 2", -420, 780, 56, mode + " · SESSION REPORT · RE:WRITE · ", fNem, 46, Color.white, 4f, 60f, -1, 0.28f,
            ResultV2Fx.OutlineMaterial(fNem, ResultV2Fx.A(mint, 0.2f), 0.2f));

        // 고정 글씨 3
        var t3 = NewDecor("Strip 3", 120, 770, 900, 18, 0.42f);
        t3.fade = t3.frame.gameObject.AddComponent<CanvasGroup>();
        ResultV2Fx.Text("Text", t3.frame, fOxB, FormatScore(finalScore) + " // " + rank + " // " + result.maxCombo + " COMBO", 13,
            ResultV2Fx.A(mint, 0.3f), TextAlignmentOptions.MidlineLeft, 0, 0, 900, 18, 30f);

        var band = NewDecor("Diag Band", -300, 430, 2400, 260, 0.18f);
        AddPart(band, ResultV2Fx.GradientV((0f, ResultV2Fx.A(mint, 0.05f)), (0.35f, ResultV2Fx.A(mint, 0.2f)), (0.6f, ResultV2Fx.A(mint, 0.28f)), (0.95f, ResultV2Fx.A(mint, 0.08f)), (1f, ResultV2Fx.A(mint, 0.08f))),
            Color.white, 0, 0, 2400, 260);
        AddPart(band, white, ResultV2Fx.A(mint, 0.5f), 0, 0, 2400, 2);
        AddPart(band, white, ResultV2Fx.A(mint, 0.12f), 0, 259, 2400, 1);

        var thin = NewDecor("Diag Thin", -300, 720, 2400, 6, 0.26f);
        AddPart(thin, white, ResultV2Fx.A(mint, 0.35f), 0, 0, 2400, 6);
    }

    private Decor NewDecor(string name, float x, float y, float w, float h, float delay)
    {
        var frame = IngameV2Art.NewRect(name, decorStage);
        IngameV2Art.PlaceCenter(frame, x + w * 0.5f, y + h * 0.5f, w, h);
        frame.localEulerAngles = new Vector3(0f, 0f, 15f); // CSS rotate(-15deg)
        var d = new Decor { frame = frame, delay = delay };
        decors.Add(d);
        return d;
    }

    private void AddPart(Decor d, Texture tex, Color color, float x, float y, float w, float h, bool tiled = false)
    {
        var raw = ResultV2Fx.Raw("Part", d.frame, tex, color, x, y, w, h);
        Rect uv = tiled ? new Rect(0, 0, w / tex.width, h / tex.height) : new Rect(0, 0, 1, 1);
        raw.uvRect = uv;
        d.parts.Add(new DecorPart { g = raw, raw = raw, w = w, uv = uv });
    }

    private Marquee AddMarquee(string name, float x, float y, float h, string unit, TMP_FontAsset font, float size, Color color,
        float spacing, float period, int dir, float delay, Material mat)
    {
        var d = NewDecor(name, x, y, 2400, h, delay);
        d.fade = d.frame.gameObject.AddComponent<CanvasGroup>();
        var text = ResultV2Fx.Text("Text", d.frame, font, unit, size, color, TextAlignmentOptions.MidlineLeft, 0, 0, 2400, h, spacing);
        if (mat != null) text.fontSharedMaterial = mat;
        int repeats;
        float unitW = ResultV2Fx.FillRepeated(text, unit, 2400f * 2f, out repeats);
        text.rectTransform.sizeDelta = new Vector2(unitW * repeats, h);
        var m = new Marquee { rt = text.rectTransform, unit = unitW, speed = unitW * repeats * 0.5f / period, dir = dir, baseX = 0f };
        marquees.Add(m);
        return m;
    }

    // 3. 워터마크: 테두리만 있는 아주 큰 글씨가 계속 왼쪽으로 흐른다
    private void BuildWatermark()
    {
        string unit = "RESULT · " + RhythmGameUIV2.PrettyTitle(title).ToUpperInvariant() + " · ";
        watermark = MakeWatermarkText("Watermark", unit, ResultV2Fx.A(ResultV2Fx.Mint, 0.2f), out watermarkMq);
        watermarkGroup = watermark.gameObject.AddComponent<CanvasGroup>();
        ghost = MakeWatermarkText("Watermark Ghost", unit, ResultV2Fx.A(ResultV2Fx.RedGhost, 0.55f), out ghostMq);
        ghostGroup = ghost.gameObject.AddComponent<CanvasGroup>();
        ghostGroup.alpha = 0f;
        ghost.SetSiblingIndex(watermark.GetSiblingIndex());
    }

    private RectTransform MakeWatermarkText(string name, string unit, Color stroke, out Marquee mq)
    {
        var holder = IngameV2Art.NewRect(name, decorStage);
        holder.anchorMin = holder.anchorMax = new Vector2(0f, 1f);
        holder.pivot = new Vector2(0f, 1f);
        holder.anchoredPosition = new Vector2(-200f, -760f);
        holder.sizeDelta = new Vector2(2400f, 340f);
        holder.localEulerAngles = new Vector3(0f, 0f, 15f);
        var text = ResultV2Fx.Text("Text", holder, fNum, unit, 330, Color.white, TextAlignmentOptions.TopLeft, 0, 0, 2400, 340, 0f, true);
        text.fontSharedMaterial = ResultV2Fx.OutlineMaterial(fNum, stroke, 0.07f);
        int repeats;
        float unitW = ResultV2Fx.FillRepeated(text, unit, 2400f * 2f, out repeats);
        text.rectTransform.sizeDelta = new Vector2(unitW * repeats, 340f);
        mq = new Marquee { rt = text.rectTransform, unit = unitW, speed = unitW * repeats * 0.5f / 46f, dir = -1 };
        return holder;
    }

    private Group NewGroup(string name, float at)
    {
        var rt = IngameV2Art.NewRect(name, fgStage);
        IngameV2Art.Stretch(rt);
        var g = new Group { rt = rt, cg = rt.gameObject.AddComponent<CanvasGroup>(), at = at, basePos = Vector2.zero };
        g.cg.alpha = 0f;
        groups.Add(g);
        return g;
    }

    // 4-1. 위 막대, 제목
    private void BuildTopBar()
    {
        var top = NewGroup("Top Bar", P2).rt;
        ResultV2Fx.Img("Bottom Line", top, null, ResultV2Fx.Line, 90, 104, 1420, 1);
        int[] heights = { 10, 20, 14, 24, 8 };
        for (int i = 0; i < heights.Length; i++)
            ResultV2Fx.Img("Logo Bar " + i, top, null, Color.white, 90 + i * 6, 76 - heights[i] * 0.5f, 3, heights[i]);
        ResultV2Fx.Text("Logo", top, fNem, "RE:WRITE", 26, Color.white, TextAlignmentOptions.MidlineLeft, 124, 56, 200, 40);
        ResultV2Fx.Img("Separator", top, null, ResultV2Fx.Line, 331, 63, 1, 26);
        ResultV2Fx.Text("Session", top, fOx, "SESSION REPORT", 14, ResultV2Fx.MintHi, TextAlignmentOptions.MidlineLeft, 350, 56, 300, 40, 14f);

        // 오른쪽 칩: 배속, 싱크
        float right = 1510f;
        string[] chips = { "싱크 " + (GameSettings.SyncMs >= 0 ? "+" : "") + GameSettings.SyncMs + "ms", "배속 " + GameSettings.NoteSpeed.ToString("0.0", CultureInfo.InvariantCulture) };
        for (int i = 0; i < chips.Length; i++)
        {
            var text = ResultV2Fx.Text("Chip Text", top, fKr, chips[i], 14, ResultV2Fx.Fg, TextAlignmentOptions.Center, 0, 0, 10, 36);
            float w = ResultV2Fx.Width(text) + 28f;
            float x = right - w;
            Box("Chip", top, x, 58, w, 36, ResultV2Fx.Panel, ResultV2Fx.Line);
            IngameV2Art.Place(text.rectTransform, x, 58, w, 36);
            text.transform.SetAsLastSibling();
            right = x - 8f;
        }

        var head = NewGroup("Title", P2 + 0.15f).rt;
        ResultV2Fx.Text("Title", head, fKr, "결과", 44, Color.white, TextAlignmentOptions.BottomLeft, 90, 124, 100, 60);
        string sub = story ? "STORY MODE" : "SINGLE PLAY";
        if (track > 0) sub += " · TRACK " + track.ToString("00");
        ResultV2Fx.Text("Mode", head, fOx, sub, 15, ResultV2Fx.MintHi, TextAlignmentOptions.BottomLeft, 182, 160, 600, 22, 10f);
    }

    // 테두리 있는 상자
    private void Box(string name, Transform parent, float x, float y, float w, float h, Color fill, Color line)
    {
        ResultV2Fx.Img(name, parent, null, fill, x, y, w, h);
        ResultV2Fx.Img(name + " T", parent, null, line, x, y, w, 1);
        ResultV2Fx.Img(name + " B", parent, null, line, x, y + h - 1, w, 1);
        ResultV2Fx.Img(name + " L", parent, null, line, x, y, 1, h);
        ResultV2Fx.Img(name + " R", parent, null, line, x + w - 1, y, 1, h);
    }

    // 4-2. 재킷 (지직 노이즈 → 팍 켜짐)
    private void BuildJacket()
    {
        const float x = 96, y = 200, s = 380;
        // 아래쪽 빛 + 빛을 내는 막대
        beamGlow = ResultV2Fx.Raw("Beam Glow", fgStage, ResultV2Fx.GradientV((0f, ResultV2Fx.A(ResultV2Fx.Mint, 0f)), (1f, ResultV2Fx.A(ResultV2Fx.Mint, 0.28f))),
            Color.white, x, 432, s, 160);
        emitter = IngameV2Art.NewRect("Emitter", fgStage);
        IngameV2Art.PlaceCenter(emitter, 86 + 200, 592, 400, 4);
        var emGlow = IngameV2Art.Img("Glow", emitter, IngameV2Art.SoftBar(), ResultV2Fx.A(ResultV2Fx.Mint, 0.8f));
        IngameV2Art.PlaceCenter(emGlow.rectTransform, 200, 2, 420, 30);
        emGlow.rectTransform.anchorMin = emGlow.rectTransform.anchorMax = new Vector2(0f, 1f);
        var emBar = IngameV2Art.Img("Bar", emitter, null, ResultV2Fx.MintHi);
        IngameV2Art.Stretch(emBar.rectTransform);

        jacketRoot = IngameV2Art.NewRect("Jacket", fgStage);
        IngameV2Art.PlaceCenter(jacketRoot, x + s * 0.5f, y + s * 0.5f, s, s);

        jacketGlow = ResultV2Fx.Img("Glow", jacketRoot, ResultV2Fx.SoftRect(26), ResultV2Fx.A(ResultV2Fx.Mint, 0.35f), -26, -26, s + 52, s + 52);
        jacketGlow.type = Image.Type.Sliced;
        jacketFrame.Add(jacketGlow);

        // 재킷이 없는 곡은 전환 연출과 같은 noCover 그림 (그것도 없으면 예전 "NO COVER" 판)
        jacketTex = cover != null ? cover : SongTransitionSettings.Load().noCover;
        jacketImg = ResultV2Fx.Raw("Cover", jacketRoot, jacketTex, Color.white, 0, 0, s, s);
        if (jacketTex == null)
        {
            jacketImg.color = ResultV2Fx.DarkPanel;
            var noCover = ResultV2Fx.Text("No Cover", jacketRoot, fOxB, "NO COVER", 22, ResultV2Fx.Dim, TextAlignmentOptions.Center, 0, 0, s, s, 20f);
            noCover.transform.SetParent(jacketImg.transform, true);
        }
        else
        {
            // 정사각형으로 가운데를 잘라 쓴다
            float a = jacketTex.width / (float)Mathf.Max(1, jacketTex.height);
            jacketImg.uvRect = a > 1f ? new Rect((1f - 1f / a) * 0.5f, 0f, 1f / a, 1f) : new Rect(0f, (1f - a) * 0.5f, 1f, a);
        }

        // 색 번짐 조각 (빨강·시안) 3개씩
        for (int i = 0; i < 6; i++)
        {
            var tear = ResultV2Fx.Raw("Tear " + i, jacketRoot, jacketTex,i < 3 ? new Color(1f, 0.35f, 0.45f, 0.55f) : new Color(0.35f, 1f, 1f, 0.5f), 0, 0, s, 20);
            tear.gameObject.SetActive(false);
            tears.Add(tear);
        }

        jacketBright = ResultV2Fx.Img("Bright", jacketRoot, null, new Color(1f, 1f, 1f, 0f), 0, 0, s, s);
        jacketWash = ResultV2Fx.Img("Wash", jacketRoot, null, ResultV2Fx.A(IngameV2Art.Hex(0xD4F7EC), 0f), 0, 0, s, s);
        tintImg = ResultV2Fx.Raw("Tint", jacketRoot, ResultV2Fx.GradientV((0f, ResultV2Fx.A(ResultV2Fx.Mint, 0.08f)), (1f, ResultV2Fx.A(ResultV2Fx.Mint, 0.28f))), Color.white, 0, 0, s, s);
        scanImg = ResultV2Fx.Tiled("Scan", jacketRoot, ResultV2Fx.TileY(4, 2, Color.white), new Color(0f, 0f, 0f, 0.22f), 0, 0, s, s);

        // 테두리 2px
        jacketFrame.Add(ResultV2Fx.Img("Edge T", jacketRoot, null, ResultV2Fx.A(ResultV2Fx.Mint, 0.9f), -2, -2, s + 4, 2));
        jacketFrame.Add(ResultV2Fx.Img("Edge B", jacketRoot, null, ResultV2Fx.A(ResultV2Fx.Mint, 0.9f), -2, s, s + 4, 2));
        jacketFrame.Add(ResultV2Fx.Img("Edge L", jacketRoot, null, ResultV2Fx.A(ResultV2Fx.Mint, 0.9f), -2, 0, 2, s));
        jacketFrame.Add(ResultV2Fx.Img("Edge R", jacketRoot, null, ResultV2Fx.A(ResultV2Fx.Mint, 0.9f), s, 0, 2, s));

        noise = new ResultV2Noise();
        noiseImg = ResultV2Fx.Raw("Noise", jacketRoot, noise.Texture, Color.white, 0, 0, s, s);

        // 메타 줄 + SIG 글씨
        var meta = NewGroup("Meta", P2 + 0.3f).rt;
        var mt = ResultV2Fx.Text("Meta Title", meta, fOx, RhythmGameUIV2.PrettyTitle(title), 15, Color.white, TextAlignmentOptions.MidlineLeft, 96, 616, 170, 22, 12f);
        mt.overflowMode = TextOverflowModes.Ellipsis;
        ResultV2Fx.Text("Meta BPM", meta, fOx, bpm > 0 ? bpm.ToString("0.#", CultureInfo.InvariantCulture) + " BPM" : "-- BPM", 15, Color.white, TextAlignmentOptions.Center, 266, 616, 120, 22, 12f);
        ResultV2Fx.Text("Meta Time", meta, fOx, length > 0 ? Clock(length) : "--:--", 15, Color.white, TextAlignmentOptions.MidlineRight, 386, 616, 90, 22, 12f);

        var sig = IngameV2Art.NewRect("SIG", fgStage);
        IngameV2Art.Stretch(sig);
        sigGroup = sig.gameObject.AddComponent<CanvasGroup>();
        sigGroup.alpha = 0f;
        var l1 = ResultV2Fx.Text("SIG 1", sig, fOx, "SIG", 12, ResultV2Fx.Fg, TextAlignmentOptions.MidlineLeft, 96, 654, 200, 16, 10f);
        float sx = 96 + ResultV2Fx.Width(l1) + 8;
        for (int i = 0; i < 5; i++) ResultV2Fx.Img("Cell " + i, sig, null, ResultV2Fx.A(ResultV2Fx.Fg, i < 4 ? 0.9f : 0.3f), sx + i * 10, 658, 7, 7);
        ResultV2Fx.Text("SIG 2", sig, fOx, "FREQ 48k", 12, ResultV2Fx.Fg, TextAlignmentOptions.MidlineLeft, 96, 671, 200, 16, 10f);
        ResultV2Fx.Text("SIG 3", sig, fOx, "CH 2 · STEREO", 12, ResultV2Fx.Fg, TextAlignmentOptions.MidlineLeft, 96, 688, 200, 16, 10f);
    }

    // 4-3. 점수
    private void BuildScore()
    {
        var g = NewGroup("Score Block", P2 + 0.3f).rt;
        var label = ResultV2Fx.Text("Label", g, fOx, "TOTAL SCORE · ", 13, ResultV2Fx.Dim, TextAlignmentOptions.MidlineLeft, 520, 176, 300, 20, 14f);
        float tx = 520 + ResultV2Fx.Width(label) + 4;
        string tag = diffName + (diffLevel > 0 ? " " + diffLevel : "");
        Color tagColor = diffName == "NORMAL" ? ResultV2Fx.NormalDiff : IngameV2Art.DifficultyColor(diffName);
        Tag(g, tx, 174, tag, tagColor, IngameV2Art.Hex(0x06202B), 6f);

        scoreRoot = IngameV2Art.NewRect("Score", g);
        IngameV2Art.Place(scoreRoot, 520, 196, 760, 160);
        scoreFlash = ResultV2Fx.Img("Flash", scoreRoot, ResultV2Fx.SoftRect(40), ResultV2Fx.A(ResultV2Fx.Mint, 0f), -40, -10, 700, 180);
        scoreFlash.type = Image.Type.Sliced;
        scoreMint = ResultV2Fx.Text("Mint", scoreRoot, fNum, "0", 160, ResultV2Fx.A(ResultV2Fx.Mint, 0.75f), TextAlignmentOptions.MidlineLeft, -4, 0, 760, 160, 0f, true);
        scoreRed = ResultV2Fx.Text("Red", scoreRoot, fNum, "0", 160, new Color(224 / 255f, 106 / 255f, 134 / 255f, 0.45f), TextAlignmentOptions.MidlineLeft, 4, 0, 760, 160, 0f, true);
        scoreText = ResultV2Fx.Text("Number", scoreRoot, fNum, "0", 160, ResultV2Fx.Perfect, TextAlignmentOptions.MidlineLeft, 0, 0, 760, 160, 0f, true);
        IngameV2Art.Glow(scoreText, ResultV2Fx.A(ResultV2Fx.Mint, 0.35f), 0.7f);
        scoreRoot.pivot = new Vector2(0.15f, 0.5f);
        scoreRoot.anchoredPosition = new Vector2(520 + 760 * 0.15f, -(196 + 80));

        // 반짝이는 띠 (숫자 위를 지나간다)
        var shineTex = ResultV2Fx.Paint(90, 150, (x, y) =>
        {
            float shift = (0.5f - y / 150f) * 150f * 0.364f; // skewX(-20deg)
            float d = Mathf.Abs(x - 45f - shift) / 45f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a * 0.9f);
        });
        shine = ResultV2Fx.Raw("Shine", scoreRoot, shineTex, new Color(1f, 1f, 1f, 0f), 0, 5, 90, 150).rectTransform;

        // 아래 기울어진 막대
        var barTex = ResultV2Fx.Paint(645, 8, (x, y) =>
        {
            float inside = ResultV2Fx.SkewInside(x, y, 640, 8, 4.6f);
            return ResultV2Fx.A(ResultV2Fx.Mint, inside * Mathf.Lerp(1f, 0f, x / 645f));
        });
        scoreBar = ResultV2Fx.Raw("Bar", g, barTex, Color.white, 520, 352, 645, 8).rectTransform;

        // 최고 기록 줄
        var rowRt = IngameV2Art.NewRect("Best Row", g);
        IngameV2Art.Stretch(rowRt);
        bestRow = rowRt.gameObject.AddComponent<CanvasGroup>();
        float bx = 520;
        if (isNewBest && hasOldBest) bx = Tag(rowRt, bx, 368, "NEW BEST", ResultV2Fx.Gold, IngameV2Art.Hex(0x2A1D00), 6f) + 10;
        if (fullCombo) bx = Tag(rowRt, bx, 368, "FULL COMBO", ResultV2Fx.Mint, IngameV2Art.Hex(0x06201B), 6f) + 12;
        string bestText = hasOldBest ? "최고 " + FormatScore(bestShown) : "첫 기록";
        var bt = ResultV2Fx.Text("Best", rowRt, fKr, bestText, 18, ResultV2Fx.Dim, TextAlignmentOptions.MidlineLeft, bx, 366, 300, 28);
        bx += ResultV2Fx.Width(bt) + 12;
        if (isNewBest && hasOldBest && delta > 0)
            ResultV2Fx.Text("Delta", rowRt, fNum, "+" + FormatScore(delta), 22, ResultV2Fx.Gold, TextAlignmentOptions.MidlineLeft, bx, 366, 200, 28, 0f, true);
    }

    // 기울어진 작은 태그. 오른쪽 끝 x를 돌려준다
    private float Tag(Transform parent, float x, float y, string text, Color bg, Color fg, float slant)
    {
        var t = ResultV2Fx.Text("Tag Text", parent, fOxB, text, 13, fg, TextAlignmentOptions.Center, 0, 0, 10, 24, 6f);
        float w = Mathf.Round(ResultV2Fx.Width(t) + 22f);
        var shape = ResultV2Fx.Paint(Mathf.RoundToInt(w + slant), 24, (px, py) => new Color(1f, 1f, 1f, ResultV2Fx.SkewInside(px, py, w, 24, slant)));
        var img = ResultV2Fx.Raw("Tag", parent, shape, bg, x - slant * 0.5f, y, w + slant, 24);
        IngameV2Art.Place(t.rectTransform, x, y, w, 24);
        t.transform.SetSiblingIndex(img.transform.GetSiblingIndex() + 1);
        return x + w;
    }

    // 4-4. 판정 줄 4개
    private void BuildRows()
    {
        string[] names = { "PERFECT", "GREAT", "GOOD", "MISS" };
        Color[] colors = { ResultV2Fx.Perfect, ResultV2Fx.Great, ResultV2Fx.Good, ResultV2Fx.Miss };
        int[] values = { result.perfectCount, result.greatCount, result.goodCount, result.missCount };
        const float w = 500f, h = 50f, slant = 14.34f; // skewX(-16deg)
        int texW = Mathf.CeilToInt(w + slant);
        Color baseCol = new Color(8 / 255f, 28 / 255f, 26 / 255f, 0.72f);
        Color border = ResultV2Fx.A(ResultV2Fx.Mint, 0.18f);
        Color stripe = new Color(125 / 255f, 143 / 255f, 138 / 255f, 0.10f);
        var mask = ResultV2Fx.ToSprite(ResultV2Fx.Paint(texW, (int)h, (x, y) => new Color(1f, 1f, 1f, ResultV2Fx.SkewInside(x, y, w, h, slant))));

        for (int i = 0; i < 4; i++)
        {
            Color jc = colors[i];
            bool miss = i == 3;
            var tex = ResultV2Fx.Paint(texW, (int)h, (x, y) =>
            {
                float inside = ResultV2Fx.SkewInside(x, y, w, h, slant);
                if (inside <= 0f) return Color.clear;
                float left = ResultV2Fx.SkewLeft(y, h, slant);
                float fromLeft = x - left, fromRight = left + w - x;
                Color c;
                if (fromLeft < 6f) c = jc;
                else
                {
                    c = baseCol;
                    if (miss && ((int)y % 5) < 2) c = Color.Lerp(c, ResultV2Fx.A(stripe, 1f), stripe.a);
                    if (y < 1f || y > h - 1f || fromRight < 1.2f) c = Color.Lerp(c, ResultV2Fx.A(border, Mathf.Max(c.a, border.a)), 0.5f);
                }
                c.a *= inside;
                return c;
            });

            var row = new Row { value = values[i] };
            row.root = IngameV2Art.NewRect(names[i], fgStage);
            IngameV2Art.PlaceCenter(row.root, 520 + i * 34 + w * 0.5f, 432 + i * 58 + h * 0.5f, w, h);
            row.cg = row.root.gameObject.AddComponent<CanvasGroup>();
            row.cg.alpha = 0f;
            ResultV2Fx.Raw("Bg", row.root, tex, Color.white, -slant * 0.5f, 0, texW, h);
            row.scan = ResultV2Fx.Tiled("Scan", row.root, ResultV2Fx.TileY(4, 1, Color.white), ResultV2Fx.A(ResultV2Fx.Mint, 0.4f), 0, 0, w, h);
            ResultV2Fx.Text("Name", row.root, fNem, names[i], 20, ResultV2Fx.A(jc, miss ? 0.9f : 1f), TextAlignmentOptions.MidlineLeft, 26, 0, 200, h, 4f);
            row.count = ResultV2Fx.Text("Count", row.root, fNum, "0", 34, Color.white, TextAlignmentOptions.MidlineRight, w - 22 - 200, 0, 200, h, 0f, true);
            row.bright = ResultV2Fx.Img("Bright", row.root, mask, new Color(1f, 1f, 1f, 0f), -slant * 0.5f, 0, texW, h);

            row.delay = P2 + UnityEngine.Random.Range(0.25f, 0.63f);
            row.kind = UnityEngine.Random.Range(0, 3);
            rows[i] = row;
        }

        // 최대 콤보 · 정확도
        var sum = NewGroup("Summary", P2 + 0.45f).rt;
        float sx = 670f, cy = 688f;
        var l1 = ResultV2Fx.Text("Combo Label", sum, fKr, "최대 콤보", 19, ResultV2Fx.Dim, TextAlignmentOptions.MidlineLeft, sx, cy - 16, 200, 32);
        sx += ResultV2Fx.Width(l1) + 10;
        comboValue = ResultV2Fx.Text("Combo", sum, fNum, result.maxCombo.ToString(), 30, Color.white, TextAlignmentOptions.MidlineLeft, sx, cy - 20, 160, 40, 0f, true);
        sx += ResultV2Fx.Width(comboValue) + 18;
        ResultV2Fx.Img("Divider", sum, null, ResultV2Fx.Line, sx, cy - 8, 1, 16);
        sx += 18;
        var l2 = ResultV2Fx.Text("Acc Label", sum, fKr, "정확도", 19, ResultV2Fx.Dim, TextAlignmentOptions.MidlineLeft, sx, cy - 16, 200, 32);
        sx += ResultV2Fx.Width(l2) + 10;
        accValue = ResultV2Fx.Text("Acc", sum, fNum, (accuracy * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%", 30, Color.white, TextAlignmentOptions.MidlineLeft, sx, cy - 20, 200, 40, 0f, true);
    }

    // 4-5. 랭크 판
    private void BuildRank()
    {
        const float w = 300f, h = 240f, slant = 59.84f; // skewX(-14deg)
        int texW = Mathf.CeilToInt(w + slant);
        Color dark = new Color(8 / 255f, 28 / 255f, 26 / 255f, 0.85f);
        Color inner = new Color(8 / 255f, 28 / 255f, 26 / 255f, 0.6f);
        Color mint = ResultV2Fx.Mint;
        var tex = ResultV2Fx.Paint(texW, (int)h, (x, y) =>
        {
            float inside = ResultV2Fx.SkewInside(x, y, w, h, slant);
            if (inside <= 0f) return Color.clear;
            float left = ResultV2Fx.SkewLeft(y, h, slant);
            float lx = x - left;
            float edge = Mathf.Min(Mathf.Min(lx, w - lx) * 0.97f, Mathf.Min(y, h - y));
            float u = (lx / w + y / h) * 0.5f;
            Color c = ResultV2Fx.Sample(u, (0f, ResultV2Fx.A(mint, 0.22f)), (0.55f, dark), (1f, dark));
            if (edge < 8f) c = Color.Lerp(c, ResultV2Fx.A(inner, Mathf.Max(c.a, 0.6f)), 0.6f);
            if (lx < 12f) c = mint;
            if (edge < 2f) c = mint;
            // 오른쪽 아래 사선 무늬
            if (lx > w - 104f && lx < w - 14f && y > h - 34f && y < h - 12f && ((int)(x + y) % 8) < 3)
                c = Color.Lerp(c, ResultV2Fx.A(mint, 1f), 0.7f);
            c.a *= inside;
            return c;
        });

        rankRoot = IngameV2Art.NewRect("Rank Plate", fgStage);
        IngameV2Art.PlaceCenter(rankRoot, 1100 + w * 0.5f, 100 + h * 0.5f, w, h);
        rankGroup = rankRoot.gameObject.AddComponent<CanvasGroup>();
        rankGroup.alpha = 0f;
        var glow = ResultV2Fx.Img("Glow", rankRoot, ResultV2Fx.SoftRect(30), ResultV2Fx.A(mint, 0.35f), -30 - slant * 0.25f, -30, w + 60 + slant * 0.5f, h + 60);
        glow.type = Image.Type.Sliced;
        ResultV2Fx.Raw("Plate", rankRoot, tex, Color.white, -slant * 0.5f, 0, texW, h);
        ResultV2Fx.Text("RANK", rankRoot, fOxB, "RANK", 15, ResultV2Fx.MintHi, TextAlignmentOptions.TopLeft, 30, 14, 200, 22, 24f);
        var letter = ResultV2Fx.Text("Letter", rankRoot, fNem, rank, 176, Color.white, TextAlignmentOptions.Center, 0, 30, w, h - 20);
        IngameV2Art.Glow(letter, ResultV2Fx.A(mint, 0.9f), 0.6f);
    }

    // 4-6. 폴라로이드 (사진 + 문구 묶음 랜덤)
    private void BuildPolaroid()
    {
        var set = settings.PickPolaroid();
        hasPolaroid = set != null && set.photo != null;
        if (!hasPolaroid)
        {
            typingDone = true;
            return;
        }

        const float cw = 430f, pw = 402f, ph = 500f;
        captionText = (set.caption ?? "").Replace("\r", "");
        caption = IngameV2Art.Text("Caption", fgStage, fKr, captionText, 22, IngameV2Art.Hex(0x0D3A33), TextAlignmentOptions.TopLeft);
        caption.textWrappingMode = TextWrappingModes.Normal;
        caption.lineSpacing = 12f; // line-height 1.35 근처
        caption.richText = true;
        float capH = Mathf.Max(30f, caption.GetPreferredValues(captionText, pw - 12f, 9999f).y);
        float ch = 14f + ph + 10f + capH + 2f + 12f;

        polRoot = IngameV2Art.NewRect("Polaroid", fgStage);
        IngameV2Art.PlaceCenter(polRoot, 1150 + cw * 0.5f, 250 + ch * 0.5f, cw, ch);
        polBaseY = polRoot.anchoredPosition.y;
        polGroup = polRoot.gameObject.AddComponent<CanvasGroup>();
        polGroup.alpha = 0f;

        var shadow = ResultV2Fx.Img("Shadow", polRoot, ResultV2Fx.SoftRect(24), new Color(0f, 0f, 0f, 0.6f), -24, 10 - 24, cw + 48, ch + 48);
        shadow.type = Image.Type.Sliced;
        ResultV2Fx.Img("Card", polRoot, null, IngameV2Art.Hex(0xECE9DF), 0, 0, cw, ch);
        var photo = ResultV2Fx.Raw("Photo", polRoot, set.photo.texture, Color.white, 14, 14, pw, ph);
        photo.uvRect = CoverUv(set.photo, pw / ph, set.focusX, set.focusY);

        caption.transform.SetParent(polRoot, false);
        IngameV2Art.Place(caption.rectTransform, 14 + 6, 14 + ph + 10, pw - 12, capH + 4);
        caption.text = "";

        var tape = ResultV2Fx.Img("Tape", polRoot, null, new Color(200 / 255f, 230 / 255f, 220 / 255f, 0.55f), 0, 0, 0, 0);
        IngameV2Art.PlaceCenter(tape.rectTransform, cw * 0.5f, 0f, 90, 28);
        tape.rectTransform.localEulerAngles = new Vector3(0, 0, -3f); // CSS rotate(3deg)
    }

    // 사진을 칸에 꽉 차게 자르는 uv (focus 0~1)
    private static Rect CoverUv(Sprite sprite, float targetAspect, float focusX, float focusY)
    {
        Texture2D tex = sprite.texture;
        Rect r = sprite.textureRect;
        float aspect = r.width / Mathf.Max(1f, r.height);
        float uw = 1f, vh = 1f;
        if (aspect > targetAspect) uw = targetAspect / aspect;
        else vh = aspect / targetAspect;
        float ux = (1f - uw) * focusX, vy = (1f - vh) * focusY;
        return new Rect((r.x + ux * r.width) / tex.width, (r.y + vy * r.height) / tex.height,
            uw * r.width / tex.width, vh * r.height / tex.height);
    }

    // 4-7. 버튼, 아래 막대
    private void BuildButtons()
    {
        var bg = NewGroup("Buttons", P2 + 0.9f);
        buttonsGroup = bg.cg;
        var b = bg.rt;

        if (!story && canRetry)
        {
            var hit = ResultV2Fx.Img("Retry", b, null, new Color(0, 0, 0, 0), 596, 744, 150, 34);
            hit.raycastTarget = true;
            var btn = hit.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Retry);
            var rt = ResultV2Fx.Text("Text", b, fKr, "다시하기", 20, ResultV2Fx.Fg, TextAlignmentOptions.MidlineLeft, 600, 744, 120, 34);
            KeyCap(b, 600 + ResultV2Fx.Width(rt) + 10, 761, "R");
        }

        // 주 버튼 (곡 선택으로 / 계속하기)
        const float px = 760, py = 728, pw = 340, ph = 80, cut = 18;
        var glow = ResultV2Fx.Img("Primary Glow", b, ResultV2Fx.SoftRect(24), ResultV2Fx.A(ResultV2Fx.Mint, 0.4f), px - 24, py - 24, pw + 48, ph + 48);
        glow.type = Image.Type.Sliced;
        var shapeTex = ResultV2Fx.Paint((int)pw, (int)ph, (x, y) =>
        {
            bool tl = x + y < cut;
            bool br = (pw - x) + (ph - y) < cut;
            return new Color(1f, 1f, 1f, tl || br ? 0f : 1f);
        });
        var primary = ResultV2Fx.Img("Primary", b, ResultV2Fx.ToSprite(shapeTex), ResultV2Fx.Mint, px, py, pw, ph);
        primary.raycastTarget = true;
        var pbtn = primary.gameObject.AddComponent<Button>();
        var colors = pbtn.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        pbtn.colors = colors;
        pbtn.onClick.AddListener(Primary);
        Color ink = IngameV2Art.Hex(0x06201B);
        ResultV2Fx.Text("Label", primary.transform, fKr, story ? "계속하기" : "곡 선택으로", 30, ink, TextAlignmentOptions.MidlineLeft, 28, 10, 250, 40);
        ResultV2Fx.Text("Sub", primary.transform, fOxB, "CONTINUE", 12, ink, TextAlignmentOptions.MidlineLeft, 30, 50, 200, 16, 20f);
        ResultV2Fx.Text("Arrow", primary.transform, fNoto, "→", 34, ink, TextAlignmentOptions.Center, pw - 66, 16, 44, 48);

        // 아래 막대
        var bottom = NewGroup("Bottom Bar", P2 + 0.65f).rt;
        ResultV2Fx.Img("Top Line", bottom, null, ResultV2Fx.Line, 90, 828, 1420, 1);
        float hx = 90f;
        if (!story)
        {
            hx = Hint(bottom, hx, "ESC", "곡 선택") + 26f;
            if (canRetry) Hint(bottom, hx, "R", "다시하기");
        }
        // 오른쪽: ENTER 계속
        var et = ResultV2Fx.Text("Enter Hint", bottom, fOx, "계속", 14, ResultV2Fx.Dim, TextAlignmentOptions.MidlineRight, 1310, 836, 200, 30);
        float ew = ResultV2Fx.Width(et);
        float capW = KeyCapWidth("ENTER");
        KeyCap(bottom, 1510 - ew - 8 - capW, 851, "ENTER");
    }

    private float Hint(Transform parent, float x, string key, string label)
    {
        float w = KeyCap(parent, x, 851, key);
        var t = ResultV2Fx.Text("Hint", parent, fOx, label, 14, ResultV2Fx.Dim, TextAlignmentOptions.MidlineLeft, x + w + 8, 836, 200, 30);
        return x + w + 8 + ResultV2Fx.Width(t);
    }

    private float KeyCapWidth(string key)
    {
        return Mathf.Max(18f, key.Length * 7.5f + 10f);
    }

    // 키 모양 작은 상자. 폭을 돌려준다
    private float KeyCap(Transform parent, float x, float centerY, string key)
    {
        Color c = IngameV2Art.Hex(0xCCFFEE);
        float w = KeyCapWidth(key), h = 18f, y = centerY - h * 0.5f;
        ResultV2Fx.Img("Key T", parent, null, ResultV2Fx.A(c, 0.6f), x, y, w, 1);
        ResultV2Fx.Img("Key B", parent, null, ResultV2Fx.A(c, 0.6f), x, y + h - 1, w, 1);
        ResultV2Fx.Img("Key L", parent, null, ResultV2Fx.A(c, 0.6f), x, y, 1, h);
        ResultV2Fx.Img("Key R", parent, null, ResultV2Fx.A(c, 0.6f), x + w - 1, y, 1, h);
        ResultV2Fx.Text("Key", parent, fOxB, key, 11, c, TextAlignmentOptions.Center, x, y, w, h, 4f);
        return w;
    }

    // ───────────────────────── 매 프레임 ─────────────────────────
    private void Update()
    {
        if (fgStage == null) return;
        t += Time.unscaledDeltaTime;
        HandleInput();
        Render(t);
        if (scanOverlay != null) scanOverlay.uvRect = new Rect(0, 0, 1, canvasRect.rect.height / 3f);
    }

    private bool IntroDone { get { return skipped || t >= IntroEnd; } }

    private void HandleInput()
    {
        if (leaving) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool enter = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
        bool r = kb != null && kb.rKey.wasPressedThisFrame;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

        if (!IntroDone)
        {
            if (enter || space || click) Skip();
            return;
        }
        if (enter || esc) Primary();
        else if (r && !story && canRetry) Retry();
    }

    private void Skip()
    {
        skipped = true;
        t = Mathf.Max(t, SkipTo);
        // 착지 연출 중이면 오버레이 재킷을 바로 치우고 결과 재킷을 켠 상태로 (팝 소리는 내지 않음)
        if (landingOverlay != null) landingOverlay.FinishNow();
        if (caption != null)
        {
            typed = captionText.Length;
            glyphFrames = 0;
            typingDone = true;
            caption.text = "<noparse>" + captionText + "</noparse>";
        }
    }

    private void Render(float now)
    {
        RenderDecor(now);
        RenderWatermark(now);
        RenderGroups(now);
        RenderJacket(now);
        RenderScore(now);
        RenderRows(now);
        RenderRank(now);
        RenderPolaroid(now);
        RenderCaption(now);
        if (buttonsGroup != null)
        {
            bool on = IntroDone && !leaving;
            buttonsGroup.interactable = on;
            buttonsGroup.blocksRaycasts = on;
        }
    }

    private void RenderDecor(float now)
    {
        for (int i = 0; i < decors.Count; i++)
        {
            var d = decors[i];
            float u = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, d.delay, d.delay + 0.55f));
            if (d.fade != null) d.fade.alpha = u;
            for (int p = 0; p < d.parts.Count; p++)
            {
                var part = d.parts[p];
                var rt = part.raw.rectTransform;
                rt.sizeDelta = new Vector2(part.w * u, rt.sizeDelta.y);
                part.raw.uvRect = new Rect(part.uv.x, part.uv.y, part.uv.width * u, part.uv.height);
                part.raw.enabled = u > 0.001f;
            }
        }
        for (int i = 0; i < marquees.Count; i++)
        {
            var m = marquees[i];
            float s = Mathf.Repeat(now * m.speed, m.unit);
            m.rt.anchoredPosition = new Vector2(m.dir < 0 ? -s : -m.unit + s, m.rt.anchoredPosition.y);
        }
    }

    private void RenderWatermark(float now)
    {
        watermarkGroup.alpha = ResultV2Fx.Seg(now, 0.3f, 1.1f);
        float s = Mathf.Repeat(now * watermarkMq.speed, watermarkMq.unit);
        float jump = 0f, alpha = 1f;
        // 글리치: 2.5초부터 4.3초마다 0.4초 동안
        if (now >= 2.5f)
        {
            float p = Mathf.Repeat(now - 2.5f, 4.3f);
            if (p < 0.1f) { jump = 16f; alpha = 0.55f; }
            else if (p < 0.2f) { jump = -10f; alpha = 0.25f; }
            else if (p < 0.3f) { jump = 6f; }
        }
        watermarkMq.rt.anchoredPosition = new Vector2(-s + jump, 0f);
        watermarkGroup.alpha *= alpha;

        // 빨간 잔상: 2초부터 4.3초마다 0.3초
        float ga = 0f, gx = 0f;
        if (now >= 2f)
        {
            float p = Mathf.Repeat(now - 2f, 4.3f);
            if (p < 0.1f) { ga = 1f; gx = -28f; }
            else if (p < 0.2f) { ga = 1f; gx = 22f; }
            else if (p < 0.3f) { ga = 1f; gx = -12f; }
        }
        ghostGroup.alpha = ga * watermarkGroup.alpha;
        ghostMq.rt.anchoredPosition = new Vector2(-s + gx, 0f);
    }

    private void RenderGroups(float now)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            float u = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, g.at, g.at + 0.5f));
            g.cg.alpha = u;
            g.rt.anchoredPosition = new Vector2(0f, -14f * (1f - u));
        }
        // SIG 글씨: 깜빡이며 켜짐
        float k = now - (P2 + 0.2f);
        sigGroup.alpha = k < 0f ? 0f : k < 0.06f ? 0.6f : k < 0.12f ? 0.1f : k < 0.18f ? 0.9f : k < 0.24f ? 0.3f : 1f;
    }

    private void RenderJacket(float now)
    {
        if (landingMode)
        {
            RenderJacketLanding(now);
            return;
        }

        float k = (now - (P2 + 0.25f)) / 0.45f; // 노이즈 진행 0~1
        float pop = P2 + 0.70f;

        // 아래 빛, 빛 막대
        beamGlow.color = new Color(1f, 1f, 1f, ResultV2Fx.Seg(now, P2 + 0.75f, P2 + 1.25f));
        float ex = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, P2 + 0.75f, P2 + 1.05f));
        emitter.localScale = new Vector3(ex, 1f, 1f);

        if (now < pop)
        {
            SetJacketVisible(false, 0f);
            if (k >= 0f)
            {
                noiseImg.enabled = true;
                noiseImg.color = Color.white;
                // 진행 85% 전까지 18% 확률로 한 프레임 건너뛰기 (깜빡임)
                if (k >= 0.85f || UnityEngine.Random.value > 0.18f) noise.DrawStatic(Mathf.Clamp01(k));
            }
            else noiseImg.enabled = false;
            return;
        }

        if (!popped)
        {
            popped = true;
            if (settings.popSound != null && !skipped) sfx.PlayOneShot(settings.popSound, settings.popVolume * GameSettings.SfxVolume);
        }

        float since = now - pop;
        // 켜진 직후 깜빡임 (0.3초 안에 안정)
        float flick = since < 0.06f ? 0.35f : since < 0.12f ? 1f : since < 0.18f ? 0.75f : 1f;
        SetJacketVisible(true, since < 0.3f ? flick : 1f);
        float settle = ResultV2Fx.EaseOut(ResultV2Fx.Seg(since, 0f, 0.3f));
        jacketRoot.localScale = Vector3.one * Mathf.Lerp(1.035f, 1f, settle);
        jacketBright.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0f, settle)); // 밝기 1.7 → 1

        // 흰 빛 (0.4초)
        float wa = since < 0.08f ? 0.55f : since < 0.16f ? 0.25f : since < 0.24f ? 0.35f : since < 0.32f ? 0.08f : since < 0.4f ? Mathf.Lerp(0.08f, 0f, (since - 0.32f) / 0.08f) : 0f;
        jacketWash.color = ResultV2Fx.A(IngameV2Art.Hex(0xD4F7EC), wa);

        // 색 번짐 조각 (0.4초)
        bool tearOn = since < 0.4f;
        tearTimer -= Time.unscaledDeltaTime;
        for (int i = 0; i < tears.Count; i++)
        {
            tears[i].gameObject.SetActive(tearOn && jacketTex != null);
            if (!tearOn || tearTimer > 0f) continue;
            float bandY = UnityEngine.Random.Range(0f, 360f), bandH = UnityEngine.Random.Range(8f, 40f);
            float off = UnityEngine.Random.Range(6f, 22f) * (i < 3 ? -1f : 1f);
            IngameV2Art.Place(tears[i].rectTransform, off, bandY, 380f, bandH);
            Rect baseUv = jacketImg.uvRect;
            tears[i].uvRect = new Rect(baseUv.x, baseUv.y + baseUv.height * (1f - (bandY + bandH) / 380f), baseUv.width, baseUv.height * bandH / 380f);
        }
        if (tearTimer <= 0f) tearTimer = 0.05f;

        // 민트 색·스캔라인: P2 + 1.8까지 사라짐
        float fade = 1f - ResultV2Fx.Seg(now, pop, P2 + 1.8f);
        tintImg.color = new Color(1f, 1f, 1f, fade);
        scanImg.color = new Color(0f, 0f, 0f, 0.22f * fade);

        // 켜진 직후 노이즈는 0.35에서 0으로 사라지고, 그 뒤로는 완전히 끈다 (재이 요청: 끝난 뒤 치지직 알갱이가 과함)
        float na = Mathf.Lerp(0.35f, 0f, ResultV2Fx.Seg(now, pop, P2 + 1.5f));
        noiseImg.enabled = na > 0.001f;
        noiseImg.color = new Color(1f, 1f, 1f, na);
    }

    // 곡 끝 → 결과 전환 A: 레코드가 재킷 자리에 내려앉는 순간(landR) 진짜 재킷을 켠다.
    // 노이즈·깜빡임·흰 빛·색 번짐은 쓰지 않는다 (레코드가 그대로 내려앉은 느낌). 끝 상태는 원래 결과 화면과 같다.
    private void RenderJacketLanding(float now)
    {
        // 결과 씬이 늦게 켜졌으면 오버레이가 착지 시각을 조금 당겨 알려 준다
        if (landingOverlay != null) landR = landingOverlay.LandResultTime;
        float land = landR;
        noiseImg.enabled = false;

        if (now < land)
        {
            SetJacketVisible(false, 0f);
            beamGlow.color = new Color(1f, 1f, 1f, 0f);
            emitter.localScale = new Vector3(0f, 1f, 1f);
            return;
        }

        if (!popped)
        {
            popped = true;
            if (settings.popSound != null && !skipped) sfx.PlayOneShot(settings.popSound, settings.popVolume * GameSettings.SfxVolume);
            // 같은 프레임에 오버레이 재킷을 숨기고 외곽선 퍼짐을 시작한다
            if (landingOverlay != null) landingOverlay.NotifyLanded();
        }

        SetJacketVisible(true, 1f);
        jacketRoot.localScale = Vector3.one;
        float k = ResultV2Fx.Seg(now, land, land + 0.35f);
        jacketBright.color = new Color(1f, 1f, 1f, 0.6f * (1f - k));
        jacketWash.color = ResultV2Fx.A(IngameV2Art.Hex(0xD4F7EC), 0f);
        // 글로우: 0.4 + 0.6 x (1 - k) 비율로 밝았다가 원래 세기(0.35)로
        jacketGlow.color = ResultV2Fx.A(ResultV2Fx.Mint, 0.35f * (0.4f + 0.6f * (1f - k)) / 0.4f);
        for (int i = 0; i < tears.Count; i++) tears[i].gameObject.SetActive(false);

        // emitter 막대는 착지 순간 바로 켜짐, 아래 빛은 원래 결과 화면처럼 켜 둔다
        emitter.localScale = Vector3.one;
        beamGlow.color = Color.white;

        // 민트 색·스캔라인: 착지 후 1.1초에 걸쳐 사라짐
        float fade = 1f - ResultV2Fx.Seg(now, land, land + 1.1f);
        tintImg.color = new Color(1f, 1f, 1f, fade);
        scanImg.color = new Color(0f, 0f, 0f, 0.22f * fade);
    }

    private void SetJacketVisible(bool on, float alpha)
    {
        jacketImg.enabled = on;
        jacketImg.color = jacketTex != null ? new Color(1f, 1f, 1f, alpha) : ResultV2Fx.A(ResultV2Fx.DarkPanel, alpha);
        for (int i = 0; i < jacketImg.transform.childCount; i++) jacketImg.transform.GetChild(i).gameObject.SetActive(on);
        jacketBright.enabled = on;
        jacketWash.enabled = on;
        tintImg.enabled = on;
        scanImg.enabled = on;
        for (int i = 0; i < jacketFrame.Count; i++) jacketFrame[i].enabled = on;
    }

    private void RenderScore(float now)
    {
        float start = P2 + 0.3f, end = start + 0.85f;
        string text;
        bool counting = now >= start && now < end;
        if (now < start) text = "0";
        else if (counting)
        {
            float u = (now - start) / 0.85f;
            int v = Mathf.RoundToInt(ResultV2Fx.EaseOut(u) * finalScore);
            if (u < 0.9f) v += UnityEngine.Random.Range(0, 1000);
            if (finalScore > 0) v = Mathf.Min(v, finalScore - 1);
            text = FormatScore(Mathf.Max(0, v));
        }
        else text = FormatScore(finalScore);
        if (scoreText.text != text) scoreText.text = scoreRed.text = scoreMint.text = text;
        scoreRed.enabled = scoreMint.enabled = counting;

        // 끝나는 순간 펀치 (1.08 → 1, 오버슈트) + 빛
        float pu = ResultV2Fx.Seg(now, end, end + 0.45f);
        float scale = now < end ? 1f : 1f + 0.08f * (1f - ResultV2Fx.Back(pu, 2.2f));
        scoreRoot.localScale = Vector3.one * Mathf.Max(0.95f, scale);
        scoreFlash.color = ResultV2Fx.A(ResultV2Fx.Mint, now < end ? 0f : 0.45f * (1f - pu));

        // 막대: 왼쪽에서 오른쪽으로
        float bu = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, P2 + 0.25f, P2 + 0.75f));
        var bar = scoreBar.GetComponent<RawImage>();
        scoreBar.sizeDelta = new Vector2(645f * bu, 8f);
        bar.uvRect = new Rect(0, 0, bu, 1);

        // 최고 기록 줄: 펀치와 같이
        bestRow.alpha = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, end, end + 0.35f));

        // 반짝이는 띠: P2+1.4부터 4.5초마다 0.8초 동안 지나감
        var shineImg = shine.GetComponent<RawImage>();
        float sStart = P2 + 1.4f;
        if (now >= sStart)
        {
            float p = Mathf.Repeat(now - sStart, 4.5f);
            if (p < 0.8f)
            {
                float width = Mathf.Max(200f, scoreText.preferredWidth);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-90f, width + 20f, p / 0.8f), shine.anchoredPosition.y);
                shineImg.color = new Color(1f, 1f, 1f, 0.35f);
            }
            else shineImg.color = new Color(1f, 1f, 1f, 0f);
        }
        else shineImg.color = new Color(1f, 1f, 1f, 0f);
    }

    private void RenderRows(float now)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            float s = now - r.delay;
            if (s < 0f)
            {
                r.cg.alpha = 0f;
                continue;
            }
            float u = Mathf.Clamp01(s / 0.6f);
            float x = 0f, sx = 1f, sy = 1f, a = 1f, bright = 1f;
            if (r.kind == 0 || r.kind == 1)
            {
                // A: 오른쪽에서 들어옴 / B: 왼쪽에서 (빨간 번짐)
                float dir = r.kind == 0 ? 1f : -1f;
                if (u < 0.45f)
                {
                    float e = ResultV2Fx.EaseOut(u / 0.45f);
                    x = Mathf.Lerp(160f * dir, (r.kind == 0 ? -14f : 12f), e);
                    sx = Mathf.Lerp(1.25f, 0.98f, e);
                    bright = Mathf.Lerp(2.4f, 1f, e);
                    a = Mathf.Lerp(0.2f, 0.75f, e);
                }
                else if (u < 0.55f) { a = 0.3f; x = r.kind == 0 ? -14f : 12f; }
                else if (u < 0.65f) { a = 0.9f; x = 5f * dir; }
                else if (u < 0.75f) { a = 0.55f; }
                else { float e = (u - 0.75f) / 0.25f; a = Mathf.Lerp(0.55f, 1f, e); }
            }
            else
            {
                // C: 가는 선에서 펼쳐짐
                if (u < 0.35f)
                {
                    float e = ResultV2Fx.EaseOut(u / 0.35f);
                    sy = Mathf.Lerp(0.05f, 1.15f, e);
                    sx = Mathf.Lerp(1.4f, 1f, e);
                    bright = Mathf.Lerp(3f, 1.2f, e);
                }
                else if (u < 0.5f) { a = 0.25f; x = -8f; sy = 1.05f; }
                else if (u < 0.65f) { a = 0.5f; x = 6f; }
                else { float e = (u - 0.65f) / 0.35f; a = Mathf.Lerp(0.5f, 1f, e); sy = 1f; }
            }
            r.cg.alpha = a;
            var basePos = r.root.anchoredPosition;
            r.root.anchoredPosition = new Vector2(520 + i * 34 + 250 + x, basePos.y);
            r.root.localScale = new Vector3(sx, sy, 1f);
            Color bc = r.kind == 1 && u < 0.45f ? new Color(1f, 0.55f, 0.65f) : Color.white;
            r.bright.color = new Color(bc.r, bc.g, bc.b, Mathf.Clamp01((bright - 1f) * 0.4f));
            r.scan.color = ResultV2Fx.A(ResultV2Fx.Mint, 0.4f * (1f - Mathf.Clamp01(s / 0.9f)));

            // 숫자: d + 0.38부터 0.42초
            float cu = ResultV2Fx.Seg(s, 0.38f, 0.8f);
            int v = Mathf.RoundToInt(ResultV2Fx.EaseOut(cu) * r.value);
            string txt = v.ToString();
            if (r.count.text != txt) r.count.text = txt;
            r.count.color = cu < 1f && s >= 0.38f ? ResultV2Fx.Mint : Color.white;
        }

        float su = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, P2 + 1.05f, P2 + 1.47f));
        string combo = Mathf.RoundToInt(result.maxCombo * su).ToString();
        if (comboValue.text != combo) comboValue.text = combo;
        string acc = (accuracy * 100f * su).ToString("0.00", CultureInfo.InvariantCulture) + "%";
        if (accValue.text != acc) accValue.text = acc;
    }

    private void RenderRank(float now)
    {
        float u = ResultV2Fx.Seg(now, P2 + 1.15f, P2 + 1.6f);
        rankGroup.alpha = Mathf.Clamp01(u * 3f);
        float scale = now < P2 + 1.15f ? 1.8f : Mathf.LerpUnclamped(1.8f, 1f, ResultV2Fx.Back(u, 2.0f));
        rankRoot.localScale = Vector3.one * scale;
    }

    private void RenderPolaroid(float now)
    {
        if (!hasPolaroid) return;
        float u = ResultV2Fx.EaseOut(ResultV2Fx.Seg(now, P2 + 1.4f, P2 + 1.9f));
        polGroup.alpha = u;
        polRoot.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(12f, 5f, u)); // CSS -12도 → -5도
        polRoot.anchoredPosition = new Vector2(polRoot.anchoredPosition.x, polBaseY + 30f * (1f - u));
    }

    // 손글씨 타이핑: 글자마다 가짜 기호 1~2번 깜빡인 뒤 진짜 글자
    private void RenderCaption(float now)
    {
        if (!hasPolaroid || typingDone) return;
        float start = P2 + 1.9f;
        if (now < start) return;
        if (nextTypeAt < 0f) nextTypeAt = start;

        bool changed = false;
        while (!typingDone && now >= nextTypeAt)
        {
            changed = true;
            if (glyphFrames > 0)
            {
                glyphFrames--;
                if (glyphFrames > 0)
                {
                    glyph = GlitchGlyphs[UnityEngine.Random.Range(0, GlitchGlyphs.Length)];
                    nextTypeAt += 0.018f;
                    continue;
                }
                // 진짜 글자 찍기
                char c = captionText[typed];
                typed++;
                nextTypeAt += NextDelay(c);
            }
            else
            {
                if (typed >= captionText.Length)
                {
                    typingDone = true;
                    break;
                }
                char c = captionText[typed];
                if (c == ' ' || c == '\n')
                {
                    typed++;
                    nextTypeAt += c == '\n' ? 0.17f : 0.03f;
                }
                else
                {
                    glyphFrames = UnityEngine.Random.Range(1, 3) + 1; // 가짜 1~2번 + 진짜 1번
                    glyph = GlitchGlyphs[UnityEngine.Random.Range(0, GlitchGlyphs.Length)];
                    nextTypeAt += 0.018f;
                }
            }
            if (typed >= captionText.Length && glyphFrames == 0) typingDone = true;
        }
        if (!changed) return;

        string shown = "<noparse>" + captionText.Substring(0, typed) + "</noparse>";
        if (glyphFrames > 0 && !typingDone)
            shown += "<color=#E06A86><noparse>" + glyph + "</noparse></color>";
        caption.text = shown;
    }

    private static float NextDelay(char c)
    {
        if (c == ',' || c == '.') return 0.1f;
        return UnityEngine.Random.Range(0.03f, 0.052f);
    }

    // ───────────────────────── 버튼 동작 ─────────────────────────
    // 원래 결과 화면(RhythmResultSceneController.Leave)과 같은 순서: 검게 페이드 → 요청 → 씬 이동
    private void Primary()
    {
        if (story)
        {
            string back = RhythmResultContext.ReturnSceneName;
            string scene = !string.IsNullOrWhiteSpace(back) && Application.CanStreamedLevelBeLoaded(back) ? back : hubSceneName;
            StartCoroutine(Leave(scene, RhythmLaunchContext.Clear));
        }
        else
        {
            StartCoroutine(Leave(songSelectSceneName, () => RhythmLaunchContext.RequestSingle(string.Empty, hubSceneName)));
        }
    }

    private void Retry()
    {
        if (story || !canRetry) return;
        string back = string.IsNullOrWhiteSpace(RhythmResultContext.ReturnSceneName) ? hubSceneName : RhythmResultContext.ReturnSceneName;
        // 곡 선택 v2와 같은 방법으로 같은 곡을 요청한다 (난이도 정보 RhythmLaunchExtrasV2는 그대로 남아 있다)
        StartCoroutine(Leave(GameplayScene, () => RhythmLaunchContext.Request(RhythmEntryMode.Single, songName, back)));
    }

    private IEnumerator Leave(string scene, Action prepare)
    {
        if (leaving) yield break;
        if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("[결과 v2] 이동할 씬을 찾지 못했습니다: " + scene);
            yield break;
        }
        leaving = true;
        if (transition != null)
        {
            while (transition.IsBusy) yield return null;
            yield return transition.FadeToBlack();
        }
        if (prepare != null) prepare();
        SceneManager.LoadScene(scene);
    }

    // ───────────────────────── 도우미 ─────────────────────────
    private static string FormatScore(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
