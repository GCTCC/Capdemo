using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 인게임 v2(시안 E4) HUD. 원본 RhythmGameUI(OnGUI)를 Canvas UI(TMP)로 옮겼다.
// Play를 누르면 코드로 화면을 만든다. 위치는 시안(1600 x 900) 좌표 x 1.2 = 1920 x 1080 캔버스.
//  - 배경 캔버스(카메라 뒤): 라이브하우스 일러스트(밝기 85%) + 비네트, 스포트라이트 빔 자리
//  - HUD 캔버스(맨 앞): 곡 정보, 진행도, 점수·정확도·랭크, 일시정지 버튼, 콤보, 판정, 게이트 옆 글자, 화면 스캔라인
// LIFE(LifeGaugeV2), BAND SYNC(BandSyncPanel), 스포트라이트(ComboSpotlightV2)는 따로 붙은 컴포넌트가 이 캔버스에 그린다.
[DisallowMultipleComponent]
public class RhythmGameUIV2 : MonoBehaviour
{
    [Header("글꼴 (TTF) — 에디터 메뉴가 채운다")]
    [SerializeField] private Font oxaniumRegular;
    [SerializeField] private Font oxaniumBold;
    [SerializeField] private Font sairaCondensed;      // 큰 숫자(점수·콤보·판정). 기울임은 TMP Italic
    [SerializeField] private Font notoSansKr;          // 한글

    [Header("그림")]
    [SerializeField] private Sprite background;        // 청록 라이브하우스 무대
    [SerializeField, Range(0f, 1f)] private float backgroundBrightness = 0.85f;
    [SerializeField] private Texture2D noCover;
    [Tooltip("곡의 앨범 재킷을 화면 가득 채워 배경으로 쓴다 (재킷이 없는 곡은 라이브하우스 배경)")]
    [SerializeField] private bool coverAsBackground = true;
    [Tooltip("재킷 배경의 밝기. 노트가 잘 보이게 어둡게 깐다")]
    [SerializeField, Range(0f, 1f)] private float coverBackgroundBrightness = 0.45f;

    [Header("곡 정보 아래 작은 글씨")]
    [SerializeField] private string venueText = "RE:WRITE · LIVE AT RUST HALL";
    [Tooltip("스토리 모드일 때 대신 보일 공연 장소 (비우면 위 글씨)")]
    [SerializeField] private string storyVenueText = "";

    [Header("정확도 가중치 (결과 화면 랭크와 같은 기준)")]
    [SerializeField] private float perfectWeight = 1.0f;
    [SerializeField] private float greatWeight = 0.7f;
    [SerializeField] private float goodWeight = 0.4f;

    [Header("화면 스캔라인")]
    [SerializeField, Range(0f, 0.5f)] private float screenScanlineAlpha = 0.14f;

    // 다른 V2 컴포넌트가 쓰는 것들
    public TMP_FontAsset FontLabel { get; private set; }
    public TMP_FontAsset FontLabelBold { get; private set; }
    public TMP_FontAsset FontNumber { get; private set; }
    public TMP_FontAsset FontKorean { get; private set; }
    public RectTransform Hud { get; private set; }
    public RectTransform BackgroundLayer { get; private set; }  // 배경 그림 위, 하이웨이 뒤
    public RectTransform ComboTagAnchor { get; private set; }
    public IngameSpectrumV2 Spectrum { get; private set; }
    public float Accuracy { get; private set; }   // 0~1
    public bool HasAccuracy { get; private set; }
    public RhythmGameControllerV2 Controller { get; private set; }

    private RhythmPlayFieldV2 playField;
    private PauseMenuV2 pauseMenu;
    private JudgementScoreManager scores;
    private RhythmClock clock;
    private CanvasGroup hudGroup;
    private RectTransform hudCanvasRect;

    // 곡 정보
    private RawImage cover;
    private RawImage coverBackground;
    private AspectRatioFitter coverBackgroundFitter;
    private Image diffTag;
    private TextMeshProUGUI diffText, bpmText, titleMain, titleRed, titleCyan, venue;
    // 진행도
    private TextMeshProUGUI timeNow, timeTotal;
    private RectTransform barFill, barMarker, barArea;
    private readonly List<TextMeshProUGUI> sectionLabels = new List<TextMeshProUGUI>();
    private readonly List<float> sectionStarts = new List<float>();
    private RectTransform sectionRoot;
    private string sectionsForSong;
    // 점수
    private TextMeshProUGUI scoreText, accText;
    private float shownScore, scoreFrom, scoreTo, scoreT = 1f;
    private const float ScoreRollTime = 0.15f;
    // 콤보
    private RectTransform comboRoot;
    private TextMeshProUGUI comboText;
    private float comboPop = 1f;
    private int lastCombo;
    // 판정
    private RectTransform judgeRoot;
    private TextMeshProUGUI judgeMain, judgeRed, judgeCyan, judgeOffset;
    private CanvasGroup judgeGroup;
    private float judgeTimer;
    private const float JudgeShowTime = 0.5f, JudgeFadeTime = 0.2f;
    // 게이트 옆 글자
    private RectTransform gateLeft, gateRight;
    private TextMeshProUGUI gateBpm;
    // 전체 플래시(스포트라이트가 켜질 때)
    private Image flash;
    private float flashT;

    private bool visible;

    public void Initialize(RhythmGameControllerV2 controller, RhythmPlayFieldV2 newPlayField, PauseMenuV2 newPauseMenu)
    {
        Controller = controller;
        playField = newPlayField;
        pauseMenu = newPauseMenu;
        scores = controller.Scores;
        clock = controller.Clock;
        Spectrum = new IngameSpectrumV2();

        IngameV2Art.SetKoreanFallback(notoSansKr);
        FontKorean = IngameV2Art.MakeFont(notoSansKr, null);
        FontLabel = IngameV2Art.MakeFont(oxaniumRegular, FontKorean);
        FontLabelBold = IngameV2Art.MakeFont(oxaniumBold != null ? oxaniumBold : oxaniumRegular, FontLabel);
        FontNumber = IngameV2Art.MakeFont(sairaCondensed, FontLabelBold);

        BuildBackgroundCanvas();
        BuildHudCanvas();
        EnsureEventSystem();

        scores.ScoreChanged += OnScoreChanged;
        scores.ComboChanged += OnComboChanged;
        controller.JudgedSigned += OnJudged;

        var life = GetComponent<LifeGaugeV2>();
        if (life) life.Initialize(this);
        var band = GetComponent<BandSyncPanel>();
        if (band) band.Initialize(this);
        var spot = GetComponent<ComboSpotlightV2>();
        if (spot) spot.Initialize(this, playField);

        SetVisible(false);
    }

    public void SetVisible(bool show)
    {
        visible = show;
        if (hudGroup) hudGroup.alpha = show ? 1f : 0f;
        if (show) ResetForSong();
    }

    // ── 만들기 ──
    private void BuildBackgroundCanvas()
    {
        var go = new GameObject("Ingame V2 Background Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 95f;
        canvas.sortingOrder = -100;
        SetScaler(go.GetComponent<CanvasScaler>());
        var rootRect = (RectTransform)go.transform;

        var bg = IngameV2Art.Img("Live House", rootRect, background, Color.white * backgroundBrightness);
        bg.color = new Color(backgroundBrightness, backgroundBrightness, backgroundBrightness, 1f);
        IngameV2Art.Stretch(bg.rectTransform);
        if (background)
        {
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / background.rect.height;
        }
        else
        {
            bg.color = new Color(0.02f, 0.08f, 0.08f, 1f);
        }

        // 앨범 재킷 배경: 화면을 꽉 채우도록(넘치는 부분은 잘림) 라이브하우스 배경 위에 깐다
        var coverBgGo = new GameObject("Album Cover Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        coverBgGo.transform.SetParent(rootRect, false);
        coverBackground = coverBgGo.GetComponent<RawImage>();
        coverBackground.raycastTarget = false;
        coverBackground.color = new Color(coverBackgroundBrightness, coverBackgroundBrightness, coverBackgroundBrightness, 1f);
        IngameV2Art.Stretch(coverBackground.rectTransform);
        coverBackgroundFitter = coverBgGo.GetComponent<AspectRatioFitter>();
        coverBackgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        coverBackgroundFitter.aspectRatio = 1f;
        coverBgGo.SetActive(false);

        BackgroundLayer = IngameV2Art.NewRect("Spotlight Layer", rootRect);
        IngameV2Art.Stretch(BackgroundLayer);

        var vignette = IngameV2Art.Img("Vignette", rootRect, IngameV2Art.Vignette(), Color.white);
        IngameV2Art.Stretch(vignette.rectTransform);
    }

    private void BuildHudCanvas()
    {
        var go = new GameObject("Ingame V2 HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        SetScaler(go.GetComponent<CanvasScaler>());
        hudGroup = go.GetComponent<CanvasGroup>();
        hudCanvasRect = (RectTransform)go.transform;
        Hud = IngameV2Art.NewRect("HUD", hudCanvasRect);
        IngameV2Art.Stretch(Hud);

        BuildGateLabels();
        BuildSongPanel();
        BuildProgress();
        BuildScore();
        BuildPauseButton();
        BuildCombo();
        BuildJudgement();

        // 화면 전체 스캔라인 (1px 선, 3px 간격) + 플래시
        var scan = IngameV2Art.NewRect("Screen Scanlines", hudCanvasRect).gameObject.AddComponent<RawImage>();
        scan.texture = IngameV2Art.ScanlineTile(screenScanlineAlpha);
        scan.raycastTarget = false;
        IngameV2Art.Stretch(scan.rectTransform);
        scan.uvRect = new Rect(0, 0, 1, 1080f / 3f);

        flash = IngameV2Art.Img("Flash", hudCanvasRect, null, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0f));
        IngameV2Art.Stretch(flash.rectTransform);
    }

    private static void SetScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // 유리 패널을 만들어 돌려준다 (시안 좌표)
    public RectTransform GlassPanel(string name, Transform parent, float mx, float my, float mw, float mh)
    {
        float k = IngameV2Art.K;
        int w = Mathf.RoundToInt(mw * k), h = Mathf.RoundToInt(mh * k);
        var img = IngameV2Art.Img(name, parent, IngameV2Art.GlassPanel(w, h), Color.white);
        IngameV2Art.Place(img.rectTransform, mx * k, my * k, w, h);
        return img.rectTransform;
    }

    private void BuildSongPanel()
    {
        float k = IngameV2Art.K;
        var panel = GlassPanel("Song Panel", Hud, 56, 30, 420, 96);

        // 앨범 커버 (민트 테두리 글로우)
        var glow = IngameV2Art.Img("Cover Glow", panel, IngameV2Art.SoftDot(), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.35f));
        IngameV2Art.Place(glow.rectTransform, 2 * k, 1 * k, 94 * k, 94 * k);
        var frame = IngameV2Art.Img("Cover Frame", panel, null, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.85f));
        IngameV2Art.Place(frame.rectTransform, 12 * k, 12 * k, 72 * k, 72 * k);
        cover = IngameV2Art.NewRect("Cover", panel).gameObject.AddComponent<RawImage>();
        cover.raycastTarget = false;
        IngameV2Art.Place(cover.rectTransform, 13 * k, 13 * k, 70 * k, 70 * k);

        // 첫 줄: 난이도 태그, BPM, LIVE
        float tagY = 14 * k;
        diffTag = IngameV2Art.Img("Difficulty Tag", panel, IngameV2Art.Parallelogram(110, 22, 7), IngameV2Art.Red);
        IngameV2Art.Place(diffTag.rectTransform, 100 * k, tagY, 76 * k, 16 * k);
        diffText = IngameV2Art.Text("Text", diffTag.rectTransform, FontLabelBold, "HARD 12", 15, IngameV2Art.Ink, TextAlignmentOptions.Center, 6f);
        IngameV2Art.Stretch(diffText.rectTransform);

        bpmText = IngameV2Art.Text("BPM", panel, FontLabel, "BPM 172", 14, IngameV2Art.TextDim, TextAlignmentOptions.MidlineLeft, 8f);
        IngameV2Art.Place(bpmText.rectTransform, 184 * k, tagY, 70 * k, 16 * k);

        var live = IngameV2Art.Img("LIVE Tag", panel, null, IngameV2Art.Red);
        IngameV2Art.Place(live.rectTransform, 246 * k, tagY, 60 * k, 16 * k);
        var dot = IngameV2Art.Img("Dot", live.rectTransform, IngameV2Art.SoftDot(), Color.white);
        IngameV2Art.PlaceCenter(dot.rectTransform, 11 * k, 8 * k, 9, 9);
        var liveText = IngameV2Art.Text("Text", live.rectTransform, FontLabelBold, "LIVE", 15, Color.white, TextAlignmentOptions.Center, 8f);
        IngameV2Art.Place(liveText.rectTransform, 14 * k, 0, 46 * k, 16 * k);

        // 곡명 (Oxanium Bold 28 + RGB 분리 글리치)
        titleMain = IngameV2Art.GlitchText("Title", panel, FontLabelBold, "Moonside Howl", 28 * k, IngameV2Art.MintLight,
            TextAlignmentOptions.MidlineLeft, 2f, false, 0f, out titleRed, out titleCyan);
        IngameV2Art.Place((RectTransform)titleMain.transform.parent, 99 * k, 34 * k, 310 * k, 30 * k);
        IngameV2Art.Glow(titleMain, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.45f), 0.7f);

        venue = IngameV2Art.Text("Venue", panel, FontLabel, venueText, 13 * k, IngameV2Art.TextDim, TextAlignmentOptions.MidlineLeft, 0f);
        IngameV2Art.Place(venue.rectTransform, 100 * k, 68 * k, 310 * k, 16 * k);
    }

    private void BuildProgress()
    {
        float k = IngameV2Art.K;
        var panel = GlassPanel("Progress Panel", Hud, 540, 34, 520, 46);

        timeNow = IngameV2Art.Text("Now", panel, FontLabelBold, "00:00", 14 * k, IngameV2Art.MintLight, TextAlignmentOptions.MidlineLeft, 2f);
        IngameV2Art.Place(timeNow.rectTransform, 13 * k, 6 * k, 50 * k, 14 * k);
        IngameV2Art.Glow(timeNow, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.6f), 0.8f);
        timeTotal = IngameV2Art.Text("Total", panel, FontLabel, "00:00", 14 * k, IngameV2Art.TextDim, TextAlignmentOptions.MidlineRight, 2f);
        IngameV2Art.Place(timeTotal.rectTransform, 455 * k, 6 * k, 52 * k, 14 * k);

        // 바
        barArea = IngameV2Art.NewRect("Bar", panel);
        IngameV2Art.Place(barArea, 66 * k, 11 * k, 388 * k, 4 * k);
        var track = IngameV2Art.Img("Track", barArea, null, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.16f));
        IngameV2Art.Stretch(track.rectTransform);
        var fillGlow = IngameV2Art.Img("Fill Glow", barArea, IngameV2Art.SoftBar(), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.55f));
        barFill = IngameV2Art.Img("Fill", barArea, null, IngameV2Art.Mint).rectTransform;
        barFill.gameObject.AddComponent<HorizontalGradientV2>().Set(IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.35f), IngameV2Art.MintLight);
        SetFill(barFill, 0f);
        fillGlow.rectTransform.SetParent(barFill, false);
        fillGlow.rectTransform.anchorMin = new Vector2(0, 0.5f);
        fillGlow.rectTransform.anchorMax = new Vector2(1, 0.5f);
        fillGlow.rectTransform.sizeDelta = new Vector2(0, 20);
        fillGlow.rectTransform.anchoredPosition = Vector2.zero;
        barMarker = IngameV2Art.Img("Marker", barArea, null, Color.white).rectTransform;
        barMarker.anchorMin = barMarker.anchorMax = new Vector2(0, 0.5f);
        barMarker.pivot = new Vector2(0.5f, 0.5f);
        barMarker.sizeDelta = new Vector2(3, 22);

        sectionRoot = IngameV2Art.NewRect("Sections", panel);
        IngameV2Art.Place(sectionRoot, 66 * k, 11 * k, 388 * k, 30 * k);
    }

    private static void SetFill(RectTransform fill, float t)
    {
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
    }

    private void BuildScore()
    {
        float k = IngameV2Art.K;
        float right = 1548 * k; // 시안 오른쪽 끝
        var label = IngameV2Art.Text("SCORE", Hud, FontLabelBold, "SCORE", 11 * k, IngameV2Art.TextDim, TextAlignmentOptions.MidlineRight, 12f);
        IngameV2Art.Place(label.rectTransform, right - 200 * k, 25 * k, 200 * k, 14 * k);

        scoreText = IngameV2Art.Text("Score", Hud, FontNumber, "0000000", 60 * k, Color.white, TextAlignmentOptions.MidlineRight, 0f, true);
        scoreText.richText = true;
        IngameV2Art.Place(scoreText.rectTransform, right - 330 * k, 38 * k, 334 * k, 62 * k);
        IngameV2Art.Glow(scoreText, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.35f), 0.8f);

        var line = IngameV2Art.Img("Line", Hud, null, Color.white);
        line.gameObject.AddComponent<HorizontalGradientV2>().Set(IngameV2Art.WithAlpha(IngameV2Art.Mint, 0f), IngameV2Art.Mint);
        IngameV2Art.Place(line.rectTransform, right - 196 * k, 100 * k, 196 * k, 2);

        accText = IngameV2Art.Text("Acc", Hud, FontLabelBold, "ACC --.--% · RANK -", 12 * k, IngameV2Art.TextDim, TextAlignmentOptions.MidlineRight, 8f);
        accText.richText = true;
        IngameV2Art.Place(accText.rectTransform, right - 260 * k, 106 * k, 260 * k, 16 * k);
    }

    private void BuildPauseButton()
    {
        float k = IngameV2Art.K;
        float x = 1404 * k, y = 156 * k, w = 136 * k, h = 32 * k;
        var bgImg = IngameV2Art.Img("Pause Button", Hud, IngameV2Art.Parallelogram(Mathf.RoundToInt(w), Mathf.RoundToInt(h), 10),
            IngameV2Art.Mint);
        bgImg.raycastTarget = true;
        IngameV2Art.Place(bgImg.rectTransform, x, y, w, h);
        var outline = IngameV2Art.Img("Outline", bgImg.rectTransform,
            IngameV2Art.Parallelogram(Mathf.RoundToInt(w), Mathf.RoundToInt(h), 10, true), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.4f));
        IngameV2Art.Stretch(outline.rectTransform);

        var button = bgImg.gameObject.AddComponent<Button>();
        var colors = button.colors;
        // 민트 바탕에 곱해지는 색: 평소 어두운 유리, 마우스를 올리면 밝게
        colors.normalColor = new Color(0.05f, 0.15f, 0.15f, 0.8f);
        colors.highlightedColor = new Color(0.25f, 0.45f, 0.42f, 0.9f);
        colors.selectedColor = new Color(0.05f, 0.15f, 0.15f, 0.8f);
        colors.pressedColor = new Color(0.4f, 0.6f, 0.56f, 0.95f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.AddListener(() => { if (pauseMenu) pauseMenu.OpenPauseMenu(); });

        // ❚❚ 아이콘 (글꼴에 없을 수 있어서 막대 두 개로 그린다)
        for (int i = 0; i < 2; i++)
        {
            var bar = IngameV2Art.Img("Bar" + i, bgImg.rectTransform, null, Color.white);
            IngameV2Art.Place(bar.rectTransform, (20 + i * 6) * k, 10 * k, 3 * k, 12 * k);
        }
        var text = IngameV2Art.Text("Label", bgImg.rectTransform, FontKorean, "일시정지", 14 * k, Color.white, TextAlignmentOptions.MidlineLeft, 0f);
        IngameV2Art.Place(text.rectTransform, 38 * k, 0, 64 * k, h);
        var keyBox = IngameV2Art.Img("ESC Box", bgImg.rectTransform, null, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.18f));
        IngameV2Art.Place(keyBox.rectTransform, 100 * k, 9 * k, 26 * k, 14 * k);
        var keyLine = IngameV2Art.Img("ESC Line", keyBox.rectTransform, IngameV2Art.Parallelogram(32, 18, 0, true), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.6f));
        IngameV2Art.Stretch(keyLine.rectTransform);
        var esc = IngameV2Art.Text("ESC", keyBox.rectTransform, FontLabelBold, "ESC", 9 * k, IngameV2Art.MintLight, TextAlignmentOptions.Center, 2f);
        IngameV2Art.Stretch(esc.rectTransform);
    }

    private void BuildCombo()
    {
        float k = IngameV2Art.K;
        comboRoot = IngameV2Art.NewRect("Combo", Hud);
        IngameV2Art.PlaceCenter(comboRoot, 800 * k, 160 * k, 300 * k, 110 * k);
        comboText = IngameV2Art.Text("Number", comboRoot, FontNumber, "0", 120 * k, Color.white, TextAlignmentOptions.Center, 0f, true);
        IngameV2Art.Stretch(comboText.rectTransform);
        IngameV2Art.Glow(comboText, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.3f), 0.9f);

        var label = IngameV2Art.Text("COMBO", Hud, FontLabelBold, "COMBO", 14 * k, IngameV2Art.Mint, TextAlignmentOptions.Center, 38f);
        IngameV2Art.PlaceCenter(label.rectTransform, 800 * k, 216 * k, 200 * k, 18 * k);
        label.transform.SetParent(comboRoot, true);

        ComboTagAnchor = IngameV2Art.NewRect("Combo Tag Anchor", Hud);
        IngameV2Art.PlaceCenter(ComboTagAnchor, 800 * k, 241 * k, 220 * k, 18 * k);
    }

    private void BuildJudgement()
    {
        float k = IngameV2Art.K;
        judgeRoot = IngameV2Art.NewRect("Judgement", Hud);
        IngameV2Art.PlaceCenter(judgeRoot, 800 * k, 532 * k, 500 * k, 90 * k);
        judgeGroup = judgeRoot.gameObject.AddComponent<CanvasGroup>();
        judgeMain = IngameV2Art.GlitchText("Word", judgeRoot, FontNumber, "PERFECT", 64 * k, Color.white,
            TextAlignmentOptions.Center, 3f, true, 2f, out judgeRed, out judgeCyan);
        IngameV2Art.Stretch((RectTransform)judgeMain.transform.parent);
        IngameV2Art.Glow(judgeMain, IngameV2Art.WithAlpha(Color.black, 0.5f), 0.5f);

        judgeOffset = IngameV2Art.Text("Offset", judgeRoot, FontLabelBold, "+0.000s", 12 * k, Color.white, TextAlignmentOptions.Center, 6f);
        var ort = judgeOffset.rectTransform;
        ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0f);
        ort.pivot = new Vector2(0.5f, 1f);
        ort.sizeDelta = new Vector2(200, 20);
        ort.anchoredPosition = new Vector2(0, -4);
        judgeGroup.alpha = 0f;
    }

    private void BuildGateLabels()
    {
        float k = IngameV2Art.K;
        gateLeft = IngameV2Art.NewRect("Gate Label Left", Hud);
        var l = IngameV2Art.Text("Text", gateLeft, FontLabelBold, "HOLO·LANE 06", 10 * k, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.8f), TextAlignmentOptions.BottomRight, 10f);
        gateLeft.sizeDelta = new Vector2(220, 18);
        gateLeft.pivot = new Vector2(1f, 0f);
        IngameV2Art.Stretch(l.rectTransform);

        gateRight = IngameV2Art.NewRect("Gate Label Right", Hud);
        gateRight.sizeDelta = new Vector2(220, 18);
        gateRight.pivot = new Vector2(0f, 0f);
        gateBpm = IngameV2Art.Text("Text", gateRight, FontLabelBold, "120 BPM", 10 * k, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.8f), TextAlignmentOptions.BottomLeft, 10f);
        IngameV2Art.Place(gateBpm.rectTransform, 0, 0, 120, 18);
        gateBpm.rectTransform.anchorMin = gateBpm.rectTransform.anchorMax = new Vector2(0, 0);
        gateBpm.rectTransform.pivot = new Vector2(0, 0);
        gateBpm.rectTransform.anchoredPosition = Vector2.zero;
        // ▮▮▮▯ (글꼴에 없을 수 있어서 작은 막대로 그린다)
        for (int i = 0; i < 4; i++)
        {
            var b = IngameV2Art.Img("Cell" + i, gateRight, null, IngameV2Art.WithAlpha(IngameV2Art.Mint, i < 3 ? 0.85f : 0.25f));
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0, 0);
            b.rectTransform.pivot = new Vector2(0, 0);
            b.rectTransform.sizeDelta = new Vector2(4, 9);
            b.rectTransform.anchoredPosition = new Vector2(0, 3);
        }
    }

    // ── 매 곡 시작 ──
    private void ResetForSong()
    {
        shownScore = scoreFrom = scoreTo = 0f;
        scoreT = 1f;
        lastCombo = 0;
        comboRoot.gameObject.SetActive(false);
        judgeTimer = 0f;
        judgeGroup.alpha = 0f;
        Accuracy = 1f;
        HasAccuracy = false;
        UpdateScoreText();
        UpdateAccText();
        RefreshSongInfo();
        var life = GetComponent<LifeGaugeV2>();
        if (life) life.ResetLife();
        var spot = GetComponent<ComboSpotlightV2>();
        if (spot) spot.ResetForSong();
    }

    private void RefreshSongInfo()
    {
        var song = Controller.CurrentSong;
        Texture tex = song != null && song.albumCover != null ? (Texture)song.albumCover : noCover;
        cover.texture = tex;
        cover.color = tex ? Color.white : new Color(0.05f, 0.15f, 0.15f, 1f);

        if (coverBackground)
        {
            Texture bgTex = coverAsBackground && song != null ? song.albumCover : null;
            coverBackground.texture = bgTex;
            coverBackground.gameObject.SetActive(bgTex);
            if (bgTex) coverBackgroundFitter.aspectRatio = (float)bgTex.width / Mathf.Max(1, bgTex.height);
        }

        string diff = Controller.DifficultyName;
        int level = Controller.DifficultyLevel;
        diffTag.color = IngameV2Art.DifficultyColor(diff);
        diffText.text = level > 0 ? diff + " " + level : diff;

        string title = PrettyTitle(Controller.CurrentSongTitle);
        titleMain.text = titleRed.text = titleCyan.text = title;
        venue.text = Controller.IsStoryMode && !string.IsNullOrWhiteSpace(storyVenueText) ? storyVenueText : venueText;

        LoadSections(song != null ? song.DisplayName : null);
    }

    // 파일 이름에서 온 곡명을 보기 좋게: "Moonside-Howl-_フリーBGM…" → "Moonside Howl"
    public static string PrettyTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "NO TRACK";
        string s = raw;
        int cut = s.IndexOf('_');
        if (cut > 0) s = s.Substring(0, cut);
        s = s.Replace('-', ' ').Trim();
        return s.Length > 0 ? s : raw;
    }

    // 곡 구간 라벨: Resources/RhythmSongs/{곡이름}_sections.json (없으면 4등분 눈금만)
    [System.Serializable] private class SectionFile { public SectionEntry[] sections; }
    [System.Serializable] private class SectionEntry { public float time; public string name; }

    private void LoadSections(string songName)
    {
        if (sectionsForSong == songName && sectionRoot.childCount > 0) return;
        sectionsForSong = songName;
        for (int i = sectionRoot.childCount - 1; i >= 0; i--) Destroy(sectionRoot.GetChild(i).gameObject);
        sectionLabels.Clear();
        sectionStarts.Clear();

        SectionFile file = null;
        if (!string.IsNullOrEmpty(songName))
        {
            var asset = Resources.Load<TextAsset>("RhythmSongs/" + songName + "_sections");
            if (asset != null)
            {
                try { file = JsonUtility.FromJson<SectionFile>(asset.text); }
                catch (System.Exception e) { Debug.LogWarning("구간 파일을 읽지 못했습니다: " + e.Message); }
            }
        }
        if (file != null && file.sections != null && file.sections.Length > 0)
        {
            for (int i = 0; i < file.sections.Length; i++)
            {
                sectionStarts.Add(file.sections[i].time);
                var t = IngameV2Art.Text("Section " + file.sections[i].name, sectionRoot, FontLabelBold,
                    (file.sections[i].name ?? "").ToUpperInvariant(), 10 * IngameV2Art.K, IngameV2Art.TextDim, TextAlignmentOptions.Center, 10f);
                sectionLabels.Add(t);
            }
        }
        else
        {
            for (int i = 1; i < 4; i++) sectionStarts.Add(-i); // 음수 = 4등분 눈금 표시용
        }
        sectionsLaidOut = false;
        sectionRoot.gameObject.SetActive(false); // 곡 길이를 알기 전에는 숨긴다
    }

    private bool sectionsLaidOut;

    private void LayoutSections(float duration)
    {
        if (sectionsLaidOut || duration <= 0f) return;
        sectionsLaidOut = true;
        sectionRoot.gameObject.SetActive(true);
        float width = sectionRoot.rect.width;
        // 눈금
        for (int i = 0; i < sectionStarts.Count; i++)
        {
            float t = sectionStarts[i] < 0 ? -sectionStarts[i] / 4f : sectionStarts[i] / duration;
            if (t <= 0.001f) continue;
            var tick = IngameV2Art.Img("Tick", sectionRoot, null, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.45f));
            IngameV2Art.Place(tick.rectTransform, t * width, -4, 1, 12);
        }
        // 라벨: 구간 시작 ~ 다음 구간 시작의 가운데
        for (int i = 0; i < sectionLabels.Count; i++)
        {
            float a = sectionStarts[i] / duration;
            float b = i + 1 < sectionStarts.Count ? sectionStarts[i + 1] / duration : 1f;
            IngameV2Art.PlaceCenter(sectionLabels[i].rectTransform, (a + b) * 0.5f * width, 17 * IngameV2Art.K, (b - a) * width + 30, 14);
        }
    }

    // ── 이벤트 ──
    private void OnScoreChanged(int score)
    {
        scoreFrom = shownScore;
        scoreTo = score;
        scoreT = 0f;
        RecalcAccuracy();
    }

    private void OnComboChanged(int combo)
    {
        if (combo > lastCombo) comboPop = 0f;
        lastCombo = combo;
        comboRoot.gameObject.SetActive(combo > 0);
        comboText.text = combo.ToString();
    }

    private void OnJudged(JudgementResult result, double signedOffset)
    {
        Color c;
        string word;
        switch (result.judgement)
        {
            case JudgementType.Perfect: c = Color.white; word = "PERFECT"; break;
            case JudgementType.Great: c = IngameV2Art.Mint; word = "GREAT"; break;
            case JudgementType.Good: c = IngameV2Art.GoodGray; word = "GOOD"; break;
            default: c = IngameV2Art.Red; word = "MISS"; break;
        }
        judgeMain.text = judgeRed.text = judgeCyan.text = word;
        judgeMain.color = c;
        bool showOffset = result.judgement != JudgementType.Miss || System.Math.Abs(signedOffset) > 0.0001;
        judgeOffset.text = showOffset ? (signedOffset >= 0 ? "+" : "-") + System.Math.Abs(signedOffset).ToString("0.000") + "s" : "";
        judgeOffset.color = IngameV2Art.WithAlpha(c, 0.85f);
        judgeTimer = JudgeShowTime + JudgeFadeTime;
        judgeGroup.alpha = 1f;
        judgeRoot.localScale = Vector3.one * 1.06f;
    }

    private void RecalcAccuracy()
    {
        int total = scores.PerfectCount + scores.GreatCount + scores.GoodCount + scores.MissCount;
        HasAccuracy = total > 0;
        Accuracy = HasAccuracy
            ? (scores.PerfectCount * perfectWeight + scores.GreatCount * greatWeight + scores.GoodCount * goodWeight) / total
            : 1f;
        UpdateAccText();
    }

    // 결과 화면(RhythmBestRecords.RankOf)과 같은 기준
    public static string RankFor(float acc)
    {
        if (acc >= 0.95f) return "S";
        if (acc >= 0.90f) return "A";
        if (acc >= 0.80f) return "B";
        if (acc >= 0.70f) return "C";
        return "D";
    }

    private void UpdateAccText()
    {
        string mint = ColorUtility.ToHtmlStringRGB(IngameV2Art.Mint);
        string white = ColorUtility.ToHtmlStringRGB(Color.white);
        accText.text = HasAccuracy
            ? "ACC <color=#" + white + ">" + (Accuracy * 100f).ToString("0.00") + "%</color> · RANK <color=#" + mint + ">" + RankFor(Accuracy) + "</color>"
            : "ACC <color=#" + white + ">--.--%</color> · RANK <color=#" + mint + ">-</color>";
    }

    private void UpdateScoreText()
    {
        int value = Mathf.Clamp(Mathf.RoundToInt(shownScore), 0, 9999999);
        string digits = value.ToString("0000000");
        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') lead++;
        scoreText.text = lead > 0
            ? "<alpha=#33>" + digits.Substring(0, lead) + "<alpha=#FF>" + digits.Substring(lead)
            : digits;
    }

    // ── 매 프레임 ──
    private void Update()
    {
        if (Controller == null) return;
        float dt = Time.unscaledDeltaTime;
        if (Spectrum == null) Spectrum = new IngameSpectrumV2(); // Play 중에 스크립트가 다시 컴파일되면 비어 있을 수 있다
        Spectrum.Update(Controller.MusicSource);

        // 점수 굴리기
        if (scoreT < 1f)
        {
            scoreT = Mathf.Min(1f, scoreT + dt / ScoreRollTime);
            shownScore = Mathf.Lerp(scoreFrom, scoreTo, 1f - (1f - scoreT) * (1f - scoreT));
            UpdateScoreText();
        }

        // 콤보 튀기 (1.0 → 1.08 → 1.0, 0.12초)
        if (comboPop < 1f)
        {
            comboPop = Mathf.Min(1f, comboPop + dt / 0.12f);
            float s = 1f + 0.08f * Mathf.Sin(comboPop * Mathf.PI);
            comboText.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        // 판정 글자
        if (judgeTimer > 0f)
        {
            judgeTimer -= dt;
            judgeGroup.alpha = Mathf.Clamp01(judgeTimer / JudgeFadeTime);
            judgeRoot.localScale = Vector3.Lerp(judgeRoot.localScale, Vector3.one, dt * 18f);
            float jitter = Random.value < 0.15f ? Random.Range(-2f, 2f) : 0f;
            judgeRed.rectTransform.anchoredPosition = new Vector2(-3f + jitter, 0f);
            judgeCyan.rectTransform.anchoredPosition = new Vector2(3f - jitter, 0f);
        }

        // 곡명 글리치: 가끔 크게 번진다
        float tj = Random.value < 0.02f ? 4f : 2f;
        titleRed.rectTransform.anchoredPosition = new Vector2(-tj, 0f);
        titleCyan.rectTransform.anchoredPosition = new Vector2(tj, 0f);

        UpdateProgress();
        UpdateGateLabels();

        if (flashT > 0f)
        {
            flashT -= dt;
            flash.color = IngameV2Art.WithAlpha(IngameV2Art.Mint, Mathf.Clamp01(flashT / 0.25f) * 0.35f);
        }
    }

    public void Flash()
    {
        flashT = 0.25f;
    }

    private void UpdateProgress()
    {
        float duration = clock.SongDuration;
        // 곡 시작 전(전환 연출 카운트인 중)에는 곡 파일 길이로 보여 준다
        if (duration <= 0f && Controller.CurrentSong != null && Controller.CurrentSong.audioClip != null)
            duration = Controller.CurrentSong.audioClip.length;
        float now = Mathf.Max(0f, (float)clock.SongTime);
        float t = duration > 0f ? Mathf.Clamp01(now / duration) : 0f;
        timeNow.text = FormatTime(now);
        timeTotal.text = FormatTime(duration);
        SetFill(barFill, t);
        barMarker.anchoredPosition = new Vector2(t * barArea.rect.width, 0f);
        LayoutSections(duration);
        // 현재 구간만 흰색
        int current = -1;
        for (int i = 0; i < sectionLabels.Count; i++)
        {
            if (now >= sectionStarts[i]) current = i;
        }
        for (int i = 0; i < sectionLabels.Count; i++)
        {
            sectionLabels[i].color = i == current ? Color.white : IngameV2Art.TextDim;
        }
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    private void UpdateGateLabels()
    {
        Camera cam = Camera.main;
        if (cam == null || playField == null) return;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Hud, cam.WorldToScreenPoint(playField.GateLeftWorld), null, out local))
        {
            gateLeft.anchorMin = gateLeft.anchorMax = new Vector2(0.5f, 0.5f);
            gateLeft.anchoredPosition = local + new Vector2(-6f, -4f);
        }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Hud, cam.WorldToScreenPoint(playField.GateRightWorld), null, out local))
        {
            gateRight.anchorMin = gateRight.anchorMax = new Vector2(0.5f, 0.5f);
            gateRight.anchoredPosition = local + new Vector2(6f, -4f);
        }
        int bpm = Mathf.RoundToInt(clock.IsPlaying || Controller.PendingBpm <= 0f ? clock.CurrentBpm : Controller.PendingBpm);
        string bpmString = bpm + " BPM";
        if (gateBpm.text != bpmString) gateBpm.text = bpmString;
        bpmText.text = "BPM " + bpm;
        // 막대 4칸을 글자 오른쪽에 붙인다
        float x = gateBpm.preferredWidth + 6f;
        for (int i = 0; i < 4; i++)
        {
            var cell = (RectTransform)gateRight.GetChild(1 + i);
            cell.anchoredPosition = new Vector2(x + i * 6f, 3f);
        }
    }

    private void OnDestroy()
    {
        if (scores != null)
        {
            scores.ScoreChanged -= OnScoreChanged;
            scores.ComboChanged -= OnComboChanged;
        }
        if (Controller != null) Controller.JudgedSigned -= OnJudged;
    }
}

// 곡 AudioSource의 주파수(스펙트럼)를 한 프레임에 한 번만 읽어 나눠 쓴다.
// 곡마다 소리 크기가 달라서, 최근 최고값으로 나눠(자동 음량 맞춤) 0~1로 만든다.
public class IngameSpectrumV2
{
    private const int BarBands = 64;
    private readonly float[] raw = new float[512];
    private readonly float[] smooth = new float[BarBands];
    private readonly float[] barPeak = new float[BarBands];
    private readonly float[] groupPeak = new float[3];
    private int lastFrame = -1;

    public float Low { get; private set; }      // 저음 (드럼)
    public float LowMid { get; private set; }   // 저중음 (베이스)
    public float MidHigh { get; private set; }  // 중고음 (보컬·기타)
    public float[] Bands { get { return smooth; } }  // 64칸, 0~1, 스무딩됨

    public void Update(AudioSource source)
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        float dt = Time.unscaledDeltaTime;
        bool playing = source != null && source.isPlaying && source.clip != null;
        if (playing) source.GetSpectrumData(raw, 0, FFTWindow.BlackmanHarris);
        else System.Array.Clear(raw, 0, raw.Length);

        float binHz = AudioSettings.outputSampleRate * 0.5f / raw.Length;
        Low = Approach(Low, Normalize(Band(40f, 160f, binHz), ref groupPeak[0], dt), dt);
        LowMid = Approach(LowMid, Normalize(Band(160f, 600f, binHz), ref groupPeak[1], dt), dt);
        MidHigh = Approach(MidHigh, Normalize(Band(600f, 5000f, binHz), ref groupPeak[2], dt), dt);

        // 막대용 64칸: 로그 간격(60Hz ~ 12kHz)
        for (int i = 0; i < BarBands; i++)
        {
            float f0 = 60f * Mathf.Pow(200f, i / (float)BarBands);
            float f1 = 60f * Mathf.Pow(200f, (i + 1) / (float)BarBands);
            smooth[i] = Approach(smooth[i], Normalize(Band(f0, f1, binHz), ref barPeak[i], dt), dt);
        }
    }

    private float Band(float f0, float f1, float binHz)
    {
        int a = Mathf.Clamp(Mathf.FloorToInt(f0 / binHz), 0, raw.Length - 1);
        int b = Mathf.Clamp(Mathf.CeilToInt(f1 / binHz), a + 1, raw.Length);
        float sum = 0f;
        for (int i = a; i < b; i++) sum += raw[i];
        return sum / (b - a);
    }

    // 최근 최고값(천천히 줄어듦)으로 나눈다. 아주 작은 소리는 0 근처로
    private static float Normalize(float value, ref float peak, float dt)
    {
        peak = Mathf.Max(value, peak * Mathf.Exp(-0.35f * dt), 1e-5f);
        float v = value / peak;
        return Mathf.Clamp01(v * v * 0.95f);
    }

    // 올라갈 때는 빠르게, 내려갈 때는 천천히
    private static float Approach(float current, float target, float dt)
    {
        target = Mathf.Clamp01(target);
        float speed = target > current ? 18f : 5f;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
    }
}
