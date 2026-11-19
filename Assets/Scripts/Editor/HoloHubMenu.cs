using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 에디터 상단 메뉴 Tools > CapDemo > 허브 > HoloHubScene 만들기 (홀로그램 목업)
// 확정 시안(UI목업_v4_1: RE:WRITE 메인 허브) + RADIO 7 홀로그램 바를 새 씬 Assets/Scenes/HoloHubScene.unity로 조립한다.
// 숫자·문구는 전부 가짜 값(목업)이다. 기존 HubScene, NewHubDemoScene은 건드리지 않는다.
// 다시 실행하면 씬을 처음부터 새로 만들므로, 손으로 다듬은 뒤에는 실행하지 않는다.
// 좌표는 목업 그림(2000 x 1125) 기준 픽셀이고, 1920 x 1080 캔버스에 맞게 0.96배 한다.
public static class HoloHubMenu
{
    private const string ScenePath = "Assets/Scenes/HoloHubScene.unity";
    private const string ArtRoot = "Assets/Art/UI/HoloHub/";
    private const string BgSource = "main_hub/hub_A_s2001.png"; // 프로젝트 폴더 기준 (Assets 바깥)
    private const string BgPath = ArtRoot + "hub_bg.png";
    private const string BgFolder = ArtRoot + "Backgrounds/"; // 허브 배경으로 돌려 쓸 그림을 넣는 폴더
    private const string TitleFontPath = "Assets/Art/Fonts/경기천년제목_Bold SDF.asset";
    private const string MonoFontPath = "Assets/Art/Fonts/neodgm SDF.asset";
    private const string BodyFontPath = "Assets/Art/Fonts/BMDOHYEON_ttf SDF.asset";
    private const string ShaderPath = "Assets/Art/Shaders/HoloTransition.shader";
    private const string TransitionMatPath = ArtRoot + "HoloTransition.mat";
    private const string SettingsPath = "Assets/Resources/HoloTransitionSettings.asset";
    // 2026-11-18 재이 결정: 전환 소리는 꺼짐(Hologram_Off) 하나만. 켜짐 소리는 없음
    private const string OffSfxPath = "Assets/Audio/SFX/Hologram_Off.mp3";
    private const string OnSfxPath = "";
    private const string SongSelectScene = "SongSelectScene_v2";
    private const string StoryScene = "TutoScene"; // 지금은 스토리 모드 = 튜토리얼 챕터(0장)
    private const string GameTitle = "RE:WRITE";

    private const float K = 0.96f;            // 목업 픽셀 → 캔버스 픽셀
    private const float MockW = 2000f, MockH = 1125f;

    private static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0);
    private static readonly Vector2 ML = new Vector2(0, 0.5f), MR = new Vector2(1, 0.5f), MC = new Vector2(0.5f, 0.5f), BottomLeft = new Vector2(0, 0);

    private static TMP_FontAsset titleFont, monoFont, bodyFont;
    private static Material titleGlow, monoGlow, bodyGlow;
    private static Sprite bgSprite;

    // 목업 좌표로 위치를 잡기 위한 상자 (w, h는 목업 픽셀)
    private class Box
    {
        public RectTransform rt;
        public float w, h;
    }

    [MenuItem("Tools/CapDemo/허브/HoloHubScene 만들기 (홀로그램 목업)")]
    private static void CreateScene()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("씬 덮어쓰기",
                $"{ScenePath}를 처음부터 새로 만듭니다.\n손으로 옮긴 위치도 모두 초기화됩니다.", "덮어쓰기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!PrepareAssets()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        BuildCanvas();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToSceneList(ScenePath);
        Debug.Log($"[HoloHub] {ScenePath}를 만들었어요. Play를 누르면 화면이 지직거리며 켜집니다. (싱글 플레이 → {SongSelectScene}로 지직 전환)");
    }

    // ── 재료 준비 ────────────────────────────────────────────────

    private static bool PrepareAssets()
    {
        Directory.CreateDirectory(ArtRoot);

        // 배경 그림을 Assets 안으로 복사 (한 번만)
        if (!File.Exists(BgPath))
        {
            if (!File.Exists(BgSource))
            {
                EditorUtility.DisplayDialog("배경 그림 없음", $"{BgSource} 를 찾지 못했어요.\n프로젝트 폴더의 main_hub 폴더를 확인해 주세요.", "확인");
                return false;
            }
            File.Copy(BgSource, BgPath);
            AssetDatabase.ImportAsset(BgPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(BgPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);

        HoloHubArt.GenerateAll();

        titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFontPath);
        bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        if (!titleFont || !monoFont || !bodyFont)
            Debug.LogWarning("[HoloHub] 폰트 일부를 찾지 못해 기본 폰트로 대신합니다. (경기천년제목_Bold / neodgm / BMDOHYEON SDF)");

        // 한글이 많으면 글자 그림판(아틀라스) 한 장이 꽉 차서 네모(□)로 나온다 → 꽉 차면 새 장을 쓰게 한다
        foreach (var f in new[] { titleFont, monoFont, bodyFont })
        {
            if (!f || f.isMultiAtlasTexturesEnabled) continue;
            f.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(f);
            Debug.Log($"[HoloHub] {f.name}: 글자판 여러 장 쓰기(Multi Atlas Textures)를 켰어요.");
        }

        // 홀로그램 글자용 머티리얼: 하늘색 빛 번짐 + 빨간 그림자(색 번짐)
        titleGlow = GlowMaterial(titleFont, "Holo_TitleGlow", HoloPalette.Holo.WithAlpha(0.55f), -0.6f);
        monoGlow = GlowMaterial(monoFont, "Holo_MonoGlow", HoloPalette.Holo.WithAlpha(0.5f), -0.5f);
        bodyGlow = GlowMaterial(bodyFont, "Holo_BodyGlow", HoloPalette.Holo.WithAlpha(0.45f), -0.5f);

        // 지직 전환 설정 (Resources에 있어야 실행 중에 찾는다)
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TransitionMatPath);
        if (!mat && shader)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, TransitionMatPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        var settings = AssetDatabase.LoadAssetAtPath<HoloTransitionSettings>(SettingsPath);
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<HoloTransitionSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.screenMaterial = mat;
        settings.titleFont = titleFont;
        settings.monoFont = monoFont;
        settings.logoTitle = GameTitle;
        settings.offSound = AssetDatabase.LoadAssetAtPath<AudioClip>(OffSfxPath);
        settings.onSound = string.IsNullOrEmpty(OnSfxPath) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(OnSfxPath);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static Material GlowMaterial(TMP_FontAsset font, string name, Color glow, float underlayX)
    {
        if (!font) return null;
        string path = ArtRoot + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(font.material) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.CopyPropertiesFromMaterial(font.material);

        mat.EnableKeyword("UNDERLAY_ON");
        SetIf(mat, "_UnderlayColor", new Color(1f, 0.27f, 0.43f, 0.45f));
        SetIf(mat, "_UnderlayOffsetX", underlayX);
        SetIf(mat, "_UnderlayOffsetY", 0f);
        SetIf(mat, "_UnderlayDilate", 0f);
        SetIf(mat, "_UnderlaySoftness", 0.15f);
        mat.EnableKeyword("GLOW_ON");
        SetIf(mat, "_GlowColor", glow);
        SetIf(mat, "_GlowOffset", 0f);
        SetIf(mat, "_GlowInner", 0.05f);
        SetIf(mat, "_GlowOuter", 0.4f);
        SetIf(mat, "_GlowPower", 0.85f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetIf(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }
    private static void SetIf(Material m, string prop, Color v) { if (m.HasProperty(prop)) m.SetColor(prop, v); }

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = HoloPalette.Hex(0x071211);
        cam.orthographic = true;
        go.transform.position = new Vector3(0, 0, -10);
    }

    // ── 화면 조립 ────────────────────────────────────────────────

    private static void BuildCanvas()
    {
        var canvasGo = new GameObject("HoloHub", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HoloHubIntro));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = new Box { rt = (RectTransform)canvasGo.transform, w = MockW, h = MockH };

        BuildBackground(root);

        // 화면 모서리마다 붙는 묶음 (화면 비율이 달라져도 각자 자기 모서리에 붙는다)
        var topLeft = Group(root, "TopLeft", TL);
        var topRight = Group(root, "TopRight", TR);
        var bottomLeft = Group(root, "BottomLeft", BL);
        var bottomRight = Group(root, "BottomRight", BR);

        BuildHeader(root, topLeft);
        BuildPlayerCard(topLeft);
        BuildMenu(topLeft);
        BuildLeftBottom(topLeft);
        BuildTopRight(topRight);
        BuildStory(topRight);
        BuildEvent(bottomRight);
        BuildRadio(bottomLeft);
        BuildVersion(bottomRight);
        BuildOverlay(root);
    }

    private static void BuildBackground(Box root)
    {
        var bg = Stretch(root, "Background");

        // 마우스 따라 살짝 움직이도록 화면보다 조금 크게
        var parallax = Stretch(bg, "Parallax", typeof(HoloParallax));
        parallax.rt.offsetMin = new Vector2(-30, -20);
        parallax.rt.offsetMax = new Vector2(30, 20);

        var artBox = Stretch(parallax, "Art", typeof(Image), typeof(AspectRatioFitter), typeof(HoloBackgroundCycle));
        var art = artBox.rt.GetComponent<Image>();
        art.sprite = bgSprite;
        art.raycastTarget = false;
        var fitter = artBox.rt.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;

        // 첫 배경 그림 속 전구 위치에 따뜻한 빛 (그림 1536x864 기준 위치). 다른 배경일 때는 꺼진다.
        var lamps = Stretch(artBox, "Lamps");
        var bulbs = new[] { new Vector2(935, 18), new Vector2(997, 88), new Vector2(1157, 60), new Vector2(1270, 6) };
        foreach (var b in bulbs)
            Lamp(lamps, b.x / 1536f, 1f - b.y / 864f, 230f, new Color(1f, 0.72f, 0.40f, 0.30f), 0.18f);
        Lamp(lamps, 1100f / 1536f, 1f - 60f / 864f, 900f, new Color(1f, 0.62f, 0.32f, 0.10f), 0.06f);

        // 허브에 들어올 때마다 바뀌는 배경 목록: 기본 배경 + Backgrounds 폴더의 그림들
        var entries = new List<object>
        {
            new Dictionary<string, object> { { "image", bgSprite }, { "showLamps", true } }
        };
        foreach (var sprite in LoadExtraBackgrounds())
            entries.Add(new Dictionary<string, object> { { "image", sprite }, { "showLamps", false } });
        var cycle = artBox.rt.GetComponent<HoloBackgroundCycle>();
        Set(cycle, "backgrounds", entries.ToArray());
        Set(cycle, "lamps", lamps.rt.gameObject);
        Debug.Log($"[HoloHub] 배경 {entries.Count}장을 돌려 씁니다. (그림 추가: {BgFolder} 폴더에 넣고 메뉴 다시 실행, 또는 Art 오브젝트의 Backgrounds 목록에 직접 추가)");

        // 조명 아래 떠다니는 먼지
        var dustBox = Node("Dust", bg, 1100, 0, 900, 760, TR, TR, typeof(HoloDust));
        Set(dustBox.rt.GetComponent<HoloDust>(), "sprite", HoloHubArt.Load("glow"));

        // 왼쪽(메뉴 뒤)·위·아래 어둡게
        var shadeColor = new Color(4 / 255f, 12 / 255f, 12 / 255f, 1f);
        var left = Img(root, "ShadeLeft", 0, 0, 1100, MockH, "grad_left", shadeColor);
        left.rectTransform.anchorMin = new Vector2(0, 0);
        left.rectTransform.anchorMax = new Vector2(0, 1);
        left.rectTransform.pivot = new Vector2(0, 0.5f);
        left.rectTransform.anchoredPosition = Vector2.zero;
        left.rectTransform.sizeDelta = new Vector2(1100 * K, 0);

        var top = Img(root, "ShadeTop", 0, 0, MockW, 180, "grad_top", shadeColor.WithAlpha(0.65f));
        HStretchEdge(top.rectTransform, true, 180);
        var bottom = Img(root, "ShadeBottom", 0, 0, MockW, 250, "grad_top", shadeColor.WithAlpha(0.8f));
        HStretchEdge(bottom.rectTransform, false, 250);
        bottom.rectTransform.localScale = new Vector3(1, -1, 1);
    }

    // Backgrounds 폴더 안의 그림을 모두 Sprite로 만들어 가져온다 (이름 순)
    private static List<Sprite> LoadExtraBackgrounds()
    {
        Directory.CreateDirectory(BgFolder);
        AssetDatabase.Refresh();
        var result = new List<Sprite>();
        var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { BgFolder.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToList();
        foreach (var path in paths)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp) continue;
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single || imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite) result.Add(sprite);
        }
        return result;
    }

    private static void Lamp(Box art, float u, float v, float size, Color color, float wobble)
    {
        var go = new GameObject("LampGlow", typeof(RectTransform), typeof(Image), typeof(HoloFlicker));
        go.transform.SetParent(art.rt, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(u, v);
        rt.pivot = MC;
        rt.sizeDelta = new Vector2(size * K, size * K);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = HoloHubArt.Load("glow");
        img.color = color;
        img.raycastTarget = false;
        Set(go.GetComponent<HoloFlicker>(), "wobble", wobble);
    }

    private static void BuildHeader(Box root, Box g)
    {
        // 로고: 파형 + OTONE + 장소
        var logo = Node("Logo", g, 80, 30, 640, 45);
        Enter(logo, 0.0f, new Vector2(0, 24));
        var wave = Node("Wave", logo, 0, 6, 38, 33);
        var waveBars = new List<RectTransform>();
        for (int i = 0; i < 7; i++)
        {
            var bar = Img(wave, "Bar" + i, i * 5.5f, 0, 3, 33, null, HoloPalette.Teal, ML, ML);
            waveBars.Add(bar.rectTransform);
        }
        var eq = wave.rt.gameObject.AddComponent<HoloEqualizer>();
        Set(eq, "bars", waveBars.Cast<Object>().ToArray());
        Set(eq, "height", new Vector2(6 * K, 30 * K));
        Set(eq, "speed", 2.2f);

        Txt(logo, "Title", GameTitle, 46, 0, 200, 45, titleFont, 37, HoloPalette.Ink, spacing: 4);
        Img(logo, "Sep", 227, 10, 1.5f, 26, null, HoloPalette.Dim);
        Txt(logo, "Place", "7구역 공업단지  //  지하 라이브하우스 「RUST」", 244, 0, 480, 45, monoFont, 15, HoloPalette.Mute, spacing: 6);

        // 윗줄: 화면 가로로 늘어나고, 빛 조각이 가끔 달려간다
        var lineGo = new GameObject("TopLine", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HoloEntrance));
        lineGo.transform.SetParent(root.rt, false);
        var lineRt = (RectTransform)lineGo.transform;
        lineRt.anchorMin = new Vector2(0, 1);
        lineRt.anchorMax = new Vector2(1, 1);
        lineRt.pivot = new Vector2(0.5f, 0.5f);
        lineRt.offsetMin = new Vector2(80 * K, -100 * K - 0.75f);
        lineRt.offsetMax = new Vector2(-80 * K, -100 * K + 0.75f);
        var lineImg = lineGo.GetComponent<Image>();
        lineImg.color = HoloPalette.Line;
        lineImg.raycastTarget = false;
        Set(lineGo.GetComponent<HoloEntrance>(), "fromOffset", new Vector2(-80, 0));
        var line = new Box { rt = lineRt, w = MockW - 160, h = 1.5f };
        Img(line, "Accent", 0, -1.25f, 400, 4, null, HoloPalette.TealDark, ML, ML);
        var runner = Img(line, "Runner", 0, -2.5f, 180, 6, "sheen", HoloPalette.Teal.WithAlpha(0.9f), ML, ML);
        var sweep = runner.gameObject.AddComponent<HoloSweep>();
        Set(sweep, "travelTime", 2.4f);
        Set(sweep, "pause", new Vector2(2f, 5f));
    }

    private static void BuildPlayerCard(Box g)
    {
        var card = Node("Profile", g, 80, 130, 412, 95, TL, ML);
        Enter(card, 0.06f, new Vector2(-50, 0));
        var bg = Img(card, "Bg", 0, 0, 412, 95, "panel_trbl14", HoloPalette.Panel, raycast: true);
        var line = Img(card, "Line", 0, 0, 412, 95, "panel_trbl14_line", HoloPalette.Line);
        var content = Stretch(card, "Content", typeof(CanvasGroup));

        Img(content, "Hex", 14, 15, 66, 66, "hex", new Color(0.17f, 0.36f, 0.33f, 1f));
        var avatar = Img(content, "Avatar", 14, 15, 66, 66, "avatar", HoloPalette.Teal.WithAlpha(0.85f));
        avatar.gameObject.AddComponent<HoloFlicker>();
        Set(avatar.GetComponent<HoloFlicker>(), "wobble", 0.06f);
        Set(avatar.GetComponent<HoloFlicker>(), "dropChance", 0.08f);

        // 이름: 튜토리얼에서 입력한 플레이어 이름 ({player}를 PlayerNameText가 바꿔 준다). 역할은 이름 바로 뒤에 붙는다.
        var name = Txt(content, "Name", "{player}<size=50%><color=#A3EBD6>   VOCAL / GUITAR</color></size>",
            101, 4, 300, 36, titleFont, 24, HoloPalette.Ink);
        name.gameObject.AddComponent<PlayerNameText>();

        // 클리어한 곡 수를 칸으로 (가짜 값 6 / 8): 앞 4칸 민트, 다음 2칸 주황, 남은 칸은 꺼짐
        var segs = new List<Object>();
        float[] heights = { 5f, 7.5f, 10f, 12.5f, 15f, 17.5f, 17.5f, 17.5f };
        const float segW = 34f, gap = 3.6f;
        for (int i = 0; i < heights.Length; i++)
        {
            var c = i < 4 ? HoloPalette.Teal : i < 6 ? HoloPalette.Amber : HoloPalette.Teal.WithAlpha(0.15f);
            segs.Add(Img(content, "Vu" + i, 101 + i * (segW + gap), 66 - heights[i], segW, heights[i], null, c));
        }

        Txt(content, "Clear", "CLEAR", 101, 68, 80, 18, monoFont, 11, HoloPalette.Dim, spacing: 8);
        Txt(content, "Tracks", "6 / 8 TRACKS", 231, 68, 160, 18, monoFont, 11, HoloPalette.Dim, TextAlignmentOptions.MidlineRight, spacing: 4);

        Hover(card, 1.03f, content.rt, false,
            CT(line, HoloPalette.Teal),
            CT(bg, HoloPalette.Panel2));
    }

    private static void BuildMenu(Box g)
    {
        var header = Node("MenuHeader", g, 80, 275, 400, 20);
        Enter(header, 0.12f, new Vector2(-40, 0));
        Txt(header, "Label", "SET LIST", 0, 0, 90, 20, monoFont, 14, HoloPalette.Teal, spacing: 20);
        Img(header, "Line", 94, 9.5f, 186, 1.5f, null, HoloPalette.Line);
        Txt(header, "Count", "04", 293, 0, 60, 20, monoFont, 12, HoloPalette.Dim);

        var menu = Node("MainMenu", g, 40, 300, 620, 470, TL, TL, typeof(HoloMenu));

        var defs = new[]
        {
            ("01", "싱글 플레이", "SINGLE PLAY", "icon_play", "8곡 수록", SongSelectScene, true, "NOW PLAYING  ·  Moonside Howl"),
            ("02", "스토리 모드", "STORY MODE", "icon_book", null, StoryScene, false, "CH.03  ·  녹슨 무대 위의 노이즈  ·  3/6"),
            ("03", "아카이브", "ARCHIVE", "icon_disc", null, "", false, "ARCHIVE  ·  6 / 8 TRACKS CLEARED"),
            ("04", "설정", "SETTINGS", "icon_sliders", null, "", false, "SYSTEM  ·  AUDIO / DISPLAY / KEY CONFIG"),
        };

        var items = new List<HoloMenuItem>();
        for (int i = 0; i < defs.Length; i++)
        {
            var (no, title, sub, icon, tag, scene, songSelect, caption) = defs[i];
            items.Add(MenuItem(menu, i, no, title, sub, icon, tag, scene, songSelect, caption));
        }

        // 선택된 버튼 왼쪽 막대
        var cursor = Img(menu, "Cursor", 18, 15, 7, 96, null, HoloPalette.Teal, TL, ML);
        Enter(Wrap(cursor), 0.2f, Vector2.zero);

        // 선택된 버튼 오른쪽에서 배경 쪽으로 뻗는 연결선
        var conn = Node("Connector", menu, 564, 23, 260, 80, TL, ML, typeof(CanvasGroup));
        var lineColor = HoloPalette.Teal.WithAlpha(0.75f);
        Img(conn, "Line1", 0, 39.25f, 52, 1.5f, null, lineColor);
        Img(conn, "Dot", 49.5f, 37.5f, 5, 5, null, HoloPalette.Teal);
        var diag = Img(conn, "Diagonal", 77 - 34.65f, 16 - 0.75f, 69.3f, 1.5f, null, lineColor, TL, MC);
        diag.rectTransform.localEulerAngles = new Vector3(0, 0, 43.8f);
        Img(conn, "Line2", 101, -8.75f, 150, 1.5f, null, lineColor);
        var np = Node("NowBars", conn, 112, -32, 16, 14, TL, TL);
        var npBars = new List<Object>();
        for (int i = 0; i < 4; i++)
            npBars.Add(Img(np, "Bar" + i, i * 4f, 14 - 8, 2.5f, 8, null, HoloPalette.Teal, BottomLeft, BottomLeft).rectTransform);
        var npEq = np.rt.gameObject.AddComponent<HoloEqualizer>();
        Set(npEq, "bars", npBars.ToArray());
        Set(npEq, "height", new Vector2(3 * K, 13 * K));
        Set(npEq, "speed", 4f);
        var caption0 = Txt(conn, "Caption", defs[0].Item8, 132, -36, 560, 22, monoFont, 13, HoloPalette.Mute, spacing: 6);
        Enter(conn, 0.55f, new Vector2(-20, 0));

        var holoMenu = menu.rt.GetComponent<HoloMenu>();
        Set(holoMenu, "items", items.Cast<Object>().ToArray());
        Set(holoMenu, "cursor", cursor.rectTransform);
        Set(holoMenu, "connector", conn.rt);
        Set(holoMenu, "connectorGroup", conn.rt.GetComponent<CanvasGroup>());
        Set(holoMenu, "connectorText", caption0);
    }

    private static HoloMenuItem MenuItem(Box menu, int index, string no, string title, string sub, string icon,
        string tag, string scene, bool songSelect, string caption)
    {
        var item = Node("Item_" + sub.Replace(" ", ""), menu, 40, 15 + index * 112, 524, 96, TL, ML);
        var bg = Img(item, "Bg", 0, 0, 524, 96, "panel_tr18", HoloPalette.Panel, raycast: true);
        var line = Img(item, "Line", 0, 0, 524, 96, "panel_tr18_line", HoloPalette.Teal.WithAlpha(0.18f));

        // 민트 위를 훑는 반짝임 (선택됐을 때만)
        var sheenMask = Stretch(item, "SheenMask", typeof(RectMask2D), typeof(CanvasGroup));
        var sheen = Img(sheenMask, "Sheen", 0, 0, 150, 96, "sheen", new Color(1, 1, 1, 0.45f), ML, ML);
        Set(sheen.gameObject.AddComponent<HoloSweep>(), "travelTime", 0.9f);
        Set(sheen.GetComponent<HoloSweep>(), "pause", new Vector2(0.8f, 1.8f));

        var content = Stretch(item, "Content", typeof(CanvasGroup));
        var noText = Txt(content, "No", no, 20, 0, 40, 96, monoFont, 15, HoloPalette.Mute);
        var iconImg = Img(content, "Icon", 76, 30, 36, 36, icon, HoloPalette.Teal);
        var titleText = Txt(content, "Title", title, 130, 12, 330, 46, titleFont, 34, HoloPalette.Ink);
        var subText = Txt(content, "Sub", sub, 131, 56, 300, 20, monoFont, 13, HoloPalette.Dim, spacing: 18);
        var arrow = Img(content, "Arrow", 476, 37, 22, 22, "icon_chevron", HoloPalette.Dim);

        var shows = new List<Object> { sheenMask.rt.GetComponent<CanvasGroup>() };
        if (!string.IsNullOrEmpty(tag))
        {
            var tagBox = Node("Tag", content, 360, 13, 100, 21, TL, TL, typeof(CanvasGroup));
            Img(tagBox, "Bg", 0, 0, 100, 21, null, HoloPalette.DarkInk);
            Txt(tagBox, "Text", tag, 0, 0, 100, 21, monoFont, 11, HoloPalette.Teal, TextAlignmentOptions.Center, spacing: 12);
            shows.Add(tagBox.rt.GetComponent<CanvasGroup>());
        }

        var hover = Hover(item, 1.05f, content.rt, true,
            CT(bg, HoloPalette.Teal.WithAlpha(0.93f)),
            CT(line, HoloPalette.DarkInk.WithAlpha(0.25f)),
            CT(noText, HoloPalette.DarkSub),
            CT(iconImg, HoloPalette.DarkInk),
            CT(titleText, HoloPalette.DarkInk),
            CT(subText, HoloPalette.DarkSub),
            CT(arrow, HoloPalette.DarkInk));
        Set(hover, "showOnHover", shows.ToArray());

        var mi = item.rt.gameObject.AddComponent<HoloMenuItem>();
        Set(mi, "targetScene", scene);
        Set(mi, "opensSongSelect", songSelect);
        Set(mi, "opensStory", scene == StoryScene);
        Set(mi, "caption", caption);
        // 설정 메뉴는 씬 대신 설정 창을 띄운다 (Tools > CapDemo > 설정·일시정지 > 프리팹 만들기로 먼저 만들어 둔다)
        if (sub == "SETTINGS")
        {
            var settingsPanel = AssetDatabase.LoadAssetAtPath<SettingsPanel>(SettingsPauseMenu.SettingsPrefabPath);
            if (settingsPanel) Set(mi, "panelPrefab", settingsPanel);
        }

        Enter(item, 0.18f + index * 0.07f, new Vector2(-70, 0));
        return mi;
    }

    private static void BuildLeftBottom(Box g)
    {
        var exit = Node("Exit", g, 80, 800, 188, 45, TL, ML, typeof(HoloQuitButton));
        var bg = Img(exit, "Bg", 0, 0, 188, 45, "panel_br10", new Color(40 / 255f, 10 / 255f, 14 / 255f, 0.55f), raycast: true);
        var line = Img(exit, "Line", 0, 0, 188, 45, "panel_br10_line", HoloPalette.Red.WithAlpha(0.45f));
        var content = Stretch(exit, "Content", typeof(CanvasGroup));
        var icon = Img(content, "Icon", 18, 13, 19, 19, "icon_exit", new Color(1f, 0.54f, 0.59f));
        var label = Txt(content, "Label", "게임 종료", 48, 0, 90, 45, titleFont, 18, new Color(1f, 0.835f, 0.85f));
        var en = Txt(content, "En", "EXIT", 132, 0, 50, 45, monoFont, 12, new Color(0.76f, 0.42f, 0.45f), spacing: 14);
        Hover(exit, 1.06f, content.rt, false,
            CT(bg, HoloPalette.Red.WithAlpha(0.85f)),
            CT(line, HoloPalette.Red),
            CT(icon, Color.white),
            CT(label, Color.white),
            CT(en, new Color(1, 1, 1, 0.8f)));
        Enter(exit, 0.55f, new Vector2(-40, 0));

        // 왼쪽 아래 꺾쇠
        var br = Node("BracketBL", g, 55, 995, 25, 25);
        Img(br, "V", 0, 0, 1.5f, 25, null, HoloPalette.TealDark);
        Img(br, "H", 0, 23.5f, 25, 1.5f, null, HoloPalette.TealDark);
        Enter(br, 0.6f, Vector2.zero);
    }

    private static void BuildTopRight(Box g)
    {
        HoloCall(g, 1676, 0.15f);

        var clock = Node("Clock", g, 1810, 32, 108, 52, TL, TL, typeof(HoloClock));
        var time = Txt(clock, "Time", "23:47", 0, 0, 108, 26, monoFont, 20, HoloPalette.Ink, TextAlignmentOptions.MidlineRight, spacing: 4);
        var date = Txt(clock, "Date", "MON  ·  NOV 16", -40, 27, 148, 20, monoFont, 13, HoloPalette.Mute, TextAlignmentOptions.MidlineRight, spacing: 4);
        Set(clock.rt.GetComponent<HoloClock>(), "timeText", time);
        Set(clock.rt.GetComponent<HoloClock>(), "dateText", date);
        Enter(clock, 0.25f, new Vector2(0, 24));

        // 오른쪽 위 꺾쇠
        var br = Node("BracketTR", g, 1918, 112, 24, 24);
        Img(br, "H", 0, 0, 24, 1.5f, null, HoloPalette.TealDark);
        Img(br, "V", 22.5f, 0, 1.5f, 24, null, HoloPalette.TealDark);
        Enter(br, 0.3f, Vector2.zero);
    }

    // 홀로콜 버튼 (전화 아이콘 + 글자 + 빨간 배지). 아직 기능은 없어서 누르면 지직거리기만 한다.
    private static void HoloCall(Box g, float x, float delay)
    {
        const float w = 132, h = 44;
        var btn = Node("HoloCall", g, x, 35, w, h, TL, MC);
        var bg = Img(btn, "Bg", 0, 0, w, h, "panel_tlbr8", HoloPalette.Panel, raycast: true);
        var line = Img(btn, "Line", 0, 0, w, h, "panel_tlbr8_line", HoloPalette.Line);
        var content = Stretch(btn, "Content", typeof(CanvasGroup));
        var icon = Img(content, "Icon", 14, 11, 22, 22, "icon_phone", HoloPalette.Teal);
        var label = Txt(content, "Label", "홀로콜", 42, 0, 60, h, titleFont, 17, HoloPalette.Ink);
        var dot = Img(content, "Badge", 101, 13, 18, 18, "dot", HoloPalette.Red);
        Txt(new Box { rt = dot.rectTransform, w = 18, h = 18 }, "Count", "2", 0, 0, 18, 18, monoFont, 11, Color.white, TextAlignmentOptions.Center);
        var pulse = dot.gameObject.AddComponent<HoloPulse>();
        Set(pulse, "alpha", new Vector2(1f, 1f));
        Set(pulse, "scaleAmount", 0.14f);
        Set(pulse, "rate", 0.9f);
        var hover = Hover(btn, 1.07f, content.rt, false,
            CT(line, HoloPalette.Teal), CT(bg, HoloPalette.TealDeep.WithAlpha(0.9f)), CT(icon, Color.white), CT(label, HoloPalette.Teal));
        Set(hover, "glitchOnClick", true);
        Enter(btn, delay, new Vector2(0, 24));
    }

    private static void Chip(Box g, string name, float x, float w, string icon, Color iconColor, string value, float delay)
    {
        var chip = Node(name, g, x, 38, w, 40, TL, MC);
        var bg = Img(chip, "Bg", 0, 0, w, 40, "panel_tlbr8", HoloPalette.Panel, raycast: true);
        var line = Img(chip, "Line", 0, 0, w, 40, "panel_tlbr8_line", HoloPalette.Line);
        var content = Stretch(chip, "Content", typeof(CanvasGroup));
        Img(content, "Icon", 12, 11, 18, 18, icon, iconColor);
        Txt(content, "Value", value, 38, 0, w - 62, 40, monoFont, 15, HoloPalette.Ink);
        var plus = Txt(content, "Plus", "+", w - 26, 0, 16, 40, monoFont, 17, HoloPalette.Teal, TextAlignmentOptions.Center);
        var hover = Hover(chip, 1.07f, content.rt, false, CT(line, HoloPalette.Teal), CT(bg, HoloPalette.TealDeep.WithAlpha(0.9f)), CT(plus, Color.white));
        Set(hover, "glitchOnClick", true);
        Enter(chip, delay, new Vector2(0, 24));
    }

    private static void IconButton(Box g, string name, float x, string icon, string badge, float delay)
    {
        var btn = Node(name, g, x, 37, 42, 42, TL, MC);
        var bg = Img(btn, "Bg", 0, 0, 42, 42, "panel_tlbr8", HoloPalette.Panel, raycast: true);
        var line = Img(btn, "Line", 0, 0, 42, 42, "panel_tlbr8_line", HoloPalette.Line);
        var content = Stretch(btn, "Content", typeof(CanvasGroup));
        var iconImg = Img(content, "Icon", 10, 10, 22, 22, icon, HoloPalette.Ink);
        if (!string.IsNullOrEmpty(badge))
        {
            var dot = Img(content, "Badge", 27, -5, 18, 18, "dot", HoloPalette.Red);
            Txt(new Box { rt = dot.rectTransform, w = 18, h = 18 }, "Count", badge, 0, 0, 18, 18, monoFont, 11, Color.white, TextAlignmentOptions.Center);
            var pulse = dot.gameObject.AddComponent<HoloPulse>();
            Set(pulse, "alpha", new Vector2(1f, 1f));
            Set(pulse, "scaleAmount", 0.14f);
            Set(pulse, "rate", 0.9f);
        }
        var hover = Hover(btn, 1.1f, content.rt, false, CT(line, HoloPalette.Teal), CT(bg, HoloPalette.TealDeep.WithAlpha(0.9f)), CT(iconImg, HoloPalette.Teal));
        Set(hover, "glitchOnClick", true);
        Enter(btn, delay, new Vector2(0, 24));
    }

    private static void BuildStory(Box g)
    {
        var card = Node("StoryProgress", g, 1545, 130, 373, 115, TL, MR);
        var bg = Img(card, "Bg", 0, 0, 373, 115, "panel_tlbr14", HoloPalette.Panel, raycast: true);
        var line = Img(card, "Line", 0, 0, 373, 115, "panel_tlbr14_line", HoloPalette.Line);
        var content = Stretch(card, "Content", typeof(CanvasGroup));
        Txt(content, "Label", "STORY", 20, 12, 220, 20, monoFont, 12, HoloPalette.Teal, spacing: 14);
        Txt(content, "Chapter", "CH.03", 272, 12, 80, 20, monoFont, 12, HoloPalette.Teal, TextAlignmentOptions.MidlineRight, spacing: 4);
        Txt(content, "Title", "녹슨 무대 위의 노이즈<size=62%><color=#A8C2BD>  ·  3/6</color></size>", 20, 32, 345, 32, titleFont, 21, HoloPalette.Ink);

        var segs = new List<Object>();
        const int count = 6;
        float segW = (332f - (count - 1) * 3f) / count;
        for (int i = 0; i < count; i++)
            segs.Add(Img(content, "Seg" + i, 20 + i * (segW + 3f), 70, segW, 6, null, HoloPalette.Teal.WithAlpha(0.15f)));
        var seg = content.rt.gameObject.AddComponent<HoloSegments>();
        Set(seg, "segments", segs.ToArray());
        Set(seg, "filled", 3.5f); // 3칸 완료 + 4번째 칸 진행 중(깜빡임)

        Txt(content, "NextLabel", "NEXT", 20, 82, 60, 18, monoFont, 11, HoloPalette.Dim, spacing: 6);
        Txt(content, "Next", "리허설 직전, 정전", 152, 82, 200, 18, monoFont, 11, HoloPalette.Dim, TextAlignmentOptions.MidlineRight);

        Hover(card, 1.03f, content.rt, false, CT(line, HoloPalette.Teal), CT(bg, HoloPalette.Panel2));
        Enter(card, 0.15f, new Vector2(60, 0));
    }

    private static void BuildEvent(Box g)
    {
        var card = Node("EventCard", g, 1470, 800, 448, 125, TL, MR, typeof(HoloEventCarousel));
        var bg = Img(card, "Bg", 0, 0, 448, 125, "panel_tlbr14", HoloPalette.Panel2, raycast: true);
        var line = Img(card, "Line", 0, 0, 448, 125, "panel_tlbr14_line", HoloPalette.Red.WithAlpha(0.5f));

        // 왼쪽 빨간 사선 띠 (천천히 흐른다)
        var stripeGo = new GameObject("Stripes", typeof(RectTransform), typeof(RawImage), typeof(HoloUVScroll));
        stripeGo.transform.SetParent(card.rt, false);
        PlaceRect((RectTransform)stripeGo.transform, card, 2, 16, 25, 107, TL, TL);
        var stripes = stripeGo.GetComponent<RawImage>();
        stripes.texture = HoloHubArt.LoadTexture("stripes");
        stripes.color = HoloPalette.Red.WithAlpha(0.85f);
        stripes.raycastTarget = false;
        Set(stripeGo.GetComponent<HoloUVScroll>(), "tileSize", new Vector2(16f, 16f));
        Set(stripeGo.GetComponent<HoloUVScroll>(), "speed", new Vector2(0.5f, 0f));

        var content = Stretch(card, "Content", typeof(CanvasGroup));
        var dot = Img(content, "KindDot", 48, 19, 8, 8, "dot", HoloPalette.Red);
        Set(dot.gameObject.AddComponent<HoloPulse>(), "rate", 1.4f);
        var kind = Txt(content, "Kind", "NEXT SHOW  ·  CH.04", 62, 13, 330, 20, monoFont, 12, HoloPalette.Red, spacing: 14);
        var title = Txt(content, "Title", "폐공장 라이브 나이트", 46, 33, 350, 36, titleFont, 26, HoloPalette.Ink);
        var sub = Txt(content, "Sub", "신곡 해금 · Ghost District", 48, 68, 300, 22, bodyFont, 14, HoloPalette.Mute);
        var badge = Txt(content, "Badge", "D-3", 287, 76, 140, 44, titleFont, 38, HoloPalette.Red, TextAlignmentOptions.MidlineRight);
        var pulse = badge.gameObject.AddComponent<HoloPulse>();
        Set(pulse, "alpha", new Vector2(0.65f, 1f));
        Set(pulse, "rate", 0.8f);

        // 아래 점 표시 (선택된 점이 길고 빨갛다)
        var dotsBox = Node("Dots", g, 1718, 936, 200, 4, TL, MR, typeof(HorizontalLayoutGroup));
        var layout = dotsBox.rt.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.spacing = 8 * K;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var dots = new List<Object>();
        for (int i = 0; i < 3; i++)
            dots.Add(Img(dotsBox, "Dot" + i, 0, 0, i == 0 ? 32 : 16, 3, null, i == 0 ? HoloPalette.Red : HoloPalette.Dim));
        Enter(dotsBox, 0.4f, new Vector2(30, 0));

        var carousel = card.rt.GetComponent<HoloEventCarousel>();
        Set(carousel, "entries", new object[]
        {
            Entry("NEXT SHOW  ·  CH.04", "폐공장 라이브 나이트", "신곡 해금 · Ghost District", "D-3"),
            Entry("BOARD  ·  NEW MEMO", "\"내 이야기 같았다\"", "게시판에 새 메모 3장 · 지난 공연 감상", "+3"),
            Entry("WARNING  ·  PATROL", "7구역 야간 순찰 강화", "공연 전 뒷문 확인할 것 · 누군가의 쪽지", "!"),
        });
        Set(carousel, "kindText", kind);
        Set(carousel, "titleText", title);
        Set(carousel, "subText", sub);
        Set(carousel, "badgeText", badge);
        Set(carousel, "content", content.rt);
        Set(carousel, "contentGroup", content.rt.GetComponent<CanvasGroup>());
        Set(carousel, "dots", dots.ToArray());

        Hover(card, 1.03f, null, false, CT(line, HoloPalette.Red), CT(bg, new Color(30 / 255f, 14 / 255f, 18 / 255f, 0.88f)));
        Enter(card, 0.32f, new Vector2(70, 0));
    }

    private static Dictionary<string, object> Entry(string kind, string title, string sub, string badge) =>
        new Dictionary<string, object> { { "kind", kind }, { "title", title }, { "sub", sub }, { "badge", badge } };

    private static void BuildRadio(Box g)
    {
        var holo = HoloPalette.Holo;
        var radio = Node("Radio7", g, 80, 1022, 1545, 80, TL, ML, typeof(HoloRadioBar));

        var beam = Img(radio, "Beam", 62, 3, 1421, 72, "beam", holo.WithAlpha(0.30f));
        var glow = Img(radio, "EmitterGlow", 20, 58, 1505, 40, "glow", holo.WithAlpha(0.55f));
        Img(radio, "Base", 32, 77, 1481, 5, null, new Color(11 / 255f, 26 / 255f, 28 / 255f, 1f));
        var emitter = Img(radio, "Emitter", 46, 74.5f, 1453, 3.5f, null, holo.WithAlpha(0.95f));

        var panel = Node("Panel", radio, 0, 16, 1545, 48, TL, TL, typeof(CanvasGroup));
        Img(panel, "Bg", 0, 0, 1545, 48, null, holo.WithAlpha(0.08f), raycast: true);
        var grad = Img(panel, "Grad", 0, 0, 1545, 48, "grad_top", holo.WithAlpha(0.16f));
        grad.rectTransform.localScale = new Vector3(1, -1, 1);

        var scanGo = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage), typeof(HoloUVScroll));
        scanGo.transform.SetParent(panel.rt, false);
        StretchRect((RectTransform)scanGo.transform);
        var scan = scanGo.GetComponent<RawImage>();
        scan.texture = HoloHubArt.LoadTexture("scanline");
        scan.color = holo.WithAlpha(0.16f);
        scan.raycastTarget = false;
        Set(scanGo.GetComponent<HoloUVScroll>(), "tileSize", new Vector2(4f, 3f));
        Set(scanGo.GetComponent<HoloUVScroll>(), "speed", new Vector2(0f, -1.5f));

        // 테두리
        var border = holo.WithAlpha(0.55f);
        Img(panel, "BorderT", 0, 0, 1545, 1.5f, null, border);
        Img(panel, "BorderB", 0, 46.5f, 1545, 1.5f, null, border);
        Img(panel, "BorderL", 0, 0, 1.5f, 48, null, border);
        Img(panel, "BorderR", 1543.5f, 0, 1.5f, 48, null, border);

        // 왼쪽 라벨 칸: 이퀄라이저 + RADIO 7 + FM 77.7
        var label = Node("Label", panel, 0, 0, 255, 48);
        Img(label, "Bg", 0, 0, 255, 48, null, holo.WithAlpha(0.14f));
        Img(label, "Divider", 253.5f, 0, 1.5f, 48, null, holo.WithAlpha(0.45f));
        var bars = new List<Object>();
        for (int i = 0; i < 6; i++)
            bars.Add(Img(label, "Bar" + i, 16 + i * 4.5f, 32 - 12, 2.5f, 12, null, holo, BottomLeft, BottomLeft).rectTransform);
        Txt(label, "Name", "RADIO 7", 50, 0, 110, 48, titleFont, 16, new Color(0.9f, 1f, 1f), spacing: 16, mat: titleGlow);
        Txt(label, "Freq", "FM 77.7", 158, 0, 90, 48, monoFont, 13, holo.WithAlpha(0.9f), spacing: 6);

        // 흐르는 뉴스: 오토네가 재건한 도시의 라디오 뉴스 (세계관 문서 기준, 가짜 기사)
        var ticker = Node("Ticker", panel, 270, 0, 1265, 48, TL, TL, typeof(RectMask2D));
        const string gap = "            ";
        const string news =
            "<color=#FF4D5E>[속보]</color>  4번 부두 물류창고서 야간조 노동자 숨진 채 발견 <alpha=#B0>· 경찰 \"단순 추락사고로 추정\"<alpha=#FF>" + gap +
            "<color=#5AE8FF>[오토네 공공방송]</color>  7구역 공업단지 오늘 밤 계획 정전 <alpha=#B0>· 발전 설비 정비, 생산시설 전력 우선 공급<alpha=#FF>" + gap +
            "<color=#FFB45C>[치안]</color>  문화관리국, 미허가 공연장 집중 단속 <alpha=#B0>· \"인증되지 않은 음원 유통은 공공질서 위반\"<alpha=#FF>" + gap +
            "<color=#FF4D5E>[사건]</color>  지하 음반 거래 혐의 20대 남성 연행 <alpha=#B0>· 미인증 음반 300여 장 압수<alpha=#FF>" + gap +
            "<color=#FFB45C>[의혹]</color>  공업지대 혁명 당시 실종자 명단 비공개 논란 <alpha=#B0>· 오토네 \"근거 없는 소문\"<alpha=#FF>" + gap +
            "<color=#5AE8FF>[재개발]</color>  3구역 노후 노동자 주거지 철거 공고 <alpha=#B0>· 주민 이주 기한 이달 말까지<alpha=#FF>" + gap +
            "<color=#FF4D5E>[사건]</color>  폐공장 지대서 신원 미상 시신 발견 <alpha=#B0>· 최근 실종 신고된 음악가 여부 확인 중<alpha=#FF>" + gap +
            "<color=#5AE8FF>[오토네]</color>  \"재건 30주년, 질서가 도시를 지켰습니다\" <alpha=#B0>· 오토네 홍보실 특별 담화<alpha=#FF>" + gap +
            "<color=#A3EBD6>[날씨]</color>  오늘 밤 산성비 <alpha=#B0>· 공업지대 주민 외출 자제 권고<alpha=#FF>" + gap;
        var tickerA = Txt(ticker, "NewsA", news, 0, 0, 9000, 48, bodyFont, 16, new Color(0.86f, 1f, 1f), spacing: 1, mat: bodyGlow);
        var tickerB = Txt(ticker, "NewsB", news, 9000, 0, 9000, 48, bodyFont, 16, new Color(0.86f, 1f, 1f), spacing: 1, mat: bodyGlow);

        // 네 귀퉁이 꺾쇠
        Corner(panel, 3, 3, false, false);
        Corner(panel, 1532, 3, true, false);
        Corner(panel, 3, 35, false, true);
        Corner(panel, 1532, 35, true, true);

        var slice = Img(panel, "GlitchSlice", 0, 22, 1545, 3, null, holo.WithAlpha(0.7f), TL, ML);

        var bar = radio.rt.GetComponent<HoloRadioBar>();
        Set(bar, "beam", beam);
        Set(bar, "emitter", emitter);
        Set(bar, "emitterGlow", glow);
        Set(bar, "panel", panel.rt);
        Set(bar, "panelGroup", panel.rt.GetComponent<CanvasGroup>());
        Set(bar, "glitchSlice", slice.rectTransform);
        Set(bar, "bars", bars.ToArray());
        Set(bar, "barHeight", new Vector2(4 * K, 16 * K));
        Set(bar, "tickerA", tickerA);
        Set(bar, "tickerB", tickerB);

        Hover(radio, 1.012f, null, false);
        Enter(radio, 0.45f, new Vector2(0, -30));
    }

    private static void Corner(Box panel, float x, float y, bool right, bool bottom)
    {
        var c = Node("Corner", panel, x, y, 10, 10);
        var color = HoloPalette.Holo;
        Img(c, "H", 0, bottom ? 8.5f : 0, 10, 1.5f, null, color);
        Img(c, "V", right ? 8.5f : 0, 0, 1.5f, 10, null, color);
    }

    private static void BuildVersion(Box g)
    {
        var info = Node("Version", g, 1700, 1070, 218, 20, TL, MR);
        Txt(info, "Ver", "VER 0.3.1", 0, 0, 218, 20, monoFont, 12, HoloPalette.Mute, TextAlignmentOptions.MidlineRight, spacing: 6);
        Enter(info, 0.5f, new Vector2(0, -20));
    }

    private static void BuildOverlay(Box root)
    {
        var overlay = Stretch(root, "ScreenFX", typeof(HoloScreenFX));

        var scanGo = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage), typeof(HoloUVScroll));
        scanGo.transform.SetParent(overlay.rt, false);
        StretchRect((RectTransform)scanGo.transform);
        var scan = scanGo.GetComponent<RawImage>();
        scan.texture = HoloHubArt.LoadTexture("scanline");
        scan.color = new Color(0f, 0f, 0f, 0.22f);
        scan.raycastTarget = false;
        Set(scanGo.GetComponent<HoloUVScroll>(), "tileSize", new Vector2(4f, 3f));
        Set(scanGo.GetComponent<HoloUVScroll>(), "speed", new Vector2(0f, -0.6f));

        var vig = Stretch(overlay, "Vignette", typeof(Image));
        var vigImg = vig.rt.GetComponent<Image>();
        vigImg.sprite = HoloHubArt.Load("vignette");
        vigImg.color = new Color(0f, 0.02f, 0.02f, 0.6f);
        vigImg.raycastTarget = false;

        var flick = Stretch(overlay, "Flicker", typeof(Image));
        var flickImg = flick.rt.GetComponent<Image>();
        flickImg.color = new Color(0, 0, 0, 0);
        flickImg.raycastTarget = false;

        var band = Img(overlay, "GlitchBand", 0, 0, MockW, 10, null, HoloPalette.Teal.WithAlpha(0.07f), ML, ML);
        band.rectTransform.anchorMin = new Vector2(0, 0.5f);
        band.rectTransform.anchorMax = new Vector2(1, 0.5f);
        band.rectTransform.pivot = MC;
        band.rectTransform.sizeDelta = new Vector2(0, 10 * K);
        band.rectTransform.anchoredPosition = Vector2.zero;

        var fx = overlay.rt.GetComponent<HoloScreenFX>();
        Set(fx, "flicker", flickImg);
        Set(fx, "band", band.rectTransform);
    }

    // ── 만들기 도움 함수 ─────────────────────────────────────────

    private static Box Group(Box root, string name, Vector2 corner) => Node(name, root, 0, 0, MockW, MockH, corner, corner);

    // x, y = 부모 안에서 이 상자의 왼쪽 위 (목업 픽셀). anchor = 부모의 어느 점에 붙을지, pivot = 커지고 도는 기준점
    private static Box Node(string name, Box parent, float x, float y, float w, float h, params Type[] comps) =>
        Node(name, parent, x, y, w, h, TL, TL, comps);

    private static Box Node(string name, Box parent, float x, float y, float w, float h, Vector2 anchor, Vector2 pivot, params Type[] comps)
    {
        var types = new List<Type> { typeof(RectTransform) };
        types.AddRange(comps);
        var go = new GameObject(name, types.ToArray());
        go.transform.SetParent(parent.rt, false);
        var rt = (RectTransform)go.transform;
        PlaceRect(rt, parent, x, y, w, h, anchor, pivot);
        return new Box { rt = rt, w = w, h = h };
    }

    private static void PlaceRect(RectTransform rt, Box parent, float x, float y, float w, float h, Vector2 anchor, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(w * K, h * K);
        float px = x + pivot.x * w;
        float py = y + (1f - pivot.y) * h;
        rt.anchoredPosition = new Vector2((px - anchor.x * parent.w) * K, -(py - (1f - anchor.y) * parent.h) * K);
    }

    private static Box Stretch(Box parent, string name, params Type[] comps)
    {
        var types = new List<Type> { typeof(RectTransform) };
        types.AddRange(comps);
        var go = new GameObject(name, types.ToArray());
        go.transform.SetParent(parent.rt, false);
        var rt = (RectTransform)go.transform;
        StretchRect(rt);
        return new Box { rt = rt, w = parent.w, h = parent.h };
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = MC;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void HStretchEdge(RectTransform rt, bool top, float height)
    {
        rt.anchorMin = new Vector2(0, top ? 1 : 0);
        rt.anchorMax = new Vector2(1, top ? 1 : 0);
        rt.pivot = new Vector2(0.5f, top ? 1 : 0);
        rt.sizeDelta = new Vector2(0, height * K);
        rt.anchoredPosition = Vector2.zero;
    }

    // 다른 오브젝트 하나를 Box처럼 쓰기
    private static Box Wrap(Component c)
    {
        var rt = (RectTransform)c.transform;
        return new Box { rt = rt, w = rt.sizeDelta.x / K, h = rt.sizeDelta.y / K };
    }

    private static Image Img(Box parent, string name, float x, float y, float w, float h, string sprite, Color color,
        Vector2? anchor = null, Vector2? pivot = null, bool raycast = false)
    {
        var box = Node(name, parent, x, y, w, h, anchor ?? TL, pivot ?? TL, typeof(Image));
        var img = box.rt.GetComponent<Image>();
        if (!string.IsNullOrEmpty(sprite))
        {
            img.sprite = HoloHubArt.Load(sprite);
            if (!img.sprite) Debug.LogWarning($"[HoloHub] 그림 {sprite}을 찾지 못했어요.");
            else if (img.sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        }
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI Txt(Box parent, string name, string text, float x, float y, float w, float h,
        TMP_FontAsset font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft,
        float spacing = 0f, Vector2? anchor = null, Vector2? pivot = null, Material mat = null)
    {
        var box = Node(name, parent, x, y, w, h, anchor ?? TL, pivot ?? TL, typeof(TextMeshProUGUI));
        var tmp = box.rt.GetComponent<TextMeshProUGUI>();
        if (font) tmp.font = font;
        if (mat) tmp.fontSharedMaterial = mat;
        tmp.text = text;
        tmp.fontSize = size * K;
        tmp.color = color;
        tmp.alignment = align;
        tmp.characterSpacing = spacing;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Dictionary<string, object> CT(Graphic g, Color hover) =>
        new Dictionary<string, object> { { "graphic", g }, { "hoverColor", hover } };

    private static HoloHover Hover(Box target, float scale, RectTransform glitch, bool menuDriven, params Dictionary<string, object>[] colors)
    {
        var hover = target.rt.gameObject.AddComponent<HoloHover>();
        Set(hover, "reactToPointer", !menuDriven);
        Set(hover, "hoverScale", scale);
        Set(hover, "colorTargets", colors.Cast<object>().ToArray());
        if (glitch) Set(hover, "glitchTarget", glitch);
        return hover;
    }

    private static void Enter(Box target, float delay, Vector2 from)
    {
        var e = target.rt.gameObject.AddComponent<HoloEntrance>();
        Set(e, "delay", delay);
        Set(e, "fromOffset", from * K);
    }

    // 스크립트의 [SerializeField] private 값을 이름으로 넣는다
    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[HoloHub] {target.GetType().Name}에 '{field}' 칸이 없어요.");
            return;
        }
        Assign(prop, value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Assign(SerializedProperty p, object v)
    {
        switch (v)
        {
            case null: p.objectReferenceValue = null; break;
            case float f: p.floatValue = f; break;
            case int i:
                if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i; else p.intValue = i;
                break;
            case bool b: p.boolValue = b; break;
            case string s: p.stringValue = s; break;
            case Color c: p.colorValue = c; break;
            case Vector2 v2: p.vector2Value = v2; break;
            case Vector2Int vi: p.vector2IntValue = vi; break;
            case Object o: p.objectReferenceValue = o; break;
            case Dictionary<string, object> d:
                foreach (var kv in d)
                {
                    var child = p.FindPropertyRelative(kv.Key);
                    if (child == null) Debug.LogError($"[HoloHub] '{p.propertyPath}'에 '{kv.Key}' 칸이 없어요.");
                    else Assign(child, kv.Value);
                }
                break;
            case IList list:
                p.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++) Assign(p.GetArrayElementAtIndex(i), list[i]);
                break;
            default:
                Debug.LogError($"[HoloHub] {v.GetType().Name} 값은 넣을 수 없어요 ({p.propertyPath}).");
                break;
        }
    }

    // 빌드 프로필이 자기 씬 목록을 쓰고 있으면 그 목록에, 아니면 기본 목록에 넣는다
    private static void AddToSceneList(string path)
    {
        var profile = BuildProfile.GetActiveBuildProfile();
        if (profile && profile.overrideGlobalScenes)
        {
            var scenes = profile.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            profile.scenes = scenes.ToArray();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        else
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Debug.Log($"[HoloHub] 씬 목록에 추가: {path}");
    }
}
