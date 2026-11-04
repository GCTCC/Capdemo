using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 허브 > NewHubDemoScene 만들기
// backstage-assets-v1 PNG로 임시 허브 화면(백스테이지)을 조립한다. 기존 HubScene은 건드리지 않는다.
// 그림 크기·위치는 대략 맞춰 둔 값이라, 씬을 만든 뒤 Scene 뷰에서 직접 옮겨 다듬으면 된다.
// 다시 실행하면 씬을 처음부터 새로 만들므로, 손으로 다듬은 뒤에는 실행하지 않는다.
public static class NewHubDemoMenu
{
    private const string ScenePath = "Assets/Scenes/NewHubDemoScene.unity";
    private const string ArtFolder = "Assets/image/Hub/Backstage/";
    private const string FontPath = "Assets/Art/Fonts/경기천년제목_Bold SDF.asset";

    // 가이드의 권장 스프라이트 영역 (왼쪽 아래 원점, X, Y, Width, Height). 투명 여백을 잘라낸다.
    private static readonly (string file, RectInt rect)[] Art =
    {
        ("background/backstage-empty.png", new RectInt(0, 0, 1672, 941)),
        ("chapter/card-frame.png", new RectInt(52, 47, 845, 1566)),
        ("chapter/chapter-01-thumbnail.png", new RectInt(0, 0, 1086, 1448)),
        ("character/guitarist-seated.png", new RectInt(50, 18, 1062, 1378)),
        ("icons/archive.png", new RectInt(203, 256, 842, 672)),
        ("icons/settings.png", new RectInt(235, 300, 784, 666)),
        ("icons/single-play.png", new RectInt(278, 238, 686, 752)),
        ("icons/story.png", new RectInt(274, 237, 667, 741)),
        ("ui/menu-bar.png", new RectInt(32, 273, 2109, 179)),
        ("ui/menu-selected.png", new RectInt(68, 96, 1547, 744)),
    };

    // 하단 메뉴: 글자, 아이콘 파일
    private static readonly (string label, string icon, string objectName)[] Menus =
    {
        ("스토리모드", "icons/story.png", "Story"),
        ("싱글 플레이", "icons/single-play.png", "SinglePlay"),
        ("아카이브", "icons/archive.png", "Archive"),
        ("설정", "icons/settings.png", "Settings"),
    };

    private static TMP_FontAsset font;

    [MenuItem("Tools/CapDemo/허브/NewHubDemoScene 만들기")]
    private static void CreateScene()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("씬 덮어쓰기",
                $"{ScenePath}를 처음부터 새로 만듭니다.\n손으로 옮긴 위치도 모두 초기화됩니다.", "덮어쓰기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (var (file, rect) in Art)
            if (!ImportSprite(ArtFolder + file, rect)) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (!font) Debug.LogWarning($"[NewHub] {FontPath} 폰트를 찾지 못해 기본 폰트를 사용합니다.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        BuildCanvas();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[NewHub] {ScenePath}를 만들었습니다. Build Settings에는 등록하지 않았습니다.");
    }

    // ── 화면 조립 (가이드의 권장 순서: 뒤에서 앞으로) ──────────────────────────
    private static void BuildCanvas()
    {
        // 1920x1080 기준으로 해상도에 맞춰 늘어나는 캔버스
        var canvasGo = new GameObject("NewHub", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // 1. 배경: 화면 전체
        var background = CreateImage("Background", root, "background/backstage-empty.png");
        Stretch(background.rectTransform);

        // 2. 캐릭터 + 기타: 왼쪽 중앙, 엉덩이가 소파 좌석에 오도록
        var character = CreateImage("Character", root, "character/guitarist-seated.png");
        Place(character.rectTransform, new Vector2(-330f, -10f), new Vector2(637f, 827f));

        // 3~4. 챕터 카드: 오른쪽 위. 썸네일, 프레임, 글자를 같은 부모 아래 따로 둔다
        BuildChapterCard(root);

        // 5~7. 하단 메뉴 바, 붉은 선택 패널, 버튼(아이콘 + 8. 글자)
        BuildMenuBar(root);
    }

    private static void BuildChapterCard(Transform root)
    {
        var card = new GameObject("ChapterCard", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(root, false);
        Place(card, new Vector2(690f, 150f), new Vector2(350f, 648f));

        // 사진 창: 프레임의 투명한 가운데 구멍 크기의 사각 영역. 썸네일이 이 밖으로 삐져나오지 않게 가린다
        var window = new GameObject("PhotoWindow", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        window.SetParent(card, false);
        SetAnchors(window, new Vector2(0.047f, 0.185f), new Vector2(0.959f, 0.884f));

        // 썸네일은 사진 창을 빈틈없이 채우도록(비율 유지, 넘치는 부분은 잘림)
        var thumbnail = CreateImage("Thumbnail", window, "chapter/chapter-01-thumbnail.png");
        var fitter = thumbnail.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1086f / 1448f;

        var frame = CreateImage("Frame", card, "chapter/card-frame.png");
        Stretch(frame.rectTransform);

        // 상단 띠: "현재 챕터" / 하단 띠: "CHAPTER 01"
        var label = CreateText("CurrentChapterLabel", card, "현재 챕터", 26f, TextAlignmentOptions.Left);
        SetAnchors(label.rectTransform, new Vector2(0.09f, 0.895f), new Vector2(0.5f, 0.975f));
        label.color = new Color(0.85f, 0.85f, 0.85f);

        var number = CreateText("ChapterNumber", card, "CHAPTER 01", 40f, TextAlignmentOptions.Center);
        SetAnchors(number.rectTransform, new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.175f));
    }

    private static void BuildMenuBar(Transform root)
    {
        var bar = CreateImage("MenuBar", root, "ui/menu-bar.png");
        var barRect = bar.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 24f);
        barRect.sizeDelta = new Vector2(1800f, 153f);
        bar.raycastTarget = false;

        // 붉은 패널: 버튼들보다 먼저 만들어서 뒤에 그려진다. 위치는 HubMenuBar가 선택된 버튼으로 옮긴다
        var panel = CreateImage("SelectedPanel", barRect, "ui/menu-selected.png");

        float slot = barRect.sizeDelta.x / Menus.Length;
        var buttons = new HubMenuButton[Menus.Length];
        for (int i = 0; i < Menus.Length; i++)
        {
            var (labelText, iconFile, objectName) = Menus[i];

            // 버튼 영역: 투명한 Image가 있어야 마우스를 인식한다
            var buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(HubMenuButton));
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.SetParent(barRect, false);
            buttonRect.anchoredPosition = new Vector2(-barRect.sizeDelta.x / 2f + slot * (i + 0.5f), 0f);
            buttonRect.sizeDelta = new Vector2(slot, barRect.sizeDelta.y);
            buttonGo.GetComponent<Image>().color = Color.clear;

            var icon = CreateImage("Icon", buttonRect, iconFile);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(-120f, 0f), new Vector2(90f, 90f));

            var text = CreateText("Label", buttonRect, labelText, 36f, TextAlignmentOptions.Left);
            Place(text.rectTransform, new Vector2(45f, 0f), new Vector2(240f, 80f));

            var menuButton = new SerializedObject(buttonGo.GetComponent<HubMenuButton>());
            var targets = menuButton.FindProperty("highlightTargets");
            targets.arraySize = 2;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = icon;
            targets.GetArrayElementAtIndex(1).objectReferenceValue = text;
            menuButton.ApplyModifiedPropertiesWithoutUndo();
            buttons[i] = buttonGo.GetComponent<HubMenuButton>();
        }

        var menuBar = new SerializedObject(bar.gameObject.AddComponent<HubMenuBar>());
        menuBar.FindProperty("selectedPanel").objectReferenceValue = panel.rectTransform;
        var list = menuBar.FindProperty("buttons");
        list.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        menuBar.ApplyModifiedPropertiesWithoutUndo();

        // 패널은 마우스를 올렸을 때만 보인다. 에디터에서는 첫 버튼 칸에 꺼진 채로 둔다
        var first = buttons[0].GetComponent<RectTransform>();
        panel.rectTransform.anchoredPosition = first.anchoredPosition;
        panel.rectTransform.sizeDelta = first.sizeDelta;
        panel.gameObject.SetActive(false);
    }

    // ── 임포트 설정 ─────────────────────────────────────────────────────────
    // 가이드대로 UI용 스프라이트 설정을 하고, 투명 여백을 뺀 영역 하나만 스프라이트로 자른다.
    private static bool ImportSprite(string path, RectInt area)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            EditorUtility.DisplayDialog("NewHubDemoScene", $"그림을 찾지 못했습니다:\n{path}", "확인");
            return false;
        }

        string spriteName = Path.GetFileNameWithoutExtension(path);
        var current = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (current.Length == 1 && current[0].name == spriteName && current[0].rect == new Rect(area.x, area.y, area.width, area.height))
            return true;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = path.EndsWith("menu-bar.png") ? 4096 : 2048;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var spriteRect = new SpriteRect
        {
            name = spriteName,
            spriteID = GUID.Generate(),
            rect = new Rect(area.x, area.y, area.width, area.height),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
        };
        provider.SetSpriteRects(new[] { spriteRect });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(spriteRect.name, spriteRect.spriteID) });
        provider.Apply();
        importer.SaveAndReimport();
        return true;
    }

    private static Sprite LoadSprite(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(ArtFolder + file).OfType<Sprite>().FirstOrDefault();

    // ── 작은 도우미들 ───────────────────────────────────────────────────────
    private static Image CreateImage(string name, Transform parent, string file)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = LoadSprite(file);
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font) text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // 부모 가운데를 기준으로 위치·크기를 정한다
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 부모 크기에 대한 비율(0~1, 왼쪽 아래 기준)로 영역을 정한다
    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect) => SetAnchors(rect, Vector2.zero, Vector2.one);

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        go.transform.position = new Vector3(0f, 0f, -10f);
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }
}
