using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 맵 > 대화창 새 스타일 적용
// 맵 대화창(DialogueCanvas 프리팹)을 지금 스타일로 맞춘다. 아래 세 단계를 순서대로 한 번에 한다.
//   ① 패널·스킨(ApplyPanelStyle) ② 화자 옆 이름표 켜기(EnableSpeakerTags) ③ 이름표 그림(ApplySpeakerTagArt)
// 맵 > 맵 부품 준비가 대화창·플레이어·NPC 프리팹을 새로 만들 때도 자동으로 부른다.
// ① 패널: "왼쪽 위 이름표 + 화면 아래 반투명 패널 + 오른쪽 아래 금색 ▼" 모양
// 1) 이름표·패널 이미지의 임포트 설정(Sprite, 패널 9-slice Border)을 맞추고
// 2) 스킨 에셋(MapDialogueSkin)을 만들어 그림·글자 색을 넣고
// 3) 프리팹에 스킨을 연결하고 배치(크기·위치)를 시안에 맞춘다.
// 여러 번 눌러도 된다. 스킨 에셋이 이미 있으면 재이가 바꾼 색은 건드리지 않고 비어 있는 그림만 채운다.
public static class DialogueStyleMenu
{
    private const string Folder = "Assets/Art/UI/Dialogue";
    private const string NameTagPath = Folder + "/NameTag.png";
    private const string PanelPath = Folder + "/TextPanel.png";
    private const string SkinPath = Folder + "/MapDialogueSkin.asset";
    private const string DialoguePrefab = "Assets/Prefabs/Map/DialogueCanvas.prefab";

    // 패널 9-slice: 왼쪽, 아래, 오른쪽, 위 (이미지 픽셀). 모서리 장식(리벳·꺾인 모서리)이 늘어나지 않는 두께
    private static readonly Vector4 PanelBorder = new Vector4(90f, 60f, 90f, 90f);

    [MenuItem("Tools/CapDemo/맵/대화창 새 스타일 적용", priority = 30)]
    private static void ApplyMapStyleMenu()
    {
        if (ApplyMapStyle(true))
            EditorUtility.DisplayDialog("대화창 새 스타일",
                "맵 대화창을 새 스타일로 맞췄어요(패널·스킨, 화자 옆 이름표, 이름표 그림).\n" +
                "색·폰트는 MapDialogueSkin 에셋에서, 위치·크기는 DialogueCanvas 프리팹에서 고칠 수 있어요.", "확인");
    }

    // 세 단계를 순서대로 한다. 중간에 실패하면 멈추고 false. showErrors가 false면 오류도 Console에만 남긴다
    internal static bool ApplyMapStyle(bool showErrors)
    {
        quiet = true;
        showQuietErrors = showErrors;
        try
        {
            return ApplyPanelStyle() && EnableSpeakerTags() && ApplySpeakerTagArt();
        }
        finally
        {
            quiet = false;
        }
    }

    private static bool quiet, showQuietErrors;

    // 단계별 알림: 한 번에 할 때(quiet)는 완료 알림은 Console에만, 오류는 showQuietErrors면 창으로도 띄운다
    private static void Notify(string title, string message, bool isError = false)
    {
        if (!quiet || (isError && showQuietErrors)) EditorUtility.DisplayDialog(title, message, "확인");
        else if (isError) Debug.LogWarning($"[CapDemo] {title}: {message}");
        else Debug.Log($"[CapDemo] {title}: {message}");
    }

    private static bool ApplyPanelStyle()
    {
        var plateSprite = ImportSprite(NameTagPath, Vector4.zero);
        var panelSprite = ImportSprite(PanelPath, PanelBorder);
        if (!plateSprite || !panelSprite)
        {
            Notify("대화창 새 스타일", $"{Folder} 폴더에서 NameTag.png / TextPanel.png를 찾지 못했어요.", true);
            return false;
        }

        var skin = PrepareSkin(plateSprite, panelSprite);

        var root = PrefabUtility.LoadPrefabContents(DialoguePrefab);
        try
        {
            var box = root.GetComponentInChildren<DialogueBox>(true);
            if (!box)
            {
                Notify("대화창 새 스타일", "DialogueCanvas 프리팹에서 DialogueBox를 찾지 못했어요.", true);
                return false;
            }
            // 처음 한 번만 배치·색을 시안대로 맞춘다. 이미 스킨이 연결돼 있으면 재이가 고친 위치·색을 덮어쓰지 않는다
            if (!new SerializedObject(box).FindProperty("skin").objectReferenceValue)
            {
                Layout(box, skin);
                CapturePanelLayout(box);
                box.ApplySkin();
            }
            box.ApplyStyle();
            PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = skin;
        Notify("대화창 새 스타일",
            "맵 대화창을 새 스타일로 바꿨어요.\n색·폰트는 선택된 MapDialogueSkin 에셋에서, 위치·크기는 DialogueCanvas 프리팹에서 고칠 수 있어요.");
        return true;
    }

    private const string PlayerPrefab = "Assets/Prefabs/Map/Player.prefab";
    private const string NpcPrefab = "Assets/Prefabs/Map/NPC.prefab";
    private const string GradientPath = Folder + "/BottomGradient.png";
    private const string LinePath = Folder + "/LineFade.png";
    private const string WavePath = Folder + "/WaveIcon.png";

    // 화자 머리 옆 이름표 준비: 플레이어·NPC 프리팹에 SpeakerTag를 붙이고, 대화창 프리팹에 이름표 UI를 만들어 켠다.
    // 되돌리기: DialogueCanvas 프리팹 > DialogueBox의 Name Beside Speaker 체크를 끈다(만든 것은 지우지 않고 꺼 두기만 한다).
    private static bool EnableSpeakerTags()
    {
        AddComponentToPrefab<SpeakerTag>(PlayerPrefab);
        AddComponentToPrefab<SpeakerTag>(NpcPrefab);

        var root = PrefabUtility.LoadPrefabContents(DialoguePrefab);
        try
        {
            var box = root.GetComponentInChildren<DialogueBox>(true);
            if (!box) return false;
            var so = new SerializedObject(box);
            var tagProp = so.FindProperty("speakerNameTag");
            if (!tagProp.objectReferenceValue)
            {
                var font = (so.FindProperty("nameText").objectReferenceValue as TMP_Text)?.font;
                tagProp.objectReferenceValue = CreateSpeakerNameTag(box.transform, font);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureBesideStyle(box);
            EnsureLineName(box);
            box.ApplyStyle();
            PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Notify("화자 옆 이름표",
            "화자 옆 이름표를 켰어요.");
        return true;
    }

    private const string SpeakerPlatePath = Folder + "/SpeakerPlate.png";
    private const string SpeakerTailPath = Folder + "/SpeakerTail.png";
    private const string SpeakerTailEndPath = Folder + "/SpeakerTailEnd.png";

    // 이름표 9-slice: 왼쪽, 아래, 오른쪽, 위 (262x82 이미지 픽셀). 왼쪽 ㄱ자 장식·밑줄·파형 아이콘, 오른쪽 깎인 모서리·ㄴ자 장식이 늘어나지 않는 두께
    private static readonly Vector4 SpeakerPlateBorder = new Vector4(74f, 40f, 64f, 38f);
    // 이름표 그림은 2배 크기로 그려져 있어서 화면에서는 절반 크기로 쓴다(Image.pixelsPerUnitMultiplier)
    private const float SpeakerPlateScale = 2f;

    // 화자 옆 이름표 그림 교체: 어두운 상자+테두리 → SpeakerPlate 그림, 꼬리 선 → SpeakerTail, 머리 점 → SpeakerTailEnd(마름모).
    // 펼침·접힘 애니메이션에 필요한 것(이름 상자를 감싸는 BoxMask, 흐리게 할 CanvasGroup)도 함께 붙인다.
    // 여러 번 눌러도 된다. 이름표 UI가 아직 없으면 먼저 만든다(켜고 끄는 것은 건드리지 않는다).
    private static bool ApplySpeakerTagArt()
    {
        var plateSprite = ImportSprite(SpeakerPlatePath, SpeakerPlateBorder);
        var tailSprite = ImportSprite(SpeakerTailPath, Vector4.zero);
        var tailEndSprite = ImportSprite(SpeakerTailEndPath, Vector4.zero);
        if (!plateSprite || !tailSprite || !tailEndSprite)
        {
            Notify("화자 옆 이름표 이미지",
                $"{Folder} 폴더에서 SpeakerPlate.png / SpeakerTail.png / SpeakerTailEnd.png를 찾지 못했어요.", true);
            return false;
        }

        var root = PrefabUtility.LoadPrefabContents(DialoguePrefab);
        try
        {
            var box = root.GetComponentInChildren<DialogueBox>(true);
            if (!box)
            {
                Notify("화자 옆 이름표 이미지", "DialogueCanvas 프리팹에서 DialogueBox를 찾지 못했어요.", true);
                return false;
            }

            var boxSo = new SerializedObject(box);
            var tagProp = boxSo.FindProperty("speakerNameTag");
            var tag = tagProp.objectReferenceValue as SpeakerNameTag;
            if (!tag) tag = box.GetComponentInChildren<SpeakerNameTag>(true);
            if (!tag)
            {
                var font = (boxSo.FindProperty("nameText").objectReferenceValue as TMP_Text)?.font;
                tag = CreateSpeakerNameTag(box.transform, font);
            }
            tagProp.objectReferenceValue = tag;
            boxSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(tag);
            var tagRect = (RectTransform)tag.transform;

            // 이름 상자 → 이름표 그림. 테두리(Outline)는 그림에 이미 있으니 끈다
            var plateRect = so.FindProperty("box").objectReferenceValue as RectTransform;
            if (plateRect)
            {
                var plate = plateRect.GetComponent<Image>();
                if (plate)
                {
                    plate.sprite = plateSprite;
                    plate.type = Image.Type.Sliced;
                    plate.fillCenter = true;
                    plate.pixelsPerUnitMultiplier = SpeakerPlateScale;
                    plate.color = Color.white;
                }
                var outline = plateRect.GetComponent<Outline>();
                if (outline) outline.enabled = false;
                // 이름이 왼쪽 파형 아이콘 오른쪽부터, 오른쪽 깎인 모서리 안쪽까지 오게
                var layout = plateRect.GetComponent<HorizontalLayoutGroup>();
                if (layout) layout.padding = new RectOffset(42, 32, 9, 9);
            }
            var nameText = so.FindProperty("nameText").objectReferenceValue as TMP_Text;
            if (nameText) nameText.color = new Color(0.878f, 0.91f, 0.894f);

            // 꼬리 선 두 개 → 꼬리 그림을 늘려 쓴다(곧은 선 한 토막이라 꺾인 곳에서 두 개로 이어 붙인다)
            foreach (var prop in new[] { "tailSlant", "tailFlat" })
            {
                var line = so.FindProperty(prop).objectReferenceValue as RectTransform;
                var image = line ? line.GetComponent<Image>() : null;
                if (!image) continue;
                image.sprite = tailSprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }

            // 머리 점 → 마름모 꼬리 끝. 선 위에 그려지도록 선 다음 순서에 둔다
            var tailEndProp = so.FindProperty("tailEnd");
            var tailEnd = tailEndProp.objectReferenceValue as RectTransform;
            if (!tailEnd)
            {
                var found = tagRect.Find("TailEnd");
                tailEnd = found ? (RectTransform)found : CreateLine("TailEnd", tagRect, Color.white);
                tailEndProp.objectReferenceValue = tailEnd;
            }
            var endImage = tailEnd.GetComponent<Image>();
            if (endImage)
            {
                endImage.sprite = tailEndSprite;
                endImage.type = Image.Type.Simple;
                endImage.color = Color.white;
                endImage.raycastTarget = false;
            }
            tailEnd.anchorMin = tailEnd.anchorMax = tailEnd.pivot = new Vector2(0.5f, 0.5f);
            tailEnd.anchoredPosition = Vector2.zero;
            tailEnd.sizeDelta = new Vector2(7f, 7f); // 14px 그림의 절반
            tailEnd.SetAsLastSibling();

            // 이름표를 꼬리 쪽에서부터 드러내는 마스크: Box를 BoxMask 안에 넣는다(이미 들어 있으면 그대로)
            if (plateRect)
            {
                var mask = plateRect.parent ? plateRect.parent.GetComponent<RectMask2D>() : null;
                if (!mask)
                {
                    var maskRect = new GameObject("BoxMask", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                    maskRect.SetParent(tagRect, false);
                    maskRect.anchorMin = maskRect.anchorMax = new Vector2(0.5f, 0.5f);
                    maskRect.pivot = plateRect.pivot;
                    maskRect.anchoredPosition = plateRect.anchoredPosition;
                    maskRect.sizeDelta = plateRect.sizeDelta;
                    plateRect.SetParent(maskRect, false);
                    plateRect.anchorMin = plateRect.anchorMax = plateRect.pivot;
                    plateRect.anchoredPosition = Vector2.zero;
                    mask = maskRect.GetComponent<RectMask2D>();
                }
                so.FindProperty("boxMask").objectReferenceValue = mask;
                mask.transform.SetAsLastSibling(); // 그리는 순서: 선들 → 꼬리 끝 → 이름표
            }

            // 말하지 않는 참가자 이름표를 흐리게 할 때 쓴다
            var group = tag.GetComponent<CanvasGroup>();
            if (!group) group = tag.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // 새 그림에 맞춘 거리·굵기(시안 프리팹 값)
            so.FindProperty("boxOffset").vector2Value = new Vector2(43f, 24f);
            so.FindProperty("lineWidth").floatValue = 1.7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 머리 옆 이름표가 없는 화자(맵에 없는 사람)는 왼쪽 위 금테 이름표 대신 하단 선 가운데에 이름을 띄운다
            EnsureLineName(box);
            box.ApplyStyle();

            PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Notify("화자 옆 이름표 이미지",
            "화자 옆 이름표 그림을 바꿨어요.\n거리·선 굵기는 DialogueCanvas 프리팹 > SpeakerNameTag에서, 글자 여백은 Box의 Horizontal Layout Group에서 고칠 수 있어요.");
        return true;
    }

    private const string MapStoryPrefab = "Assets/Prefabs/MapStoryCanvas.prefab";
    // 챕터 하단 그라데이션 진하기(0~1). 챕터는 일러스트가 화면을 꽉 채워서 맵보다 옅게 시작한다
    private const float ChapterGradientAlpha = 0.7f;

    // 챕터(스토리) 대화창을 맵과 같은 하단(그라데이션 + 가는 선 + 선 가운데 화자 이름 / 내레이션 파형 + 가운데 대사)으로 바꾼다.
    // 대상: 지금 열려 있는 씬의 대화창(프리팹 인스턴스는 빼고) + Assets/Prefabs/MapStoryCanvas.prefab이 있으면 그 안의 대화창.
    // 왼쪽 위 금테 이름표와 스탠딩 일러스트는 꺼 둔다(지우지는 않는다). 여러 번 눌러도 된다.
    [MenuItem("Tools/CapDemo/스토리/챕터 대화창 새 스타일 적용", priority = 40)]
    private static void ApplyChapterStyle()
    {
        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var box in root.GetComponentsInChildren<DialogueBox>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(box)) continue; // 맵 대화창·맵용 스토리 프리팹은 프리팹 쪽에서 바꾼다
                    UpgradeChapterBox(box);
                    changed = true;
                    count++;
                }
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(MapStoryPrefab))
        {
            var root = PrefabUtility.LoadPrefabContents(MapStoryPrefab);
            try
            {
                foreach (var box in root.GetComponentsInChildren<DialogueBox>(true))
                {
                    UpgradeChapterBox(box);
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, MapStoryPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        EditorUtility.DisplayDialog("챕터 대화창 새 스타일",
            count > 0
                ? $"대화창 {count}개를 새 스타일로 바꿨어요. 씬을 저장(Ctrl+S)해 주세요.\n" +
                  "되돌리려면 대화창(DialogueBox)의 Name Beside Speaker 체크를 끄면 예전 패널로 돌아가요."
                : "열려 있는 씬에서 바꿀 챕터 대화창을 찾지 못했어요. TutoScene처럼 StoryManager가 있는 씬을 열고 다시 눌러 주세요.", "확인");
    }

    // StoryMenu가 새 스토리 UI를 만들 때도 부른다
    internal static void UpgradeChapterBox(DialogueBox box)
    {
        bool created = EnsureBesideStyle(box);
        EnsureLineName(box);

        var so = new SerializedObject(box);
        var plate = so.FindProperty("namePlate").objectReferenceValue as GameObject;
        if (plate) plate.SetActive(false);
        var standing = so.FindProperty("standing").objectReferenceValue as Image;
        if (standing) standing.gameObject.SetActive(false); // 스탠딩 일러스트는 더 쓰지 않는다

        if (created)
        {
            // 처음 바꿀 때만: 맵과 같은 자리·크기, 그라데이션은 옅게 (그 뒤로는 재이가 고친 값을 건드리지 않는다)
            var rect = (RectTransform)box.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-40f, 250f);
            rect.anchoredPosition = new Vector2(0f, 16f);
            var decor = so.FindProperty("besideDecor").objectReferenceValue as GameObject;
            var bg = decor ? decor.transform.Find("Background") : null;
            var bgImage = bg ? bg.GetComponent<Image>() : null;
            if (bgImage)
            {
                var c = bgImage.color;
                c.a = ChapterGradientAlpha;
                bgImage.color = c;
            }
        }
        box.ApplyStyle();
        EditorUtility.SetDirty(box);
    }

    // 이름표 버전 하단 꾸밈(그라데이션·선·파형)과 배치가 없으면 만들고 Name Beside Speaker를 켠다. 새로 만들었으면 true
    private static bool EnsureBesideStyle(DialogueBox box)
    {
        var so = new SerializedObject(box);
        bool created = false;
        if (!so.FindProperty("besideDecor").objectReferenceValue)
        {
            // 처음 켤 때: 지금 패널 배치를 기억해 두고, 이름표 버전 하단 꾸밈과 배치를 만든다
            CapturePanelLayout(box); // 꾸밈이 없던 동안은 켜 있어도 배치가 패널 버전 그대로다
            so.Update();
            so.FindProperty("besideDecor").objectReferenceValue = CreateBesideDecor(box.transform);
            var beside = so.FindProperty("besideLayout");
            beside.FindPropertyRelative("bodyOffsetMin").vector2Value = new Vector2(140f, 55f);
            beside.FindPropertyRelative("bodyOffsetMax").vector2Value = new Vector2(-140f, -45f);
            beside.FindPropertyRelative("bodyAlignment").intValue = (int)TextAlignmentOptions.Center;
            beside.FindPropertyRelative("indicatorAnchor").vector2Value = new Vector2(0.5f, 0f);
            beside.FindPropertyRelative("indicatorPosition").vector2Value = new Vector2(0f, 24f);
            beside.FindPropertyRelative("indicatorSize").floatValue = 22f;
            created = true;
        }
        so.FindProperty("nameBesideSpeaker").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return created;
    }

    // 하단 선 가운데 화자 이름(DialogueLineName)을 만든다: 선을 왼쪽·오른쪽 절반(TopLine / TopLineRight)으로 나누고,
    // 틈 양 끝 마름모(GapCapLeft/Right)와 이름 글자(LineName)를 붙인다. 이미 있으면 설정만 다시 맞춘다
    private static DialogueLineName EnsureLineName(DialogueBox box)
    {
        var so = new SerializedObject(box);
        var decor = so.FindProperty("besideDecor").objectReferenceValue as GameObject;
        if (!decor) return null;
        var decorRect = (RectTransform)decor.transform;
        var leftTransform = decorRect.Find("TopLine");
        var left = leftTransform ? leftTransform.GetComponent<Image>() : null;
        if (!left) return null;

        SetHalfLine(left, Image.OriginHorizontal.Left);
        var rightTransform = decorRect.Find("TopLineRight");
        Image right = rightTransform ? rightTransform.GetComponent<Image>() : null;
        if (!right)
        {
            right = Object.Instantiate(left, decorRect);
            right.name = "TopLineRight";
            right.transform.SetSiblingIndex(left.transform.GetSiblingIndex() + 1);
        }
        SetHalfLine(right, Image.OriginHorizontal.Right);
        var lr = left.rectTransform;
        var rr = right.rectTransform;
        rr.anchorMin = lr.anchorMin;
        rr.anchorMax = lr.anchorMax;
        rr.pivot = lr.pivot;
        rr.anchoredPosition = lr.anchoredPosition;
        rr.sizeDelta = lr.sizeDelta;
        right.color = left.color;
        right.sprite = left.sprite;

        // 선 높이의 가운데 기준점
        var center = new Vector2(0.5f, lr.anchorMax.y);
        float lineY = lr.anchoredPosition.y;

        var capSprite = ImportSprite(SpeakerTailEndPath, Vector4.zero);
        var capLeft = FindOrCreateDecorImage("GapCapLeft", decorRect, capSprite, new Color(1f, 1f, 1f, 0.85f));
        var capRight = FindOrCreateDecorImage("GapCapRight", decorRect, capSprite, new Color(1f, 1f, 1f, 0.85f));
        foreach (var cap in new[] { capLeft, capRight })
        {
            cap.anchorMin = cap.anchorMax = center;
            cap.pivot = new Vector2(0.5f, 0.5f);
            cap.anchoredPosition = new Vector2(0f, lineY);
            cap.sizeDelta = new Vector2(7f, 7f);
            cap.localScale = Vector3.zero; // 실행 중에 틈이 열릴 때 커진다
        }

        var nameTransform = decorRect.Find("LineName");
        TMP_Text text = nameTransform ? nameTransform.GetComponent<TMP_Text>() : null;
        if (!text)
        {
            var go = new GameObject("LineName", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(decorRect, false);
            text = go.GetComponent<TMP_Text>();
        }
        var boxName = so.FindProperty("nameText").objectReferenceValue as TMP_Text;
        if (boxName && boxName.font) text.font = boxName.font;
        text.text = "";
        text.fontSize = 26f; // 대사보다 조금 작게
        text.color = new Color(0.93f, 0.95f, 0.94f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = textRect.anchorMax = center;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, lineY);
        textRect.sizeDelta = new Vector2(600f, 40f);

        var waveTransform = decorRect.Find("WaveIcon");
        var wave = waveTransform ? waveTransform.GetComponent<Graphic>() : null;

        // 그리는 순서: 선 → 마름모 → 이름
        capLeft.SetAsLastSibling();
        capRight.SetAsLastSibling();
        textRect.SetAsLastSibling();

        var lineName = decor.GetComponent<DialogueLineName>();
        if (!lineName) lineName = decor.AddComponent<DialogueLineName>();
        var lso = new SerializedObject(lineName);
        lso.FindProperty("lineLeft").objectReferenceValue = left;
        lso.FindProperty("lineRight").objectReferenceValue = right;
        lso.FindProperty("capLeft").objectReferenceValue = capLeft;
        lso.FindProperty("capRight").objectReferenceValue = capRight;
        lso.FindProperty("nameText").objectReferenceValue = text;
        lso.FindProperty("wave").objectReferenceValue = wave;
        lso.ApplyModifiedPropertiesWithoutUndo();

        so.FindProperty("lineName").objectReferenceValue = lineName;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lineName);
        return lineName;
    }

    private static void SetHalfLine(Image line, Image.OriginHorizontal origin)
    {
        line.type = Image.Type.Filled;
        line.fillMethod = Image.FillMethod.Horizontal;
        line.fillOrigin = (int)origin;
        line.fillAmount = 0.5f;
    }

    private static RectTransform FindOrCreateDecorImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var found = parent.Find(name);
        if (!found) return CreateDecorImage(name, parent, sprite, color);
        var image = found.GetComponent<Image>();
        if (image)
        {
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
        }
        return (RectTransform)found;
    }

    // 지금 BodyText·NextIndicator 자리를 패널 버전 배치로 기억한다
    private static void CapturePanelLayout(DialogueBox box)
    {
        var so = new SerializedObject(box);
        var body = so.FindProperty("bodyText").objectReferenceValue as TMP_Text;
        var indicator = so.FindProperty("nextIndicator").objectReferenceValue as Graphic;
        var panel = so.FindProperty("panelLayout");
        if (body)
        {
            panel.FindPropertyRelative("bodyOffsetMin").vector2Value = body.rectTransform.offsetMin;
            panel.FindPropertyRelative("bodyOffsetMax").vector2Value = body.rectTransform.offsetMax;
            panel.FindPropertyRelative("bodyAlignment").intValue = (int)body.alignment;
        }
        if (indicator)
        {
            panel.FindPropertyRelative("indicatorAnchor").vector2Value = indicator.rectTransform.anchorMin;
            panel.FindPropertyRelative("indicatorPosition").vector2Value = indicator.rectTransform.anchoredPosition;
            if (indicator is TMP_Text arrow) panel.FindPropertyRelative("indicatorSize").floatValue = arrow.fontSize;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 이름표 버전 하단: 아래로 갈수록 어두워지는 바탕 + 위쪽 가는 선 + 가운데 파형 아이콘. 대사 글씨 뒤에 깔리도록 맨 앞 자식으로 둔다
    private static GameObject CreateBesideDecor(Transform box)
    {
        var decor = new GameObject("BesideDecor", typeof(RectTransform));
        var rect = (RectTransform)decor.transform;
        rect.SetParent(box, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var bg = CreateDecorImage("Background", rect, ImportSprite(GradientPath, Vector4.zero), Color.white);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = new Vector2(-20f, -16f); // 화면 아래·양옆 끝까지
        bg.offsetMax = new Vector2(20f, 70f);

        var line = CreateDecorImage("TopLine", rect, ImportSprite(LinePath, Vector4.zero), new Color(1f, 1f, 1f, 0.45f));
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.anchoredPosition = new Vector2(0f, -18f);
        line.sizeDelta = new Vector2(-120f, 3f);

        var wave = CreateDecorImage("WaveIcon", rect, ImportSprite(WavePath, Vector4.zero), new Color(1f, 1f, 1f, 0.8f));
        wave.anchorMin = wave.anchorMax = new Vector2(0.5f, 1f);
        wave.anchoredPosition = new Vector2(0f, -18f);
        wave.sizeDelta = new Vector2(60f, 25f);

        decor.SetActive(false);
        return decor;
    }

    private static RectTransform CreateDecorImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private static void AddComponentToPrefab<T>(string path) where T : Component
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<T>()) return;
            root.AddComponent<T>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 시안: 어두운 상자 + 옅은 테두리 + 흰 이름, 머리에서 꺾인 선 꼬리
    private static SpeakerNameTag CreateSpeakerNameTag(Transform parent, TMP_FontAsset font)
    {
        var lineColor = new Color(0.85f, 0.85f, 0.9f, 0.9f);

        var go = new GameObject("SpeakerNameTag", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = Vector2.zero;
        rect.SetAsLastSibling();

        var slant = CreateLine("TailSlant", rect, lineColor);
        var flat = CreateLine("TailFlat", rect, lineColor);

        var boxImage = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Outline),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<Image>();
        var boxRect = boxImage.rectTransform;
        boxRect.SetParent(rect, false);
        boxImage.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);
        boxImage.raycastTarget = false;
        var outline = boxImage.GetComponent<Outline>();
        outline.effectColor = lineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var layout = boxImage.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 6, 6);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        var fitter = boxImage.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(boxRect, false);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        if (font) text.font = font;
        text.text = "화자";
        text.fontSize = 26f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        var tag = go.AddComponent<SpeakerNameTag>();
        var so = new SerializedObject(tag);
        so.FindProperty("box").objectReferenceValue = boxRect;
        so.FindProperty("nameText").objectReferenceValue = text;
        so.FindProperty("tailSlant").objectReferenceValue = slant;
        so.FindProperty("tailFlat").objectReferenceValue = flat;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
        return tag;
    }

    private static RectTransform CreateLine(string name, RectTransform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private static Sprite ImportSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear; // 큰 이미지를 줄여 쓰므로 Point보다 덜 깨진다
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096; // 패널 가로가 2093px이라 기본값(2048)이면 줄어든다
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static StorySkin PrepareSkin(Sprite plate, Sprite panel)
    {
        var skin = AssetDatabase.LoadAssetAtPath<StorySkin>(SkinPath);
        bool created = !skin;
        if (created)
        {
            skin = ScriptableObject.CreateInstance<StorySkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
        }

        var so = new SerializedObject(skin);
        if (!so.FindProperty("boxSprite").objectReferenceValue) so.FindProperty("boxSprite").objectReferenceValue = panel;
        if (!so.FindProperty("plateSprite").objectReferenceValue) so.FindProperty("plateSprite").objectReferenceValue = plate;
        if (created)
        {
            so.FindProperty("boxColor").colorValue = Color.white;
            so.FindProperty("plateColor").colorValue = Color.white;
            so.FindProperty("bodyColor").colorValue = Color.white;
            so.FindProperty("nameColor").colorValue = new Color(1f, 0.93f, 0.82f);
            so.FindProperty("indicatorColor").colorValue = new Color(1f, 0.72f, 0.25f);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skin);
        return skin;
    }

    // 1920x1080 기준 배치
    private static void Layout(DialogueBox box, StorySkin skin)
    {
        var so = new SerializedObject(box);
        so.FindProperty("skin").objectReferenceValue = skin;
        so.ApplyModifiedPropertiesWithoutUndo();
        var nameText = so.FindProperty("nameText").objectReferenceValue as TMP_Text;
        var plate = so.FindProperty("namePlate").objectReferenceValue as GameObject;
        var body = so.FindProperty("bodyText").objectReferenceValue as TMP_Text;
        var indicator = so.FindProperty("nextIndicator").objectReferenceValue as Graphic;

        // 패널: 화면 아래, 양옆 20씩만 남기고 가로 전체
        var boxRect = (RectTransform)box.transform;
        boxRect.anchorMin = new Vector2(0f, 0f);
        boxRect.anchorMax = new Vector2(1f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.sizeDelta = new Vector2(-40f, 250f);
        boxRect.anchoredPosition = new Vector2(0f, 16f);
        var boxImage = box.GetComponent<Image>();
        if (boxImage) boxImage.pixelsPerUnitMultiplier = 1f;

        // 이름표: 패널 왼쪽 위에 아래쪽 일부가 걸치게
        if (plate)
        {
            var plateRect = (RectTransform)plate.transform;
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 0f);
            plateRect.anchoredPosition = new Vector2(60f, -24f);
            plateRect.sizeDelta = new Vector2(332f, 76f);
            var plateImage = plate.GetComponent<Image>();
            if (plateImage) plateImage.preserveAspect = true;
        }

        // 이름: 이름표 왼쪽 III 장식 오른쪽부터
        if (nameText)
        {
            var r = nameText.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(95f, 4f);
            r.offsetMax = new Vector2(-20f, 0f);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.fontSize = 34f;
        }

        // 대사: 패널 테두리 안쪽
        if (body)
        {
            var r = body.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(70f, 50f);
            r.offsetMax = new Vector2(-90f, -62f);
            body.fontSize = 36f;
        }

        // ▼: 패널 오른쪽 아래
        if (indicator)
        {
            var r = indicator.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(-60f, 42f);
            r.sizeDelta = new Vector2(50f, 50f);
            if (indicator is TMP_Text arrow) arrow.fontSize = 28f;
        }
    }
}
