using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 맵 > 홀로그램 폰 만들기
// 맵 대화창(DialogueCanvas 프리팹) 오른쪽에 홀로그램(폰) 대화 화면(HoloPhone)을 만든다.
// 1) Assets/Art/UI/Holo 폴더에 임시 그림(패널·말풍선·스캔라인·프로필)을 만든다. 이미 있으면 건드리지 않는다
//    → 재이 그림으로 바꾸려면 같은 이름의 PNG로 덮어쓰면 된다.
// 2) 프리팹에 HoloPhone을 만들고 연결한다. 위치·크기·색은 프리팹의 HoloPhone에서 고친다.
//    미리 보려면 HoloPhone > Body를 잠깐 켠다(게임을 시작하면 알아서 꺼진다).
public static class HoloPhoneMenu
{
    private const string Folder = "Assets/Art/UI/Holo";
    private const string PanelPath = Folder + "/HoloPanel.png";
    private const string BubblePath = Folder + "/HoloBubble.png";
    private const string ScanlinePath = Folder + "/HoloScanline.png";
    private const string ProfilePath = Folder + "/HoloProfile.png";
    private const string MaskPath = Folder + "/HoloMask.png";
    private static readonly Vector4 PanelBorder = new Vector4(28f, 28f, 28f, 28f);
    private const string DialoguePrefab = "Assets/Prefabs/Map/DialogueCanvas.prefab";

    private static readonly Color Cyan = new Color(0.35f, 0.95f, 1f, 1f);

    [MenuItem("Tools/CapDemo/맵/홀로그램 폰 만들기", priority = 40)]
    private static void Create()
    {
        MakeArt();
        var panel = ImportSprite(PanelPath, PanelBorder);
        var mask = ImportSprite(MaskPath, PanelBorder);
        var bubble = ImportSprite(BubblePath, new Vector4(22f, 22f, 22f, 22f));
        var profile = ImportSprite(ProfilePath, Vector4.zero);
        var scanline = ImportScanline(ScanlinePath);

        var root = PrefabUtility.LoadPrefabContents(DialoguePrefab);
        try
        {
            var old = root.GetComponentInChildren<HoloPhone>(true);
            if (old)
            {
                int choice = EditorUtility.DisplayDialogComplex("홀로그램 폰",
                    "DialogueCanvas에 홀로그램 폰이 이미 있어요.\n" +
                    "고치기: 바꾼 값·연락처는 그대로 두고 끊어진 그림 연결, 둥근 모서리(9-slice), 스캔라인 모서리 가림만 맞춰요.\n" +
                    "새로 만들기: 지우고 처음 상태로 다시 만들어요.",
                    "고치기 (설정 유지)", "취소", "새로 만들기");
                if (choice == 1) return;
                if (choice == 0)
                {
                    Repair(old, panel, mask, bubble, profile, scanline);
                    PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefab);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("홀로그램 폰", "고쳤어요. 끊어진 그림 연결을 다시 잇고, 스캔라인이 둥근 패널 밖으로 나가지 않게 했어요.", "확인");
                    return;
                }
                Object.DestroyImmediate(old.gameObject);
            }

            TMP_FontAsset font = null;
            var box = root.GetComponentInChildren<DialogueBox>(true);
            if (box) font = (new SerializedObject(box).FindProperty("nameText").objectReferenceValue as TMP_Text)?.font;

            var holo = Build(root.transform, font, panel, mask, bubble, profile, scanline);

            var dialogue = root.GetComponent<DemoDialogue>();
            if (dialogue)
            {
                var so = new SerializedObject(dialogue);
                so.FindProperty("holo").objectReferenceValue = holo;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, DialoguePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("홀로그램 폰",
            "DialogueCanvas 프리팹에 홀로그램 폰을 만들었어요.\n" +
            "대화 에셋에서 줄마다 Holo Start / Holo Message / Holo End를 체크해서 쓰세요.\n" +
            "임시 그림은 Assets/Art/UI/Holo 폴더에 있어요. 같은 이름으로 덮어쓰면 바뀌어요.", "확인");
    }

    private const string MapStoryPrefab = "Assets/Prefabs/MapStoryCanvas.prefab";

    // Tools > CapDemo > 스토리 > 홀로그램 폰 붙이기
    // 챕터 화면(열린 씬의 StoryCanvas, 맵용 MapStoryCanvas 프리팹)에 홀로그램 폰을 붙인다.
    // 맵 대화창(DialogueCanvas)에 이미 만든 홀로그램이 있으면 그걸 복사해서, 재이가 바꾼 색·크기·연락처가 그대로 따라온다.
    // 이미 붙어 있는 곳은 건드리지 않는다. 챕터에서는 Holo Start 줄의 Holo Position·Holo Scale로 자리와 크기를 정한다.
    [MenuItem("Tools/CapDemo/스토리/홀로그램 폰 붙이기", priority = 60)]
    private static void AttachToStory()
    {
        MakeArt();
        var panel = ImportSprite(PanelPath, PanelBorder);
        var mask = ImportSprite(MaskPath, PanelBorder);
        var bubble = ImportSprite(BubblePath, new Vector4(22f, 22f, 22f, 22f));
        var profile = ImportSprite(ProfilePath, Vector4.zero);
        var scanline = ImportScanline(ScanlinePath);

        var dialoguePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefab);
        var source = dialoguePrefab ? dialoguePrefab.GetComponentInChildren<HoloPhone>(true) : null;

        HoloPhone Attach(StoryManager manager)
        {
            HoloPhone holo;
            if (source)
            {
                holo = Object.Instantiate(source, manager.transform, false);
                holo.name = "HoloPhone";
            }
            else
            {
                TMP_FontAsset font = null;
                if (manager.Dialogue) font = (new SerializedObject(manager.Dialogue).FindProperty("nameText").objectReferenceValue as TMP_Text)?.font;
                holo = Build(manager.transform, font, panel, mask, bubble, profile, scanline);
            }
            // 대사창보다 위, 검은 화면(페이드)보다 아래에 둔다
            var fade = manager.FadeOverlay;
            if (fade && fade.transform.parent == manager.transform) holo.transform.SetSiblingIndex(fade.transform.GetSiblingIndex());
            else holo.transform.SetAsLastSibling();

            var so = new SerializedObject(manager);
            so.FindProperty("holo").objectReferenceValue = holo;
            so.ApplyModifiedPropertiesWithoutUndo();
            return holo;
        }

        var report = new System.Text.StringBuilder();

        // 1) 맵용 스토리 화면 프리팹
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MapStoryPrefab))
        {
            var root = PrefabUtility.LoadPrefabContents(MapStoryPrefab);
            try
            {
                var manager = root.GetComponent<StoryManager>();
                if (!manager) report.AppendLine("· MapStoryCanvas 프리팹: StoryManager가 없어 건너뜀");
                else if (root.GetComponentInChildren<HoloPhone>(true)) report.AppendLine("· MapStoryCanvas 프리팹: 이미 있음");
                else
                {
                    Attach(manager);
                    PrefabUtility.SaveAsPrefabAsset(root, MapStoryPrefab);
                    report.AppendLine("· MapStoryCanvas 프리팹: 붙였어요");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 2) 열린 씬의 StoryCanvas (맵용 프리팹에서 나온 것은 위에서 프리팹째 붙였으니 건너뛴다)
        foreach (var manager in Object.FindObjectsByType<StoryManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(manager)) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(manager))
            {
                report.AppendLine($"· {manager.name} (씬): 프리팹에서 나온 것이라 프리팹 쪽을 따름");
                continue;
            }
            if (manager.GetComponentInChildren<HoloPhone>(true))
            {
                report.AppendLine($"· {manager.name} (씬 {manager.gameObject.scene.name}): 이미 있음");
                continue;
            }
            var holo = Attach(manager);
            Undo.RegisterCreatedObjectUndo(holo.gameObject, "홀로그램 폰 붙이기");
            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            report.AppendLine($"· {manager.name} (씬 {manager.gameObject.scene.name}): 붙였어요 → 씬을 저장해 주세요");
        }

        AssetDatabase.SaveAssets();
        if (report.Length == 0) report.AppendLine("열린 씬에 StoryCanvas가 없고 MapStoryCanvas 프리팹도 없어요.");
        EditorUtility.DisplayDialog("홀로그램 폰 붙이기",
            report + "\n챕터에서는 Holo Start 줄의 Holo Position(자리)·Holo Scale(크기)로 위치를 정해요.\n" +
            "챕터 인스펙터의 씬 미리보기에서 홀로그램 줄을 띄우면 씬 뷰의 청록 네모를 끌어서 옮길 수 있어요.", "확인");
    }

    private static HoloPhone Build(Transform parent, TMP_FontAsset font, Sprite panel, Sprite mask, Sprite bubble, Sprite profile, Texture2D scanline)
    {
        // 본체 자리: 화면 오른쪽 (1920x1080 기준 540x700)
        var go = new GameObject("HoloPhone", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.SetAsLastSibling();
        rect.anchorMin = rect.anchorMax = new Vector2(0.74f, 0.57f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(540f, 700f);
        rect.anchoredPosition = Vector2.zero;

        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasGroup));
        var body = (RectTransform)bodyGo.transform;
        body.SetParent(rect, false);
        Stretch(body, Vector2.zero, Vector2.zero);
        var group = bodyGo.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var frame = NewImage("Frame", body, panel, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
        Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);

        // 스캔라인은 둥근 패널 모양 마스크 안에서만 보인다 (모서리 밖으로 삐져나오지 않게)
        var scanMask = CreateScanMask(body, mask);
        var scan = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        scan.rectTransform.SetParent(scanMask, false);
        Stretch(scan.rectTransform, Vector2.zero, Vector2.zero);
        scan.texture = scanline;
        scan.color = new Color(0.6f, 1f, 1f, 0.12f);
        scan.raycastTarget = false;

        // 머리: 상대 이름 + 작은 표시 + 아래 선
        var header = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
        header.SetParent(body, false);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 86f);
        header.anchoredPosition = Vector2.zero;

        var title = NewText("Title", header, font, "상대 이름", 34f, new Color(0.88f, 1f, 1f, 1f), TextAlignmentOptions.MidlineLeft);
        Stretch(title.rectTransform, new Vector2(30f, 0f), new Vector2(-150f, 0f));

        var tag = NewText("LinkLabel", header, font, "● HOLO-LINK", 18f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f), TextAlignmentOptions.MidlineRight);
        Stretch(tag.rectTransform, new Vector2(300f, 0f), new Vector2(-28f, 0f));

        var line = NewImage("Line", header, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f));
        var lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(-40f, 2f);
        lineRect.anchoredPosition = Vector2.zero;

        // 말풍선 영역: 위로 넘친 말풍선은 잘린다
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(body, false);
        Stretch(viewport, new Vector2(6f, 58f), new Vector2(-6f, -96f));

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 0f);
        content.pivot = new Vector2(0.5f, 0f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = new Vector2(0f, 8f);

        var indicator = NewText("NextIndicator", body, font, "▼", 22f, Cyan, TextAlignmentOptions.Center);
        var indRect = indicator.rectTransform;
        indRect.anchorMin = indRect.anchorMax = new Vector2(1f, 0f);
        indRect.sizeDelta = new Vector2(40f, 40f);
        indRect.anchoredPosition = new Vector2(-34f, 30f);

        var holo = go.AddComponent<HoloPhone>();
        var so = new SerializedObject(holo);
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("nextIndicator").objectReferenceValue = indicator;
        so.FindProperty("scanlines").objectReferenceValue = scan;
        so.FindProperty("bubbleSprite").objectReferenceValue = bubble;
        so.FindProperty("defaultProfile").objectReferenceValue = profile;
        so.FindProperty("font").objectReferenceValue = font;
        so.ApplyModifiedPropertiesWithoutUndo();

        bodyGo.SetActive(false); // 평소에는 꺼 둔다. 게임 중에는 HoloPhone이 켜고 끈다
        return holo;
    }

    // 둥근 패널 모양 마스크. 마스크 그림 자체는 보이지 않는다
    private static RectTransform CreateScanMask(RectTransform body, Sprite mask)
    {
        var image = NewImage("ScanMask", body, mask, Color.white);
        Stretch(image.rectTransform, Vector2.zero, Vector2.zero);
        image.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        return image.rectTransform;
    }

    // 이미 만든 홀로그램 폰 고치기: 끊어진 그림 연결을 다시 잇고, 패널을 9-slice로 바꾸고, 스캔라인을 둥근 마스크 안으로 옮긴다.
    // 재이가 다른 그림으로 바꿔 둔 칸은 건드리지 않는다(비어 있는 칸만 채운다)
    private static void Repair(HoloPhone holo, Sprite panel, Sprite mask, Sprite bubble, Sprite profile, Texture2D scanline)
    {
        var so = new SerializedObject(holo);
        var body = so.FindProperty("body").objectReferenceValue as RectTransform;
        if (!body) return;

        FillIfEmpty(so.FindProperty("bubbleSprite"), bubble);
        FillIfEmpty(so.FindProperty("defaultProfile"), profile);
        so.ApplyModifiedPropertiesWithoutUndo();

        var frame = body.Find("Frame")?.GetComponent<Image>();
        if (frame && !frame.sprite) frame.sprite = panel;
        var oldMask = body.Find("ScanMask")?.GetComponent<Image>();
        if (oldMask && !oldMask.sprite) oldMask.sprite = mask;
        var scanImage = so.FindProperty("scanlines").objectReferenceValue as RawImage;
        if (scanImage && !scanImage.texture) scanImage.texture = scanline;

        foreach (var image in body.GetComponentsInChildren<Image>(true))
            if (image.sprite && image.sprite.border != Vector4.zero && image.type == Image.Type.Simple)
                image.type = Image.Type.Sliced;

        var scan = so.FindProperty("scanlines").objectReferenceValue as RawImage;
        if (scan && !scan.GetComponentInParent<Mask>(true))
        {
            int index = scan.transform.GetSiblingIndex();
            var scanMask = CreateScanMask(body, mask);
            scanMask.SetSiblingIndex(index);
            scan.rectTransform.SetParent(scanMask, false);
            Stretch(scan.rectTransform, Vector2.zero, Vector2.zero);
        }
    }

    private static void FillIfEmpty(SerializedProperty property, Object value)
    {
        if (property != null && !property.objectReferenceValue) property.objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
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
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    // ── 임시 그림 만들기 (흰색으로 만들고 색은 Image Color로 입힌다) ──

    private static void MakeArt()
    {
        Directory.CreateDirectory(Folder);
        // 패널: 반투명 안쪽 + 밝은 테두리
        WriteIfMissing(PanelPath, 128, 128, (x, y) =>
        {
            float d = RoundRect(x, y, 128, 128, 22f);
            float fill = Smooth(-d) * 0.22f;
            float border = Smooth(1.5f - Mathf.Abs(d + 1.5f));
            return Mathf.Max(fill, border);
        });
        // 말풍선: 둥근 사각형, 테두리를 조금 더 밝게
        WriteIfMissing(BubblePath, 64, 64, (x, y) =>
        {
            float d = RoundRect(x, y, 64, 64, 18f);
            float fill = Smooth(-d) * 0.7f;
            float border = Smooth(1f - Mathf.Abs(d + 1f));
            return Mathf.Max(fill, border);
        });
        // 마스크: 패널과 같은 모양의 꽉 찬 둥근 사각형 (스캔라인을 이 모양으로 자른다)
        WriteIfMissing(MaskPath, 128, 128, (x, y) => Smooth(-RoundRect(x, y, 128, 128, 22f)));
        // 스캔라인: 4픽셀마다 밝은 줄 하나 (반복해서 쓴다)
        WriteIfMissing(ScanlinePath, 4, 4, (x, y) => y == 3 ? 1f : 0f);
        // 프로필: 동그라미 안 사람 실루엣 (인스타그램 기본 프로필 느낌)
        WriteIfMissing(ProfilePath, 128, 128, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float circle = Vector2.Distance(p, new Vector2(64f, 64f)) - 62f;
            if (circle > 1f) return 0f;
            float inside = Smooth(-circle);
            float ring = Smooth(1.5f - Mathf.Abs(circle + 2f));
            float head = Smooth(22f - Vector2.Distance(p, new Vector2(64f, 80f)));
            Vector2 s = new Vector2((p.x - 64f) / 42f, (p.y - 18f) / 36f);
            float shoulders = Smooth((1f - s.magnitude) * 36f);
            float person = Mathf.Max(head, shoulders) * inside;
            return Mathf.Max(Mathf.Max(inside * 0.3f, ring), person * 0.95f);
        });
        AssetDatabase.Refresh();
    }

    private static void WriteIfMissing(string path, int w, int h, System.Func<int, int, float> alpha)
    {
        if (File.Exists(path)) return;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y))));
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // 둥근 사각형까지의 거리 (안쪽이 음수)
    private static float RoundRect(int x, int y, int w, int h, float r)
    {
        Vector2 p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
        Vector2 q = new Vector2(Mathf.Abs(p.x) - (w * 0.5f - r), Mathf.Abs(p.y) - (h * 0.5f - r));
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float Smooth(float v) => Mathf.Clamp01(v + 0.5f);

    private static Sprite ImportSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        // 처음 한 번만 설정한다. 재이가 그림을 바꾸고 직접 고친 Border는 건드리지 않는다
        bool first = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single;
        bool needBorder = border != Vector4.zero && importer.spriteBorder == Vector4.zero;
        if (first || needBorder)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (needBorder) importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D ImportScanline(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
