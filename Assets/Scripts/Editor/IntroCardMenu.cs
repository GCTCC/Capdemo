using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 스토리 > 소개 카드 넣기
// 대화창(DialogueBox)마다 소개 카드(IntroCard)를 하나씩 만들어 넣는다. 모양은 머리 옆 이름표 그림을 그대로 쓰고 조금 크게.
// 대상: 맵 대화창 프리팹(DialogueCanvas), 맵용 스토리 프리팹(MapStoryCanvas), 지금 열린 씬의 대화창(프리팹 인스턴스는 빼고).
// 이미 카드가 있는 대화창은 건드리지 않는다. 여러 번 눌러도 된다.
public static class IntroCardMenu
{
    private const string Folder = "Assets/Art/UI/Dialogue";
    private const string PlatePath = Folder + "/SpeakerPlate.png";
    private const string TailPath = Folder + "/SpeakerTail.png";
    private const string TailEndPath = Folder + "/SpeakerTailEnd.png";
    private static readonly string[] Prefabs =
    {
        "Assets/Prefabs/Map/DialogueCanvas.prefab",
        "Assets/Prefabs/MapStoryCanvas.prefab",
    };

    // 이름표(2배 축소)보다 덜 줄여서 테두리 장식도 조금 크게
    private const float PlateScale = 1.6f;
    private static readonly Color NameColor = new Color(0.878f, 0.91f, 0.894f);
    private static readonly Color ProfileColor = new Color(0.74f, 0.78f, 0.77f);

    [MenuItem("Tools/CapDemo/스토리/소개 카드 넣기", priority = 41)]
    private static void AddIntroCards()
    {
        var added = new List<string>();

        foreach (var path in Prefabs)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var box in root.GetComponentsInChildren<DialogueBox>(true))
                    changed |= Ensure(box);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var box in root.GetComponentsInChildren<DialogueBox>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(box)) continue; // 프리팹 쪽에서 넣었다
                    if (Ensure(box)) changed = true;
                }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                added.Add($"{scene.name} 씬");
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("소개 카드",
            added.Count > 0
                ? $"소개 카드를 넣었어요: {string.Join(", ", added)}\n" +
                  "씬에 넣은 경우 씬을 저장해 주세요. 거리·선 굵기·애니메이션 시간은 IntroCard 오브젝트에서, 글자 여백은 Box의 Vertical Layout Group에서 고칠 수 있어요."
                : "모든 대화창에 이미 소개 카드가 있어요.",
            "확인");
    }

    // 카드가 없으면 만들어 연결한다. 새로 만들었거나 연결을 고쳤으면 true
    private static bool Ensure(DialogueBox box)
    {
        var so = new SerializedObject(box);
        var prop = so.FindProperty("introCard");
        var card = prop.objectReferenceValue as IntroCard;
        if (!card) card = box.GetComponentInChildren<IntroCard>(true);
        bool created = false;
        if (!card)
        {
            var font = (so.FindProperty("nameText").objectReferenceValue as TMP_Text)?.font;
            card = Create(box.transform, font);
            created = true;
        }
        if (prop.objectReferenceValue == card) return created;
        prop.objectReferenceValue = card;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static IntroCard Create(Transform parent, TMP_FontAsset font)
    {
        var plateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath);
        var tailSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TailPath);
        var tailEndSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TailEndPath);
        var lineColor = tailSprite ? Color.white : new Color(0.85f, 0.85f, 0.9f, 0.9f);

        var go = new GameObject("IntroCard", typeof(RectTransform), typeof(CanvasGroup));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var slant = CreateImage("TailSlant", rect, tailSprite, lineColor);
        var flat = CreateImage("TailFlat", rect, tailSprite, lineColor);
        var tailEnd = CreateImage("TailEnd", rect, tailEndSprite, Color.white);
        tailEnd.sizeDelta = new Vector2(9f, 9f);

        // 마스크 안에 카드 상자. 그리는 순서: 선들 → 꼬리 끝 → 카드
        var maskRect = new GameObject("BoxMask", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        maskRect.SetParent(rect, false);

        var boxImage = new GameObject("Box", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<Image>();
        var boxRect = boxImage.rectTransform;
        boxRect.SetParent(maskRect, false);
        boxImage.raycastTarget = false;
        if (plateSprite)
        {
            boxImage.sprite = plateSprite;
            boxImage.type = Image.Type.Sliced;
            boxImage.pixelsPerUnitMultiplier = PlateScale;
            boxImage.color = Color.white;
        }
        else boxImage.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

        var layout = boxImage.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(52, 40, 14, 18); // 왼쪽 파형 아이콘 오른쪽부터, 오른쪽 깎인 모서리 안쪽까지
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = boxImage.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var nameText = CreateText("NameText", boxRect, font, "이름", 32f, NameColor, FontStyles.Bold);
        var profileText = CreateText("ProfileText", boxRect, font, "프로필 한 줄\n프로필 두 줄", 22f, ProfileColor, FontStyles.Normal);
        profileText.lineSpacing = 8f;

        var card = go.AddComponent<IntroCard>();
        var so = new SerializedObject(card);
        so.FindProperty("box").objectReferenceValue = boxRect;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("profileText").objectReferenceValue = profileText;
        so.FindProperty("tailSlant").objectReferenceValue = slant;
        so.FindProperty("tailFlat").objectReferenceValue = flat;
        so.FindProperty("tailEnd").objectReferenceValue = tailEnd;
        so.FindProperty("boxMask").objectReferenceValue = maskRect.GetComponent<RectMask2D>();
        so.ApplyModifiedPropertiesWithoutUndo();

        rect.SetAsLastSibling();
        go.SetActive(false);
        return card;
    }

    private static RectTransform CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, TMP_FontAsset font, string value, float size, Color color, FontStyles style)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        if (font) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
