using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 리듬게임 > 새 곡 선택 화면 만들기 (v2)
// SongSelectScene_v2를 열고, 홀로그램 곡 선택 화면(Canvas UI, 시안 "곡선택_4_홀로그램_사이버펑크")을 만들어 저장한다.
// 1) Assets/Art/UI/SongSelect 폴더에 임시 그림을 만든다. 이미 있으면 건드리지 않는다
//    → 재이 그림으로 바꾸려면 같은 이름의 PNG로 덮어쓰면 된다. (배경: SongSelectBackground.png)
// 2) 기존 OnGUI 곡 선택(SongSelectSceneController, SongLobbyMenu)을 떼고 SongSelectHoloController를 붙인다.
// 3) v2 씬 3개를 Build Settings에 넣는다 (씬 이동은 Build Settings에 있어야 된다).
// 좌표는 시안 그림(2000x1125로 본 값)에 0.96을 곱해 1920x1080 캔버스에 놓는다.
public static class SongSelectV2Menu
{
    private const string ScenePath = "Assets/Scenes/RhythmIngame_v2/SongSelectScene_v2.unity";
    private static readonly string[] V2Scenes =
    {
        "Assets/Scenes/RhythmIngame_v2/SongSelectScene_v2.unity",
        "Assets/Scenes/RhythmIngame_v2/RhythmScene_v2.unity",
        "Assets/Scenes/RhythmIngame_v2/RhythmResultScene_v2.unity",
    };

    private const string Folder = "Assets/Art/UI/SongSelect";
    private const string BackgroundPath = Folder + "/SongSelectBackground.png";
    private const string GlassRowPath = Folder + "/GlassRow.png";
    private const string GlassRowSelectedPath = Folder + "/GlassRowSelected.png";
    private const string RowGlowPath = Folder + "/RowGlow.png";
    private const string ParaPath = Folder + "/Parallelogram.png";
    private const string ChipPath = Folder + "/Chip.png";
    private const string KeyBoxPath = Folder + "/KeyBox.png";
    private const string RingPath = Folder + "/HoloRing.png";
    private const string ThinRingPath = Folder + "/ThinRing.png";
    private const string DashRingPath = Folder + "/DashRing.png";
    private const string DotPath = Folder + "/SoftDot.png";
    private const string BeamPath = Folder + "/LightBeam.png";
    private const string ShadePath = Folder + "/ShadeRight.png";
    private const string VignettePath = Folder + "/Vignette.png";
    private const string ScanlinePath = Folder + "/Scanline.png";
    private const string WavePath = Folder + "/IconWave.png";
    private const string ArrowLeftPath = Folder + "/IconArrowLeft.png";
    private const string ArrowRightPath = Folder + "/IconArrowRight.png";
    private const string GaugePath = Folder + "/IconGauge.png";
    private const string PulsePath = Folder + "/IconPulse.png";
    private const string GearPath = Folder + "/IconGear.png";
    private const string NoCoverPath = Folder + "/NoCover.png";
    private const string MaterialPath = Folder + "/HoloCover.mat";
    private const string ShaderName = "CapDemo/UI/Hologram";

    // 재이: 이 화면은 우선 경기천년제목으로 (어색하면 Oxanium·Saira·Noto Sans KR로 바꾸기)
    private const string TitleFontPath = "Assets/Art/Fonts/경기천년제목_Bold SDF.asset";
    private const string MonoFontPath = "Assets/Art/Fonts/neodgm SDF.asset";

    private const float K = 0.96f; // 시안 좌표 → 캔버스 좌표

    // 색
    private static readonly Color Mint = new Color(0.64f, 0.92f, 0.84f, 1f);          // #A3EBD6
    private static readonly Color MintLine = new Color(0.45f, 0.80f, 0.74f, 0.55f);
    private static readonly Color Red = new Color(1f, 0.30f, 0.37f, 1f);              // #FF4D5E
    private static readonly Color Amber = new Color(1f, 0.71f, 0.36f, 1f);            // #FFB45C
    private static readonly Color Cyan = new Color(0.35f, 0.91f, 1f, 1f);             // #5AE8FF
    private static readonly Color TextMain = new Color(0.92f, 0.96f, 0.95f, 1f);
    private static readonly Color TextDim = new Color(0.66f, 0.76f, 0.74f, 1f);
    private static readonly Color Ink = new Color(0.03f, 0.09f, 0.08f, 1f);
    private static readonly Color PanelDark = new Color(0.04f, 0.10f, 0.10f, 0.72f);

    [MenuItem("Tools/CapDemo/리듬게임/새 곡 선택 화면 만들기 (v2)", priority = 80)]
    private static void Create()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("새 곡 선택 화면", ScenePath + " 씬이 없어요.", "확인");
            return;
        }

        var active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var root = GameObject.Find("Song Select Controller");
        if (!root)
        {
            EditorUtility.DisplayDialog("새 곡 선택 화면", "씬에 'Song Select Controller' 오브젝트가 없어요.", "확인");
            return;
        }

        var oldCanvas = GameObject.Find("SongSelectCanvas");
        if (oldCanvas)
        {
            if (!EditorUtility.DisplayDialog("새 곡 선택 화면",
                "이미 만든 화면(SongSelectCanvas)이 있어요.\n지우고 처음 상태로 다시 만들까요? (직접 옮기거나 바꾼 값은 사라져요)",
                "다시 만들기", "취소")) return;
            Object.DestroyImmediate(oldCanvas);
        }

        MakeArt();
        var art = new Art
        {
            background = ImportSprite(BackgroundPath, Vector4.zero, 512), // 작게 불러와 늘리면 살짝 흐려진다 (블러 약 3px)
            glassRow = ImportSprite(GlassRowPath, new Vector4(20, 20, 20, 20)),
            glassRowSelected = ImportSprite(GlassRowSelectedPath, new Vector4(20, 20, 20, 20)),
            rowGlow = ImportSprite(RowGlowPath, new Vector4(30, 30, 30, 30)),
            para = ImportSprite(ParaPath, new Vector4(26, 4, 26, 4)),
            chip = ImportSprite(ChipPath, new Vector4(12, 12, 12, 12)),
            keyBox = ImportSprite(KeyBoxPath, new Vector4(6, 6, 6, 6)),
            ring = ImportSprite(RingPath, Vector4.zero),
            thinRing = ImportSprite(ThinRingPath, Vector4.zero),
            dashRing = ImportSprite(DashRingPath, Vector4.zero),
            dot = ImportSprite(DotPath, Vector4.zero),
            beam = ImportSprite(BeamPath, Vector4.zero),
            shade = ImportSprite(ShadePath, Vector4.zero),
            vignette = ImportSprite(VignettePath, Vector4.zero),
            wave = ImportSprite(WavePath, Vector4.zero),
            arrowLeft = ImportSprite(ArrowLeftPath, Vector4.zero),
            arrowRight = ImportSprite(ArrowRightPath, Vector4.zero),
            gauge = ImportSprite(GaugePath, Vector4.zero),
            pulse = ImportSprite(PulsePath, Vector4.zero),
            gear = ImportSprite(GearPath, Vector4.zero),
            scanline = ImportRepeatTexture(ScanlinePath),
            noCover = ImportTexture(NoCoverPath),
            coverMaterial = MakeMaterial(),
            titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath),
            monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFontPath),
        };
        if (!art.monoFont) art.monoFont = art.titleFont;

        // 옛 OnGUI 곡 선택 떼기 (SongSelectSceneController가 SongLobbyMenu를 요구하므로 순서대로)
        var oldController = root.GetComponent<SongSelectSceneController>();
        if (oldController) Object.DestroyImmediate(oldController);
        var oldLobby = root.GetComponent<SongLobbyMenu>();
        if (oldLobby) Object.DestroyImmediate(oldLobby);
        var controller = root.GetComponent<SongSelectHoloController>();
        if (!controller) controller = root.AddComponent<SongSelectHoloController>();

        EnsureCamera();
        EnsureEventSystem();
        Build(controller, art);

        AddScenesToBuildSettings();
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = GameObject.Find("SongSelectCanvas");

        EditorUtility.DisplayDialog("새 곡 선택 화면",
            "SongSelectScene_v2에 새 곡 선택 화면(v4 홀로그램 시안)을 만들고 저장했어요.\n\n" +
            "· 배경·임시 그림: Assets/Art/UI/SongSelect (같은 이름 PNG로 덮어쓰면 바뀌어요)\n" +
            "· 홀로그램 커버 색·스캔라인: Assets/Art/UI/SongSelect/HoloCover 머티리얼\n" +
            "· 곡 줄 모양: SongList > Viewport > Content > Row Template\n" +
            "· 난이도 탭·배속·싱크는 아직 화면만 바뀌는 목업이에요.\n\n" +
            "Play를 누르면 바로 확인할 수 있어요.", "확인");
    }

    private class Art
    {
        public Sprite background, glassRow, glassRowSelected, rowGlow, para, chip, keyBox, ring, thinRing, dashRing, dot, beam,
            shade, vignette, wave, arrowLeft, arrowRight, gauge, pulse, gear;
        public Texture2D scanline, noCover;
        public Material coverMaterial;
        public TMP_FontAsset titleFont, monoFont;
    }

    // ── 화면 만들기 ──

    private static void Build(SongSelectHoloController controller, Art art)
    {
        var canvasGo = new GameObject("SongSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "새 곡 선택 화면");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var c = canvasGo.transform;

        // ── 배경: 일러스트(살짝 흐림) + 어둡게 + 오른쪽(목록 쪽)을 더 어둡게 + 가장자리 어둡게
        var bg = NewImage("Background", c, art.background, Color.white);
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = art.background ? art.background.rect.width / art.background.rect.height : 16f / 9f;
        Stretch(NewImage("Dim", c, null, new Color(0.01f, 0.03f, 0.03f, 0.42f)).rectTransform);
        Stretch(NewImage("ShadeRight", c, art.shade, new Color(0.01f, 0.03f, 0.03f, 1f)).rectTransform);
        Stretch(NewImage("Vignette", c, art.vignette, new Color(0f, 0.01f, 0.01f, 0.7f)).rectTransform);

        // ── 머리
        var header = NewRect("Header", c);
        Stretch(header);
        M(NewImage("LogoIcon", header, art.wave, Mint).rectTransform, 78, 44, 34, 36);
        M(NewText("GameTitle", header, art.titleFont, "RE:WRITE", 30, TextMain, TextAlignmentOptions.Left).rectTransform, 125, 38, 170, 46);
        M(NewImage("TitleSeparator", header, null, MintLine).rectTransform, 284, 47, 2, 26);
        var sub = NewText("Subtitle", header, art.monoFont, "MUSIC ARCHIVE", 16, TextDim, TextAlignmentOptions.Left);
        sub.characterSpacing = 10f;
        M(sub.rectTransform, 303, 44, 320, 30);

        var speed = OptionChip(header, art, "SpeedButton", art.gauge, "배속", "8.5", "F1", 1355, 178, out var speedLabel);
        var sync = OptionChip(header, art, "SyncButton", art.pulse, "싱크", "+12ms", "F2", 1547, 206, out var syncLabel);
        var settings = OptionChip(header, art, "SettingsButton", art.gear, "설정", "", "F10", 1767, 152, out _);
        M(NewImage("HeaderLine", header, null, MintLine).rectTransform, 80, 102, 1840, 1.5f);

        M(NewText("PageTitle", c, art.titleFont, "곡 선택", 56, TextMain, TextAlignmentOptions.Left).rectTransform, 78, 122, 200, 70);
        var trackCount = NewText("TrackCount", c, art.monoFont, "SINGLE PLAY  ·  00 TRACKS", 17, TextDim, TextAlignmentOptions.Left);
        trackCount.characterSpacing = 10f;
        M(trackCount.rectTransform, 248, 152, 520, 30);
        var nowPreview = NewText("NowPreviewing", c, art.monoFont, "♪  NOW PREVIEWING", 15, Mint, TextAlignmentOptions.Left);
        nowPreview.characterSpacing = 12f;
        M(nowPreview.rectTransform, 380, 210, 320, 26);

        // ── 홀로그램 커버 디스크 (가운데 시안 487, 527)
        var stage = NewRect("HoloStage", c);
        MC(stage, 487, 527, 1000, 1000);

        // 빛 기둥 + 받침 (커버 뒤)
        var beam = NewImage("LightBeam", stage, art.beam, new Color(Mint.r, Mint.g, Mint.b, 0.32f));
        Center(beam.rectTransform, 0, -265, 270, 200);
        var pedGlow = NewImage("PedestalGlow", stage, art.dot, new Color(Mint.r, Mint.g, Mint.b, 0.35f));
        Center(pedGlow.rectTransform, 0, -347, 360, 70);
        var pedOuter = NewImage("PedestalRingOuter", stage, art.thinRing, new Color(Mint.r, Mint.g, Mint.b, 0.9f));
        Center(pedOuter.rectTransform, 0, -347, 300, 300);
        pedOuter.rectTransform.localScale = new Vector3(1f, 0.15f, 1f);
        var pedInner = NewImage("PedestalRingInner", stage, art.thinRing, new Color(Mint.r, Mint.g, Mint.b, 0.55f));
        Center(pedInner.rectTransform, 0, -347, 210, 210);
        pedInner.rectTransform.localScale = new Vector3(1f, 0.14f, 1f);

        // 궤도 링 2개 (-18° 민트 실선, +14° 레드 점선)
        var orbitA = Ellipse(stage, "OrbitA", art.thinRing, new Color(Mint.r, Mint.g, Mint.b, 0.75f), 334f, 114f, -18f);
        Ellipse(stage, "OrbitB", art.dashRing, new Color(Red.r, Red.g, Red.b, 0.7f), 292f, 92f, 14f);

        // 스펙트럼 링 (막대는 실행할 때 만들어진다)
        var spectrum = NewRect("SpectrumRing", stage);
        Center(spectrum, 0, 0, 10, 10);

        var glow = NewImage("CoverGlow", stage, art.ring, new Color(Mint.r, Mint.g, Mint.b, 0.6f));
        Center(glow.rectTransform, 0, 0, 420, 420);

        var coverStage = NewRect("CoverStage", stage);
        Center(coverStage, 0, 0, 356, 356);
        var cover = new GameObject("Cover", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        cover.rectTransform.SetParent(coverStage, false);
        Stretch(cover.rectTransform);
        cover.texture = art.noCover;
        cover.material = art.coverMaterial;
        cover.raycastTarget = false;
        var sliceMint = NewImage("GlitchMint", coverStage, null, new Color(Mint.r, Mint.g, Mint.b, 0.85f));
        Center(sliceMint.rectTransform, 0, 0, 110, 3);
        var sliceRed = NewImage("GlitchRed", coverStage, null, new Color(Red.r, Red.g, Red.b, 0.85f));
        Center(sliceRed.rectTransform, 0, 0, 70, 3);

        // 미리듣기 진행 링 (흰 원호, 12시에서 시계방향) + 끝 점
        var progress = NewImage("ProgressRing", stage, art.thinRing, new Color(1f, 1f, 1f, 0.95f));
        Center(progress.rectTransform, 0, 0, 392, 392);
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Radial360;
        progress.fillOrigin = (int)Image.Origin360.Top;
        progress.fillClockwise = true;
        progress.fillAmount = 0f;
        var progressDot = NewImage("ProgressDot", stage, art.dot, Color.white);
        Center(progressDot.rectTransform, 0, 190, 16, 16);

        // 궤도 위를 도는 빛 점
        var dotA = NewImage("OrbitDotA", stage, art.dot, Mint);
        Center(dotA.rectTransform, 0, 0, 14, 14);
        var dotB = NewImage("OrbitDotB", stage, art.dot, Red);
        Center(dotB.rectTransform, 0, 0, 14, 14);

        // 연결선 시작점: 커버 오른쪽 위 (35도)
        var linkStart = NewRect("LinkStart", stage);
        linkStart.anchorMin = linkStart.anchorMax = new Vector2(0.5f, 0.5f);
        float a = 35f * Mathf.Deg2Rad;
        linkStart.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 200f;
        linkStart.sizeDelta = Vector2.zero;

        // 데이터 글씨
        var data = NewText("DataText", c, art.monoFont, "SIG ■■■■□\nFREQ 44.1k\nCH 2 · STEREO", 13, TextDim, TextAlignmentOptions.TopLeft);
        data.characterSpacing = 8f;
        data.lineSpacing = 18f;
        M(data.rectTransform, 120, 313, 260, 70);
        var previewLabel = NewText("PreviewLabel", c, art.monoFont, "PREVIEW", 13, TextDim, TextAlignmentOptions.Left);
        previewLabel.characterSpacing = 12f;
        M(previewLabel.rectTransform, 812, 750, 140, 22);
        var previewTime = NewText("PreviewTime", c, art.monoFont, "00:00 / 00:30", 21, TextMain, TextAlignmentOptions.Left);
        previewTime.characterSpacing = 4f;
        M(previewTime.rectTransform, 763, 770, 220, 32);

        var info = NewText("SongInfo", c, art.monoFont, "BPM 150      LENGTH 02:37      NOTES 842", 18, TextMain, TextAlignmentOptions.Center);
        info.characterSpacing = 8f;
        MCenter(info.rectTransform, 487, 945, 720, 34);

        // 연결선
        var connectorGo = new GameObject("Connector", typeof(RectTransform), typeof(CanvasRenderer), typeof(HoloConnectorLine));
        connectorGo.transform.SetParent(c, false);
        var connector = connectorGo.GetComponent<HoloConnectorLine>();
        Stretch(connector.rectTransform);
        connector.color = new Color(Mint.r, Mint.g, Mint.b, 0.85f);
        connector.raycastTarget = false;
        connector.StartPoint = linkStart;

        // ── 난이도 탭 (목업)
        string[] diffNames = { "EASY", "NORMAL", "HARD", "EXPERT" };
        Color[] diffColors = { Hex(0x7FE0A8), Hex(0x7FD4FF), Red, Hex(0xC48BFF) };
        float[] tabX = { 1050, 1205, 1357, 1512 };
        var tabs = new List<Object>();
        var tabNames = new List<Object>();
        var tabLevels = new List<Object>();
        for (int i = 0; i < 4; i++)
        {
            var tab = NewImage("Tab_" + diffNames[i], c, art.para, PanelDark);
            tab.raycastTarget = true;
            M(tab.rectTransform, tabX[i], 245, 142, 64);
            tab.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tab.rectTransform.anchoredPosition += new Vector2(tab.rectTransform.sizeDelta.x * 0.5f, -tab.rectTransform.sizeDelta.y * 0.5f);
            var b = tab.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var strip = NewImage("Strip", tab.transform, null, diffColors[i]);
            Inside(strip.rectTransform, 16, -1, 30, 4);
            var name = NewText("Name", tab.transform, art.monoFont, diffNames[i], 13, diffColors[i], TextAlignmentOptions.Left);
            name.characterSpacing = 10f;
            Inside(name.rectTransform, 18, 8, 120, 20);
            var level = NewText("Level", tab.transform, art.titleFont, "8", 30, Color.white, TextAlignmentOptions.Left);
            level.fontStyle = FontStyles.Italic;
            Inside(level.rectTransform, 18, 24, 90, 36);
            tabs.Add(tab);
            tabNames.Add(name);
            tabLevels.Add(level);
        }
        KeyCap(c, art, "←", 1680, 268, 32);
        KeyCap(c, art, "→", 1720, 268, 32);
        var diffHint = NewText("DiffHint", c, art.monoFont, "난이도", 14, TextDim, TextAlignmentOptions.Left);
        M(diffHint.rectTransform, 1760, 266, 90, 26);

        // ── 곡 목록 (선택된 줄이 왼쪽으로 튀어나올 자리만큼 왼쪽 여백을 둔다)
        var listGo = new GameObject("SongList", typeof(RectTransform), typeof(ScrollRect));
        var list = (RectTransform)listGo.transform;
        list.SetParent(c, false);
        M(list, 990, 330, 930, 530);
        var viewport = NewRect("Viewport", list);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 0, 8, 8);
        layout.spacing = 11f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = listGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var row = BuildRowTemplate(content, art);

        // ── 왼쪽 아래: 최고 기록
        var bestLabel = NewText("BestLabel", c, art.monoFont, "BEST SCORE  ·  NORMAL", 14, TextDim, TextAlignmentOptions.Left);
        bestLabel.characterSpacing = 10f;
        M(bestLabel.rectTransform, 1045, 946, 300, 24);
        var bestScore = NewText("BestScore", c, art.titleFont, "--", 40, TextMain, TextAlignmentOptions.Left);
        bestScore.fontStyle = FontStyles.Italic;
        M(bestScore.rectTransform, 1045, 964, 260, 54);
        var rankLabel = NewText("RankLabel", c, art.monoFont, "RANK", 14, TextDim, TextAlignmentOptions.Left);
        rankLabel.characterSpacing = 10f;
        M(rankLabel.rectTransform, 1248, 946, 80, 24);
        var bestRank = NewText("BestRank", c, art.titleFont, "-", 40, Mint, TextAlignmentOptions.Left);
        bestRank.fontStyle = FontStyles.Italic;
        M(bestRank.rectTransform, 1248, 964, 70, 54);
        var fc = NewImage("FullComboTag", c, null, Amber);
        M(fc.rectTransform, 1325, 964, 130, 24);
        var fcText = NewText("Label", fc.transform, art.monoFont, "FULL COMBO", 13, Ink, TextAlignmentOptions.Center);
        fcText.characterSpacing = 12f;
        Stretch(fcText.rectTransform);

        // ── 오른쪽 아래: 플레이 버튼 (민트 평행사변형 + 빛)
        var playGlow = NewImage("PlayGlow", c, art.dot, new Color(Mint.r, Mint.g, Mint.b, 0.35f));
        MCenter(playGlow.rectTransform, 1763, 976, 420, 140);
        var playImg = NewImage("PlayButton", c, art.para, Mint);
        playImg.raycastTarget = true;
        M(playImg.rectTransform, 1608, 940, 312, 72);
        var play = playImg.gameObject.AddComponent<Button>();
        SetButtonColors(play);
        var playText = NewText("Label", playImg.transform, art.titleFont, "플레이", 32, Ink, TextAlignmentOptions.Left);
        Inside(playText.rectTransform, 38, 4, 160, 40);
        var playSub = NewText("Sub", playImg.transform, art.monoFont, "START", 14, Ink, TextAlignmentOptions.Left);
        playSub.characterSpacing = 14f;
        Inside(playSub.rectTransform, 38, 46, 160, 20);
        var playArrow = NewImage("Arrow", playImg.transform, art.arrowRight, Ink);
        playArrow.rectTransform.anchorMin = playArrow.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        playArrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        playArrow.rectTransform.anchoredPosition = new Vector2(-40f, 0f);
        playArrow.rectTransform.sizeDelta = new Vector2(40f, 40f);

        // ── 맨 아래: 조작키 안내
        M(NewImage("FooterLine", c, null, MintLine).rectTransform, 80, 1042, 1840, 1.5f);
        var back = NewImage("BackButton", c, null, new Color(0f, 0f, 0f, 0f));
        back.raycastTarget = true;
        M(back.rectTransform, 76, 1060, 110, 32);
        var backButton = back.gameObject.AddComponent<Button>();
        backButton.transition = Selectable.Transition.None;
        KeyCap(back.transform, art, "ESC", 4 / K, 4 / K, 42, true);
        var backLabel = NewText("Label", back.transform, art.titleFont, "뒤로", 16, TextDim, TextAlignmentOptions.Left);
        Inside(backLabel.rectTransform, 56, 4, 60, 24);
        KeyCap(c, art, "↑", 195, 1064, 26);
        KeyCap(c, art, "↓", 233, 1064, 26);
        Hint(c, art, "곡 선택", 270, 1062);
        KeyCap(c, art, "←", 352, 1064, 30);
        KeyCap(c, art, "→", 393, 1064, 30);
        Hint(c, art, "난이도", 434, 1062);
        KeyCap(c, art, "TAB", 512, 1064, 42);
        var sortLabel = Hint(c, art, "정렬 · 기본", 565, 1062);
        KeyCap(c, art, "ENTER", 1803, 1064, 60);
        Hint(c, art, "플레이", 1874, 1062);

        // ── 배속·싱크 팝업 (목업)
        var popup = NewImage("OptionPopup", c, art.chip, new Color(0.03f, 0.08f, 0.08f, 0.95f));
        M(popup.rectTransform, 1355, 92, 260, 112);
        var popupLine = NewImage("Line", popup.transform, art.chip, new Color(Mint.r, Mint.g, Mint.b, 0.0f));
        Stretch(popupLine.rectTransform);
        var popupTitle = NewText("Title", popup.transform, art.titleFont, "배속", 15, TextDim, TextAlignmentOptions.Center);
        Inside(popupTitle.rectTransform, 0, 8, 260, 24);
        var popupValue = NewText("Value", popup.transform, art.titleFont, "8.5", 34, TextMain, TextAlignmentOptions.Center);
        popupValue.fontStyle = FontStyles.Italic;
        Inside(popupValue.rectTransform, 60, 34, 140, 44);
        var left = ArrowButton(popup.transform, art, "Left", art.arrowLeft, 18);
        var right = ArrowButton(popup.transform, art, "Right", art.arrowRight, 202);
        var popupHint = NewText("Hint", popup.transform, art.monoFont, "←→ 조절 · ESC 닫기", 12, TextDim, TextAlignmentOptions.Center);
        Inside(popupHint.rectTransform, 0, 84, 260, 20);
        popup.gameObject.SetActive(false);

        // ── 화면 전체 스캔라인 (1px 선, 3px 간격)
        var scanGo = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
        scanGo.transform.SetParent(c, false);
        var scan = scanGo.GetComponent<RawImage>();
        Stretch(scan.rectTransform);
        scan.texture = art.scanline;
        scan.uvRect = new Rect(0f, 0f, 1920f / 4f, 1080f / 3f);
        scan.color = new Color(0f, 0f, 0f, 0.14f);
        scan.raycastTarget = false;

        // ── 컨트롤러 연결
        var so = new SerializedObject(controller);
        Set(so, "rowTemplate", row);
        Set(so, "listContent", content);
        Set(so, "listScroll", scroll);
        Set(so, "trackCountText", trackCount);
        Set(so, "modeText", null);
        Set(so, "coverImage", cover);
        Set(so, "coverStage", coverStage);
        Set(so, "noCoverTexture", art.noCover);
        Set(so, "infoText", info);
        Set(so, "connector", connector);
        Set(so, "orbitSpinner", null);
        Set(so, "spectrumRoot", spectrum);
        Set(so, "progressRing", progress);
        Set(so, "progressDot", progressDot.rectTransform);
        Set(so, "orbitDotA", dotA.rectTransform);
        Set(so, "orbitDotB", dotB.rectTransform);
        SetArray(so, "pulseGraphics", new Object[] { beam, pedGlow, pedOuter, pedInner });
        SetArray(so, "glitchSlices", new Object[] { sliceMint.rectTransform, sliceRed.rectTransform });
        Set(so, "dataText", data);
        Set(so, "previewTimeText", previewTime);
        SetArray(so, "diffTabs", tabs.ToArray());
        SetArray(so, "diffNames", tabNames.ToArray());
        SetArray(so, "diffLevels", tabLevels.ToArray());
        Set(so, "bestLabel", bestLabel);
        Set(so, "bestScore", bestScore);
        Set(so, "bestRank", bestRank);
        Set(so, "fullComboTag", fc.gameObject);
        Set(so, "speedButton", speed);
        Set(so, "syncButton", sync);
        Set(so, "settingsButton", settings);
        Set(so, "speedLabel", speedLabel);
        Set(so, "syncLabel", syncLabel);
        Set(so, "optionPopup", popup.gameObject);
        Set(so, "optionPopupRect", popup.rectTransform);
        Set(so, "optionTitle", popupTitle);
        Set(so, "optionValue", popupValue);
        Set(so, "optionLeft", left);
        Set(so, "optionRight", right);
        Set(so, "sortLabel", sortLabel);
        Set(so, "playButton", play);
        Set(so, "backButton", backButton);
        Set(so, "refreshButton", null);
        so.FindProperty("gameplaySceneName").stringValue = "RhythmScene_v2";
        // 궤도 점·진행 점이 그림 속 고리 위를 지나도록 크기를 맞춘다
        so.FindProperty("orbitRadiusA").vector2Value = new Vector2(334f, 114f);
        so.FindProperty("orbitTiltA").floatValue = -18f;
        so.FindProperty("orbitRadiusB").vector2Value = new Vector2(292f, 92f);
        so.FindProperty("orbitTiltB").floatValue = 14f;
        so.FindProperty("progressRadius").floatValue = 392f * 0.5f * 250f / 256f;
        so.FindProperty("spectrumRadius").floatValue = 204f;
        so.ApplyModifiedPropertiesWithoutUndo();
        _ = orbitA;
    }

    private static SongSelectRow BuildRowTemplate(RectTransform content, Art art)
    {
        // 줄 자체: 투명한 클릭 판 + 높이 (평소 83, 선택 115)
        var rootImg = NewImage("Row Template", content, null, new Color(0f, 0f, 0f, 0f));
        rootImg.raycastTarget = true;
        var le = rootImg.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 83f;
        le.minHeight = 83f;

        // 튀어나오는 내용물
        var body = NewRect("Body", rootImg.transform);
        Stretch(body);

        var glow = NewImage("Glow", body, art.rowGlow, new Color(Mint.r, Mint.g, Mint.b, 0f));
        StretchInside(glow.rectTransform, -16, -16, -16, -16);
        var bg = NewImage("Glass", body, art.glassRow, Mint);
        Stretch(bg.rectTransform);
        var scanGo = new GameObject("Scan", typeof(RectTransform), typeof(RawImage));
        scanGo.transform.SetParent(body, false);
        var scan = scanGo.GetComponent<RawImage>();
        StretchInside(scan.rectTransform, 3, 3, 3, 3);
        scan.texture = art.scanline;
        scan.uvRect = new Rect(0f, 0f, 220f, 40f);
        scan.color = new Color(Mint.r, Mint.g, Mint.b, 0.07f);
        scan.raycastTarget = false;

        var bar = NewImage("SelectedBar", body, null, Mint);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.sizeDelta = new Vector2(4f, -6f);
        bar.rectTransform.anchoredPosition = Vector2.zero;

        var number = NewText("Number", body, art.monoFont, "01", 15, TextMain, TextAlignmentOptions.Left);
        AnchorLeft(number.rectTransform, 24, 40, 30);

        var cover = new GameObject("Cover", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        cover.rectTransform.SetParent(body, false);
        AnchorLeft(cover.rectTransform, 70, 62, 62);
        cover.texture = art.noCover;
        cover.raycastTarget = false;

        // 곡 이름 (선택되면 빨강·하늘 그림자가 좌우로 벌어진다)
        var titleRed = NewText("TitleRed", body, art.titleFont, "Song Title", 28, new Color(Red.r, Red.g, Red.b, 0.65f), TextAlignmentOptions.Left);
        TopLeft(titleRed.rectTransform, 150, 10, 520, 40);
        var titleCyan = NewText("TitleCyan", body, art.titleFont, "Song Title", 28, new Color(Cyan.r, Cyan.g, Cyan.b, 0.65f), TextAlignmentOptions.Left);
        TopLeft(titleCyan.rectTransform, 150, 10, 520, 40);
        var title = NewText("Title", body, art.titleFont, "Song Title", 28, TextMain, TextAlignmentOptions.Left);
        TopLeft(title.rectTransform, 150, 10, 520, 40);
        var subText = NewText("Sub", body, art.monoFont, "BPM 150", 13, TextDim, TextAlignmentOptions.Left);
        subText.characterSpacing = 10f;
        subText.richText = true;
        TopLeft(subText.rectTransform, 151, 50, 300, 20);

        var wave = NewRect("Wave", body);
        TopLeft(wave, 151, 78, 380, 24);

        var rank = NewText("Rank", body, art.titleFont, "S", 32, TextMain, TextAlignmentOptions.Right);
        rank.fontStyle = FontStyles.Italic;
        rank.rectTransform.anchorMin = rank.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rank.rectTransform.pivot = new Vector2(1f, 0.5f);
        rank.rectTransform.anchoredPosition = new Vector2(-48f, -2f);
        rank.rectTransform.sizeDelta = new Vector2(60f, 44f);

        var lamp = NewImage("Lamp", body, null, Color.gray);
        lamp.rectTransform.anchorMin = lamp.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        lamp.rectTransform.pivot = new Vector2(1f, 0.5f);
        lamp.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
        lamp.rectTransform.sizeDelta = new Vector2(7f, 40f);

        // 재생 배지 (선택된 줄 오른쪽 위)
        var badge = NewImage("PlayBadge", body, null, Mint);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 1f);
        badge.rectTransform.pivot = new Vector2(1f, 1f);
        badge.rectTransform.anchoredPosition = new Vector2(-24f, -14f);
        badge.rectTransform.sizeDelta = new Vector2(160f, 24f);
        float[] bh = { 6f, 11f, 8f, 13f };
        for (int i = 0; i < bh.Length; i++)
        {
            var b = NewImage("Bar" + i, badge.transform, null, Ink);
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            b.rectTransform.pivot = new Vector2(0f, 0.5f);
            b.rectTransform.anchoredPosition = new Vector2(10f + i * 4.5f, 0f);
            b.rectTransform.sizeDelta = new Vector2(2.5f, bh[i]);
        }
        var badgeText = NewText("Time", badge.transform, art.monoFont, "00:00 / 00:30", 13, Ink, TextAlignmentOptions.Left);
        badgeText.characterSpacing = 6f;
        StretchInside(badgeText.rectTransform, 36, 0, 4, 0);

        var rowComp = rootImg.gameObject.AddComponent<SongSelectRow>();
        var so = new SerializedObject(rowComp);
        Set(so, "background", bg);
        Set(so, "body", body);
        Set(so, "numberText", number);
        Set(so, "cover", cover);
        Set(so, "titleText", title);
        Set(so, "titleRed", titleRed);
        Set(so, "titleCyan", titleCyan);
        Set(so, "subText", subText);
        Set(so, "rankText", rank);
        Set(so, "lamp", lamp);
        Set(so, "selectedMark", bar.gameObject);
        Set(so, "glow", glow);
        Set(so, "waveRoot", wave);
        Set(so, "badge", badge.gameObject);
        Set(so, "badgeText", badgeText);
        Set(so, "layout", le);
        Set(so, "normalSprite", art.glassRow);
        Set(so, "selectedSprite", art.glassRowSelected);
        so.FindProperty("normalColor").colorValue = Mint;
        so.FindProperty("hoverColor").colorValue = new Color(0.85f, 1f, 0.96f, 1f);
        so.FindProperty("selectedColor").colorValue = Mint;
        so.ApplyModifiedPropertiesWithoutUndo();
        rootImg.gameObject.SetActive(false);
        return rowComp;
    }

    // 상단 편의 버튼: [아이콘 이름 값 [키]]
    private static Button OptionChip(Transform parent, Art art, string name, Sprite icon, string label, string value, string key,
        float x, float w, out TextMeshProUGUI valueText)
    {
        var bg = NewImage(name, parent, art.chip, new Color(0.04f, 0.10f, 0.10f, 0.7f));
        bg.raycastTarget = true;
        M(bg.rectTransform, x, 35, w, 45);
        var line = NewImage("Line", bg.transform, null, new Color(Mint.r, Mint.g, Mint.b, 0.35f));
        line.rectTransform.anchorMin = new Vector2(0f, 0f);
        line.rectTransform.anchorMax = new Vector2(1f, 0f);
        line.rectTransform.sizeDelta = new Vector2(-16f, 1.5f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        var button = bg.gameObject.AddComponent<Button>();
        SetButtonColors(button);

        var ic = NewImage("Icon", bg.transform, icon, TextMain);
        AnchorLeft(ic.rectTransform, 18, 22, 22);
        var l = NewText("Label", bg.transform, art.titleFont, label, 16, TextMain, TextAlignmentOptions.Left);
        AnchorLeft(l.rectTransform, 48, 50, 30);
        valueText = NewText("Value", bg.transform, art.titleFont, value, 16, TextMain, TextAlignmentOptions.Left);
        AnchorLeft(valueText.rectTransform, 92, 80, 30);
        var keyBox = NewImage("Key", bg.transform, art.keyBox, new Color(TextDim.r, TextDim.g, TextDim.b, 0.8f));
        keyBox.rectTransform.anchorMin = keyBox.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        keyBox.rectTransform.pivot = new Vector2(1f, 0.5f);
        keyBox.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        keyBox.rectTransform.sizeDelta = new Vector2(key.Length > 2 ? 40f : 32f, 22f);
        var kt = NewText("Text", keyBox.transform, art.monoFont, key, 12, TextDim, TextAlignmentOptions.Center);
        Stretch(kt.rectTransform);
        return button;
    }

    private static void KeyCap(Transform parent, Art art, string key, float x, float y, float w, bool local = false)
    {
        var box = NewImage("Key " + key, parent, art.keyBox, new Color(TextDim.r, TextDim.g, TextDim.b, 0.85f));
        if (local) Place(box.rectTransform, x * K, y * K, w * K, 22f * K);
        else M(box.rectTransform, x, y, w, 22);
        var t = NewText("Text", box.transform, art.monoFont, key, 12, TextMain, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
    }

    private static TextMeshProUGUI Hint(Transform parent, Art art, string text, float x, float y)
    {
        var t = NewText("Hint " + text, parent, art.titleFont, text, 16, TextDim, TextAlignmentOptions.Left);
        M(t.rectTransform, x, y, 200, 28);
        return t;
    }

    private static Button ArrowButton(Transform parent, Art art, string name, Sprite icon, float x)
    {
        var img = NewImage(name, parent, art.keyBox, new Color(Mint.r, Mint.g, Mint.b, 0.6f));
        img.raycastTarget = true;
        Inside(img.rectTransform, x, 38, 40, 36);
        var ic = NewImage("Icon", img.transform, icon, TextMain);
        StretchInside(ic.rectTransform, 9, 7, 9, 7);
        var b = img.gameObject.AddComponent<Button>();
        SetButtonColors(b);
        return b;
    }

    // 기울어진 타원 고리 (반지름 rx, ry, 기울기 tilt도)
    private static RectTransform Ellipse(RectTransform parent, string name, Sprite ring, Color color, float rx, float ry, float tilt)
    {
        var holder = NewRect(name, parent);
        Center(holder, 0, 0, rx * 2f, rx * 2f);
        holder.localEulerAngles = new Vector3(0f, 0f, tilt);
        var img = NewImage("Ring", holder, ring, color);
        Stretch(img.rectTransform);
        img.rectTransform.localScale = new Vector3(1f, ry / rx, 1f);
        return holder;
    }

    private static void SetButtonColors(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError("[새 곡 선택 화면] '" + field + "' 칸을 찾지 못했어요."); return; }
        p.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject so, string field, Object[] values)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError("[새 곡 선택 화면] '" + field + "' 칸을 찾지 못했어요."); return; }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    private static void EnsureCamera()
    {
        if (Object.FindFirstObjectByType<Camera>()) return;
        var go = new GameObject("UI Camera", typeof(Camera));
        Undo.RegisterCreatedObjectUndo(go, "새 곡 선택 화면");
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        go.tag = "MainCamera";
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>()) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "새 곡 선택 화면");
    }

    private static void AddScenesToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>();
        // 옮긴 씬 경로가 옛 경로로 남아 있으면 GUID로 새 경로를 찾아 고친다
        foreach (var s in EditorBuildSettings.scenes)
        {
            string path = s.path;
            if (!File.Exists(path))
            {
                string moved = AssetDatabase.GUIDToAssetPath(s.guid.ToString());
                if (!string.IsNullOrEmpty(moved) && File.Exists(moved)) path = moved;
            }
            list.Add(new EditorBuildSettingsScene(path, s.enabled));
        }
        foreach (var path in V2Scenes)
        {
            if (!File.Exists(path)) continue;
            if (list.Exists(s => s.path == path)) continue;
            list.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ── UI 도우미 ──

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.sprite = sprite;
        image.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        tmp.rectTransform.SetParent(parent, false);
        if (font) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size * K;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 부모 안쪽에 왼·위·오른·아래 여백을 두고 꽉 채운다
    private static void StretchInside(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // 캔버스 왼쪽 위 기준 (x, y)에 w x h (캔버스 픽셀)
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 시안 좌표로 놓기 (왼쪽 위 기준)
    private static void M(RectTransform rt, float x, float y, float w, float h) => Place(rt, x * K, y * K, w * K, h * K);

    // 시안 좌표로 가운데 놓기
    private static void MCenter(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx * K, -cy * K);
        rt.sizeDelta = new Vector2(w * K, h * K);
    }

    private static void MC(RectTransform rt, float cx, float cy, float w, float h) => MCenter(rt, cx, cy, w, h);

    // 부모 가운데 기준 (캔버스 픽셀, y는 위가 +)
    private static void Center(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 왼쪽 위 기준 (캔버스 픽셀)
    private static void Inside(RectTransform rt, float x, float y, float w, float h) => Place(rt, x, y, w, h);
    private static void TopLeft(RectTransform rt, float x, float y, float w, float h) => Place(rt, x, y, w, h);

    // 줄 왼쪽에서 x만큼 떨어진 곳, 세로 가운데
    private static void AnchorLeft(RectTransform rt, float x, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ── 머티리얼 ──

    private static Material MakeMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat) return mat;
        var shader = Shader.Find(ShaderName);
        if (!shader)
        {
            Debug.LogWarning("[새 곡 선택 화면] 홀로그램 셰이더(" + ShaderName + ")를 못 찾아서 커버를 그냥 그림으로 띄워요.");
            return null;
        }
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    // ── 임시 그림 만들기 ──
    // 테두리·버튼은 흰색으로 만들고 색은 Image Color로 입힌다.

    private static void MakeArt()
    {
        Directory.CreateDirectory(Folder);

        // 배경이 없으면 어두운 청록 그라데이션 (재이 일러스트로 바꿀 자리)
        WriteColorIfMissing(BackgroundPath, 480, 270, (x, y) =>
        {
            float u = x / 479f, v = y / 269f;
            float vignette = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.55f, 0.55f)) * 1.1f);
            float k = 0.25f + 0.75f * vignette;
            return new Color(0.05f * k, 0.16f * k, 0.16f * k, 1f);
        });

        // 유리 줄: 오른쪽 위·왼쪽 아래가 사선으로 잘린 판. 안은 왼쪽 0.10 → 오른쪽 0.03, 테두리 0.28
        WriteIfMissing(GlassRowPath, 128, 64, (x, y) => Glass(x, y, 0.10f, 0.03f, 0.28f, 1.2f));
        WriteIfMissing(GlassRowSelectedPath, 128, 64, (x, y) => Glass(x, y, 0.22f, 0.07f, 0.75f, 1.8f));
        // 선택된 줄 바깥 빛
        WriteIfMissing(RowGlowPath, 128, 96, (x, y) =>
        {
            float d = CutBox(x + 0.5f - 64f, y + 0.5f - 48f, 64f - 16f, 48f - 16f, 14f);
            return d < 0f ? 0f : Mathf.Exp(-d / 6f) * 0.8f;
        });
        // 평행사변형 (난이도 탭·플레이 버튼): 위가 오른쪽으로 18px 밀린 모양
        WriteIfMissing(ParaPath, 96, 64, (x, y) =>
        {
            float skew = 18f * (y / 63f); // 아래(y=0)는 그대로, 위(y=63)는 18px 오른쪽
            float left = skew, right = 96f - 18f + skew;
            float px = x + 0.5f;
            return Smooth(Mathf.Min(px - left, right - px));
        });
        // 편의 버튼: 왼쪽 위·오른쪽 아래가 잘린 판
        WriteIfMissing(ChipPath, 64, 64, (x, y) => Smooth(-Chamfer(x, y, 64, 64, 10f, 10f)));
        // 키 상자: 테두리만
        WriteIfMissing(KeyBoxPath, 32, 32, (x, y) =>
        {
            float d = Mathf.Min(Mathf.Min(x + 0.5f, 32f - x - 0.5f), Mathf.Min(y + 0.5f, 32f - y - 0.5f));
            return Smooth(1.6f - d) * 0.9f + 0.08f;
        });

        // 커버 둘레 빛
        WriteIfMissing(RingPath, 256, 256, (x, y) =>
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(128f, 128f)) / 128f;
            float line = Mathf.Exp(-Mathf.Pow((r - 0.885f) * 70f, 2f));
            float halo = Mathf.Exp(-Mathf.Pow((r - 0.89f) * 14f, 2f)) * 0.35f;
            return Mathf.Max(line, halo);
        });
        // 얇은 원 (진행 링·궤도·받침). 선은 반지름의 98% 위치
        WriteIfMissing(ThinRingPath, 512, 512, (x, y) =>
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(256f, 256f));
            return Smooth(1.6f - Mathf.Abs(r - 250f));
        });
        // 점선 원 (레드 궤도)
        WriteIfMissing(DashRingPath, 512, 512, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - new Vector2(256f, 256f);
            float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg + 180f;
            bool on = Mathf.Repeat(ang, 6f) < 3f;
            return on ? Smooth(1.5f - Mathf.Abs(p.magnitude - 250f)) : 0f;
        });
        // 부드러운 빛 점
        WriteIfMissing(DotPath, 64, 64, (x, y) =>
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
            return Mathf.Clamp01(Mathf.Exp(-r * r * 4f) * 1.2f - 0.05f) * Mathf.Clamp01((1f - r) * 6f);
        });
        // 빛 기둥: 위가 넓고 아래가 좁은 사다리꼴, 아래가 진하다
        WriteIfMissing(BeamPath, 128, 128, (x, y) =>
        {
            float v = y / 127f; // 0 아래 ~ 1 위
            float half = Mathf.Lerp(0.30f, 0.5f, v) * 128f;
            float edge = Mathf.Clamp01((half - Mathf.Abs(x + 0.5f - 64f)) / 6f);
            return edge * Mathf.Lerp(1f, 0f, v) * Mathf.Lerp(1f, 0.5f, v);
        });
        // 오른쪽(목록 쪽)을 더 어둡게
        WriteIfMissing(ShadePath, 256, 4, (x, y) => Mathf.Lerp(0.05f, 0.55f, Mathf.SmoothStep(0f, 1f, (x / 255f - 0.25f) / 0.6f)));
        WriteIfMissing(VignettePath, 256, 256, (x, y) =>
        {
            float dx = (x / 255f - 0.5f) * 2f, dy = (y / 255f - 0.5f) * 2f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.45f, Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f)));
        });
        // 스캔라인 한 칸: 3줄 중 1줄
        WriteColorIfMissing(ScanlinePath, 4, 3, (x, y) => new Color(1f, 1f, 1f, y == 2 ? 1f : 0f));

        // 아이콘
        WriteIfMissing(WavePath, 64, 64, (x, y) =>
        {
            float[] heights = { 0.28f, 0.55f, 0.9f, 0.55f, 0.28f };
            for (int i = 0; i < heights.Length; i++)
            {
                float cx = 12f + i * 10f;
                if (Mathf.Abs(x + 0.5f - cx) < 2.2f && Mathf.Abs(y + 0.5f - 32f) < heights[i] * 30f) return 1f;
            }
            return 0f;
        });
        WriteIfMissing(ArrowLeftPath, 64, 64, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(Segment(p, new Vector2(40, 54), new Vector2(20, 32)), Segment(p, new Vector2(20, 32), new Vector2(40, 10)));
            return Smooth(3.5f - d);
        });
        WriteIfMissing(ArrowRightPath, 64, 64, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(Segment(p, new Vector2(6, 32), new Vector2(56, 32)),
                Mathf.Min(Segment(p, new Vector2(56, 32), new Vector2(40, 48)), Segment(p, new Vector2(56, 32), new Vector2(40, 16))));
            return Smooth(2.8f - d);
        });
        // 배속: 반원 계기판 + 바늘
        WriteIfMissing(GaugePath, 64, 64, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - new Vector2(32f, 22f);
            float arc = p.y >= 0f ? Mathf.Abs(p.magnitude - 22f) : 99f;
            float needle = Segment(p, Vector2.zero, new Vector2(12f, 14f));
            return Smooth(3f - Mathf.Min(arc, needle));
        });
        // 싱크: 심전도 같은 맥박 선
        WriteIfMissing(PulsePath, 64, 64, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Vector2[] pts = { new Vector2(4, 32), new Vector2(20, 32), new Vector2(27, 50), new Vector2(35, 12), new Vector2(42, 32), new Vector2(60, 32) };
            float d = 99f;
            for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, Segment(p, pts[i], pts[i + 1]));
            return Smooth(2.8f - d);
        });
        // 설정: 톱니
        WriteIfMissing(GearPath, 64, 64, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - new Vector2(32f, 32f);
            float ang = Mathf.Atan2(p.y, p.x);
            float outer = 20f + (Mathf.Cos(ang * 8f) > 0.3f ? 6f : 0f);
            float ring = Mathf.Max(p.magnitude - outer, 8f - p.magnitude);
            return Smooth(-ring);
        });

        // 커버 없는 곡용 그림: 겹겹이 산 능선 (홀로그램 효과가 잘 보이도록 명암이 있는 그림)
        WriteColorIfMissing(NoCoverPath, 256, 256, (x, y) =>
        {
            float u = x / 255f, v = y / 255f;
            float sky = 0.25f + 0.55f * v;
            float shade = sky;
            for (int layer = 0; layer < 4; layer++)
            {
                float baseH = 0.62f - layer * 0.14f;
                float h = baseH + 0.10f * Mathf.PerlinNoise(u * (3f + layer * 2f) + layer * 10f, layer * 3.1f)
                          + 0.05f * Mathf.PerlinNoise(u * 14f + layer, 7.3f + layer);
                if (v < h)
                {
                    float ridge = Mathf.Clamp01((h - v) * 12f);
                    shade = Mathf.Lerp(0.85f - layer * 0.12f, 0.15f + layer * 0.05f, ridge)
                            * (0.8f + 0.2f * Mathf.PerlinNoise(u * 40f, v * 40f));
                }
            }
            float moon = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.7f, 0.82f)) * 9f);
            shade = Mathf.Max(shade, moon);
            return new Color(shade, shade, shade, 1f);
        });

        AssetDatabase.Refresh();
    }

    // 유리 판 한 픽셀 (텍스처 y는 아래가 0)
    private static float Glass(int x, int y, float fillLeft, float fillRight, float border, float borderWidth)
    {
        float px = x + 0.5f - 64f, py = y + 0.5f - 32f;
        float d = CutBox(px, py, 63f, 31f, 14f);
        if (d > 0.5f) return 0f;
        float fill = Mathf.Lerp(fillLeft, fillRight, x / 127f) * Smooth(-d);
        float line = Smooth(borderWidth - Mathf.Abs(d + borderWidth)) * border;
        return Mathf.Max(fill, line);
    }

    // 오른쪽 위·왼쪽 아래 모서리를 cut만큼 사선으로 자른 상자까지의 거리 (안쪽이 음수, 가운데 기준 좌표)
    private static float CutBox(float px, float py, float hw, float hh, float cut)
    {
        float d = Mathf.Max(Mathf.Abs(px) - hw, Mathf.Abs(py) - hh);
        const float s = 0.70710678f;
        d = Mathf.Max(d, ((px + py) - (hw + hh - cut)) * s);     // 오른쪽 위
        d = Mathf.Max(d, ((-px - py) - (hw + hh - cut)) * s);    // 왼쪽 아래
        return d;
    }

    private static void WriteIfMissing(string path, int w, int h, System.Func<int, int, float> alpha)
    {
        WriteColorIfMissing(path, w, h, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y))));
    }

    private static void WriteColorIfMissing(string path, int w, int h, System.Func<int, int, Color> pixel)
    {
        if (File.Exists(path)) return;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // 왼쪽 위(cutTL)·오른쪽 아래(cutBR) 모서리를 깎은 사각형까지의 거리 (안쪽이 음수)
    private static float Chamfer(int x, int y, int w, int h, float cutTL, float cutBR)
    {
        float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
        float hw = w * 0.5f - 1f, hh = h * 0.5f - 1f;
        float d = Mathf.Max(Mathf.Abs(px) - hw, Mathf.Abs(py) - hh);
        const float s = 0.70710678f;
        if (cutBR > 0f) d = Mathf.Max(d, ((px - py) - (hw + hh - cutBR)) * s);
        if (cutTL > 0f) d = Mathf.Max(d, ((py - px) - (hw + hh - cutTL)) * s);
        return d;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float Smooth(float v) => Mathf.Clamp01(v + 0.5f);

    private static Sprite ImportSprite(string path, Vector4 border, int maxSize = 0)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        // 처음 한 번만 설정한다. 재이가 그림을 바꾸고 직접 고친 Border는 건드리지 않는다
        bool first = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single;
        bool needBorder = border != Vector4.zero && importer.spriteBorder == Vector4.zero;
        bool needSize = maxSize > 0 && importer.maxTextureSize != maxSize;
        if (first || needBorder || needSize)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (needBorder) importer.spriteBorder = border;
            if (maxSize > 0) importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D ImportTexture(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        if (importer.textureType != TextureImporterType.Default || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 반복해서 까는 무늬 (스캔라인)
    private static Texture2D ImportRepeatTexture(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        if (importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
