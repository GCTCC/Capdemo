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
    internal const string FontPath = "Assets/Fonts/DaeguJunggu/대구중구읍성 Regular SDF.asset";
    private const string SampleChapterPath = "Assets/Story/Story_Sample.asset";
    private const string IllustFolder = "Assets/image/Illust/";

    // 지금 열린 씬에 스토리 UI 한 벌을 만든다. 씬 저장은 직접 한다(Ctrl+S).
    // 프로젝트 창에서 StoryChapter 에셋을 선택한 채로 실행하면 그 에셋을 연결해 둔다.
    [MenuItem("Tools/CapDemo/스토리/스토리 UI 만들기")]
    private static void CreateStoryUI()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (!font) Debug.LogWarning($"[CapDemo] {FontPath} 폰트를 찾지 못해 기본 폰트를 사용합니다.");

        if (!Object.FindAnyObjectByType<EventSystem>())
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create Story UI");
        }

        // StoryCanvas: 1920x1080 기준으로 해상도에 맞춰 늘어난다
        var canvasGo = new GameObject("StoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(StoryManager), typeof(TutoStoryEnd));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Story UI");
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
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

        var nameInput = CreateNameInputPanel(root, font);

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
        if (Selection.activeObject is StoryChapter selected)
            managerSo.FindProperty("chapter").objectReferenceValue = selected;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        // 스토리가 끝나면 튜토리얼 완료 저장 → HubScene
        UnityEventTools.AddPersistentListener(manager.OnFinished, canvasGo.GetComponent<TutoStoryEnd>().OnStoryFinished);

        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = canvasGo;
        Debug.Log("[CapDemo] StoryCanvas를 만들었습니다. StoryManager의 Chapter에 스토리 에셋을 넣고 씬을 저장하세요.");
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
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                text.font = font;
        if (input.placeholder is TMP_Text placeholder) placeholder.text = "이름";

        var component = panel.gameObject.AddComponent<NameInputPanel>();
        var so = new SerializedObject(component);
        so.FindProperty("input").objectReferenceValue = input;
        so.FindProperty("confirmButton").objectReferenceValue = buttonGo.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);
        return component;
    }

    // 동작 확인용 샘플 스토리: image/Illust의 일러스트로 컷 2개, 이름 입력 줄 포함
    [MenuItem("Tools/CapDemo/스토리/샘플 스토리 에셋 만들기")]
    private static void CreateSampleChapter()
    {
        if (File.Exists(SampleChapterPath) && !EditorUtility.DisplayDialog("샘플 덮어쓰기",
                SampleChapterPath + " 을(를) 새로 만듭니다.", "덮어쓰기", "취소"))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(SampleChapterPath));
        var chapter = ScriptableObject.CreateInstance<StoryChapter>();
        AssetDatabase.CreateAsset(chapter, SampleChapterPath);

        var so = new SerializedObject(chapter);
        var cuts = so.FindProperty("cuts");
        cuts.arraySize = 2;

        var cut1 = cuts.GetArrayElementAtIndex(0);
        cut1.FindPropertyRelative("memo").stringValue = "컷 1 - 도입";
        cut1.FindPropertyRelative("illustration").objectReferenceValue = LoadSprite("Anima_00621_.png");
        var lines1 = cut1.FindPropertyRelative("lines");
        lines1.arraySize = 3;
        SetLine(lines1.GetArrayElementAtIndex(0), "", null, "……여기는 어디지?");
        SetLine(lines1.GetArrayElementAtIndex(1), "???", LoadSprite("Anima_00329_.png"), "드디어 눈을 떴구나.\n이름이 뭐지?");
        SetLine(lines1.GetArrayElementAtIndex(2), "???", null, "그래, 기억해 두지.", StoryPause.NameInput);

        var cut2 = cuts.GetArrayElementAtIndex(1);
        cut2.FindPropertyRelative("memo").stringValue = "컷 2 - 이름 확인";
        cut2.FindPropertyRelative("illustration").objectReferenceValue = LoadSprite("upscalemedia-transformed.png");
        cut2.FindPropertyRelative("transition").enumValueIndex = (int)CutTransition.FadeToBlack;
        var lines2 = cut2.FindPropertyRelative("lines");
        lines2.arraySize = 2;
        SetLine(lines2.GetArrayElementAtIndex(0), "???", null, "반가워, {player}.");
        SetLine(lines2.GetArrayElementAtIndex(1), "{player}", null, "……잘 부탁해.", hideStanding: true);

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Selection.activeObject = chapter;
        EditorGUIUtility.PingObject(chapter);
        Debug.Log($"[CapDemo] {SampleChapterPath} 를 만들었습니다.");
    }

    internal static void SetLine(SerializedProperty line, string speaker, Sprite standing, string text,
        StoryPause pause = StoryPause.None, bool hideStanding = false)
    {
        line.FindPropertyRelative("speaker").stringValue = speaker;
        line.FindPropertyRelative("standing").objectReferenceValue = standing;
        line.FindPropertyRelative("hideStanding").boolValue = hideStanding;
        line.FindPropertyRelative("text").stringValue = text;
        line.FindPropertyRelative("pause").enumValueIndex = (int)pause;
    }

    private static Sprite LoadSprite(string fileName)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IllustFolder + fileName);
        if (!sprite) Debug.LogWarning($"[CapDemo] {IllustFolder}{fileName} 스프라이트를 찾지 못했습니다.");
        return sprite;
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
