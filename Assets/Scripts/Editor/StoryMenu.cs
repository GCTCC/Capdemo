using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 스토리
public static class StoryMenu
{
    // 스토리 UI의 모든 글자(대사·이름·이름 입력 창)는 경기천년제목을 쓴다
    internal const string FontPath = "Assets/Art/Fonts/경기천년제목_Bold SDF.asset";
    private const string NameInputFontPath = "Assets/Art/Fonts/경기천년제목_Bold SDF.asset";

    // 지금 열린 씬에 스토리 UI 한 벌을 만든다. 씬 저장은 직접 한다(Ctrl+S).
    // 프로젝트 창에서 StoryChapter 에셋을 선택한 채로 실행하면 그 에셋을 연결해 둔다.
    [MenuItem("Tools/CapDemo/스토리/스토리 UI 만들기")]
    private static void CreateStoryUI() => BuildStoryCanvas(forMap: false);

    private const string MapStoryPrefabPath = "Assets/Prefabs/MapStoryCanvas.prefab";

    // 맵 씬 위에 덮어 띄우는 스토리 UI. NPC의 First Meet Story를 재생할 때 쓴다.
    // 처음 실행하면 지금 씬에 만들고 Assets/Prefabs/MapStoryCanvas.prefab으로 저장한다.
    // 프리팹이 이미 있으면 그 프리팹을 지금 씬에 넣기만 한다. 씬 저장은 직접 한다(Ctrl+S).
    [MenuItem("Tools/CapDemo/스토리/맵용 스토리 UI 만들기")]
    private static void CreateMapStoryUI()
    {
        if (Object.FindAnyObjectByType<StoryOverlay>(FindObjectsInactive.Include))
        {
            EditorUtility.DisplayDialog("맵용 스토리 UI", "이 씬에는 이미 맵용 StoryCanvas(StoryOverlay)가 있어요.", "확인");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapStoryPrefabPath);
        if (prefab)
        {
            EnsureEventSystem();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create Map Story UI");
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
            Debug.Log($"[CapDemo] {MapStoryPrefabPath} 프리팹을 씬에 넣었습니다. 씬을 저장하세요.");
            return;
        }

        var canvasGo = BuildStoryCanvas(forMap: true);
        Directory.CreateDirectory(Path.GetDirectoryName(MapStoryPrefabPath));
        PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGo, MapStoryPrefabPath, InteractionMode.UserAction);
        Debug.Log($"[CapDemo] 맵용 StoryCanvas를 만들고 {MapStoryPrefabPath} 프리팹으로 저장했습니다. " +
                  "다른 맵 씬에는 이 프리팹을 끌어다 놓으세요. 씬을 저장하세요.");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>()) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create Story UI");
    }

    // 스토리 UI 한 벌. forMap이면 맵 위에 덮어 띄우는 용도(StoryOverlay, 자동 재생 안 함, 끝나도 씬 이동 없음),
    // 아니면 TutoScene처럼 씬이 시작하자마자 재생하고 끝나면 HoloHubScene으로 넘어가는 용도
    private static GameObject BuildStoryCanvas(bool forMap)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (!font) Debug.LogWarning($"[CapDemo] {FontPath} 폰트를 찾지 못해 기본 폰트를 사용합니다.");

        EnsureEventSystem();

        // StoryCanvas: 1920x1080 기준으로 해상도에 맞춰 늘어난다
        var canvasGo = new GameObject(forMap ? "MapStoryCanvas" : "StoryCanvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(StoryManager), forMap ? typeof(StoryOverlay) : typeof(TutoStoryEnd));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Story UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (forMap) canvas.sortingOrder = 100; // 맵의 다른 UI(대화창 등)보다 위에 그린다
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // Illustration: 컷 배경 일러스트 두 장 (크로스페이드용). 비율이 달라도 잘리지 않게 preserveAspect
        var illustGo = CreateUI("Illustration", root, typeof(IllustrationView));
        Stretch(illustGo);
        var illustA = CreateImage("IllustA", illustGo.transform, Color.white);
        var illustB = CreateImage("IllustB", illustGo.transform, Color.white);
        foreach (var img in new[] { illustA, illustB })
        {
            Stretch(img.gameObject);
            img.preserveAspect = true;
        }

        // Standing: 화면 왼쪽 아래의 스탠딩 일러스트 (대사창 뒤)
        var standing = CreateImage("Standing", root, Color.white);
        var standingRect = standing.rectTransform;
        standingRect.anchorMin = standingRect.anchorMax = new Vector2(0f, 0f);
        standingRect.pivot = new Vector2(0.5f, 0f);
        standingRect.anchoredPosition = new Vector2(420f, 0f);
        standingRect.sizeDelta = new Vector2(800f, 1000f);
        standing.preserveAspect = true;

        var dialogueBox = CreateDialogueBox(root, font, standing);

        // 이름 입력 창 폰트 (없으면 대사창 폰트)
        var nameFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NameInputFontPath);
        if (!nameFont) Debug.LogWarning($"[CapDemo] {NameInputFontPath} 폰트를 찾지 못해 대사창 폰트를 사용합니다.");
        var nameInput = CreateNameInputPanel(root, nameFont ? nameFont : font);

        // FadeOverlay: 화면 전체를 덮는 검은 Image. 맨 마지막 자식이라 가장 위에 그려진다
        var overlay = CreateImage("FadeOverlay", root, Color.black);
        Stretch(overlay.gameObject);
        overlay.raycastTarget = true;
        var overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();

        // 참조 연결
        var illustView = new SerializedObject(illustGo.GetComponent<IllustrationView>());
        illustView.FindProperty("front").objectReferenceValue = illustA;
        illustView.FindProperty("back").objectReferenceValue = illustB;
        illustView.ApplyModifiedPropertiesWithoutUndo();

        var manager = canvasGo.GetComponent<StoryManager>();
        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("illustration").objectReferenceValue = illustGo.GetComponent<IllustrationView>();
        managerSo.FindProperty("dialogue").objectReferenceValue = dialogueBox;
        managerSo.FindProperty("nameInput").objectReferenceValue = nameInput;
        managerSo.FindProperty("fadeOverlay").objectReferenceValue = overlayGroup;
        if (forMap)
        {
            // 맵에서는 NPC가 부를 때만 재생하므로 Chapter를 비워 두고 자동 재생을 끈다
            managerSo.FindProperty("playOnStart").boolValue = false;
            // 평소에는 투명하게 두어 맵이 보이게 한다
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
        }
        else if (Selection.activeObject is StoryChapter selected)
            managerSo.FindProperty("chapter").objectReferenceValue = selected;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        // 대화창을 새 스타일(하단 그라데이션 + 가는 선 + 선 가운데 화자 이름)로 바꾼다.
        // Tools > CapDemo > 스토리 > 챕터 대화창 새 스타일 적용과 같은 결과
        DialogueStyleMenu.UpgradeChapterBox(dialogueBox);

        // 스토리가 끝나면 튜토리얼 완료 저장 → HoloHubScene
        if (!forMap)
            UnityEventTools.AddPersistentListener(manager.OnFinished, canvasGo.GetComponent<TutoStoryEnd>().OnStoryFinished);

        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = canvasGo;
        if (!forMap)
            Debug.Log("[CapDemo] StoryCanvas를 만들었습니다. StoryManager의 Chapter에 스토리 에셋을 넣고 씬을 저장하세요.");
        return canvasGo;
    }

    // 화면 아래 반투명 대사창 한 벌 (이름표·본문·▼). standing은 없어도 된다
    internal static DialogueBox CreateDialogueBox(Transform root, TMP_FontAsset font, Image standing)
    {
        // DialogueBox: 화면 아래 반투명 대사창
        var boxImage = CreateImage("DialogueBox", root, new Color(0f, 0f, 0f, 0.75f));
        var boxGo = boxImage.gameObject;
        var box = boxGo.AddComponent<DialogueBox>();
        var boxRect = boxImage.rectTransform;
        boxRect.anchorMin = new Vector2(0f, 0f);
        boxRect.anchorMax = new Vector2(1f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.sizeDelta = new Vector2(-160f, 300f);
        boxRect.anchoredPosition = new Vector2(0f, 40f);

        // NamePlate: 대사창 왼쪽 위에 걸친 이름표
        var namePlate = CreateImage("NamePlate", boxGo.transform, new Color(0.12f, 0.12f, 0.16f, 0.95f));
        var plateRect = namePlate.rectTransform;
        plateRect.anchorMin = plateRect.anchorMax = new Vector2(0f, 1f);
        plateRect.pivot = new Vector2(0f, 0.5f);
        plateRect.anchoredPosition = new Vector2(40f, 0f);
        plateRect.sizeDelta = new Vector2(320f, 70f);
        var nameText = CreateText("NameText", namePlate.transform, "화자", 38, TextAlignmentOptions.Center, font);
        Stretch(nameText.gameObject);

        // BodyText: 대사 본문
        var bodyText = CreateText("BodyText", boxGo.transform, "", 36, TextAlignmentOptions.TopLeft, font);
        var bodyRect = bodyText.rectTransform;
        Stretch(bodyText.gameObject);
        bodyRect.offsetMin = new Vector2(60f, 50f);
        bodyRect.offsetMax = new Vector2(-60f, -60f);

        // NextIndicator: 대사창 오른쪽 아래에서 깜빡이는 ▼
        var indicator = CreateText("NextIndicator", boxGo.transform, "▼", 32, TextAlignmentOptions.Center, font);
        var indicatorRect = indicator.rectTransform;
        indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(1f, 0f);
        indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        indicatorRect.anchoredPosition = new Vector2(-50f, 40f);
        indicatorRect.sizeDelta = new Vector2(60f, 60f);

        var dialogue = new SerializedObject(box);
        dialogue.FindProperty("nameText").objectReferenceValue = nameText;
        dialogue.FindProperty("namePlate").objectReferenceValue = namePlate.gameObject;
        dialogue.FindProperty("bodyText").objectReferenceValue = bodyText;
        dialogue.FindProperty("standing").objectReferenceValue = standing;
        dialogue.FindProperty("nextIndicator").objectReferenceValue = indicator;
        dialogue.ApplyModifiedPropertiesWithoutUndo();
        return box;
    }

    // 이름 입력 창: 안내 문구 + 입력칸 + 확인 버튼. 꺼진 상태로 둔다
    private static NameInputPanel CreateNameInputPanel(Transform root, TMP_FontAsset font)
    {
        var panel = CreateImage("NameInputPanel", root, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(800f, 360f);
        panel.raycastTarget = true;

        var prompt = CreateText("Prompt", panel.transform, "이름을 입력해 주세요", 40, TextAlignmentOptions.Center, font);
        var promptRect = prompt.rectTransform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 1f);
        promptRect.anchoredPosition = new Vector2(0f, -70f);
        promptRect.sizeDelta = new Vector2(700f, 70f);

        var resources = new TMP_DefaultControls.Resources();
        var inputGo = TMP_DefaultControls.CreateInputField(resources);
        inputGo.name = "NameField";
        var inputRect = (RectTransform)inputGo.transform;
        inputRect.SetParent(panel.transform, false);
        inputRect.anchoredPosition = new Vector2(0f, 10f);
        inputRect.sizeDelta = new Vector2(560f, 80f);
        var input = inputGo.GetComponent<TMP_InputField>();
        input.pointSize = 36;

        var buttonGo = TMP_DefaultControls.CreateButton(resources);
        buttonGo.name = "ConfirmButton";
        var buttonRect = (RectTransform)buttonGo.transform;
        buttonRect.SetParent(panel.transform, false);
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 70f);
        buttonRect.sizeDelta = new Vector2(240f, 70f);
        var buttonLabel = buttonGo.GetComponentInChildren<TMP_Text>();
        buttonLabel.text = "확인";
        buttonLabel.fontSize = 34;

        if (font)
        {
            input.fontAsset = font;
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                text.font = font;
        }
        if (input.placeholder is TMP_Text placeholder) placeholder.text = "이름";

        var component = panel.gameObject.AddComponent<NameInputPanel>();
        var so = new SerializedObject(component);
        so.FindProperty("input").objectReferenceValue = input;
        so.FindProperty("confirmButton").objectReferenceValue = buttonGo.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);
        return component;
    }

    internal static GameObject CreateUI(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        foreach (var type in components) go.AddComponent(type);
        go.transform.SetParent(parent, false);
        return go;
    }

    internal static Image CreateImage(string name, Transform parent, Color color)
    {
        var image = CreateUI(name, parent, typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    internal static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        var text = CreateUI(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        if (font) text.font = font;
        return text;
    }

    internal static void Stretch(GameObject go)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
