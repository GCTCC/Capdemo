using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 허브
// 허브 코르크보드 화면에 종이·핀 그림자와 램프 조명을 얹어 명암을 준다.
public static class HubDepthMenu
{
    private const string EffectsFolder = "Assets/image/Hub/Effects/";
    private const string CardShadowPath = EffectsFolder + "CardShadow.png";
    private const string PinShadowPath = EffectsFolder + "PinShadow.png";
    private const string LampLightPath = EffectsFolder + "LampLight.png";

    private const string LampLightName = "LampLight";
    private const string FadeOverlayName = "FadeOverlay";

    [MenuItem("Tools/CapDemo/허브/선택한 종이에 그림자 추가")]
    private static void AddCardShadows()
    {
        var sprite = LoadEffectSprite(CardShadowPath, new Vector4(96f, 96f, 96f, 96f));
        if (!sprite) return;
        AddShadows(sprite, Image.Type.Sliced, 48f, new Vector2(10f, -12f), 0.45f);
    }

    [MenuItem("Tools/CapDemo/허브/선택한 핀에 그림자 추가")]
    private static void AddPinShadows()
    {
        var sprite = LoadEffectSprite(PinShadowPath, null);
        if (!sprite) return;
        AddShadows(sprite, Image.Type.Simple, 6f, new Vector2(4f, -6f), 0.5f);
    }

    [MenuItem("Tools/CapDemo/허브/램프 조명 오버레이 추가")]
    private static void AddLampLight()
    {
        var selected = Selection.activeGameObject;
        var canvas = selected ? selected.GetComponentInParent<Canvas>() : null;
        if (!canvas)
        {
            EditorUtility.DisplayDialog("램프 조명", "Hub Canvas 안의 오브젝트를 선택한 뒤 실행하세요", "확인");
            return;
        }

        var root = canvas.rootCanvas.transform;
        if (root.Find(LampLightName))
        {
            EditorUtility.DisplayDialog("램프 조명", $"{root.name} 아래에 이미 {LampLightName}가 있습니다.", "확인");
            return;
        }

        var sprite = LoadEffectSprite(LampLightPath, null);
        if (!sprite) return;

        var go = new GameObject(LampLightName, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "램프 조명 오버레이 추가");
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;

        // FadeOverlay가 있으면 그 바로 뒤(먼저 그려지게), 없으면 맨 앞
        var fade = root.Find(FadeOverlayName);
        if (fade) rect.SetSiblingIndex(fade.GetSiblingIndex());
        else rect.SetAsLastSibling();

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // 선택한 각 오브젝트 바로 뒤에 같은 모양으로 조금 크게 퍼진 그림자를 만든다
    private static void AddShadows(Sprite sprite, Image.Type type, float spread, Vector2 offset, float alpha)
    {
        int created = 0;
        foreach (var target in Selection.gameObjects)
        {
            var targetRect = target.GetComponent<RectTransform>();
            var parent = targetRect ? targetRect.parent : null;
            if (!parent) continue;

            string shadowName = target.name + "_Shadow";
            if (parent.Find(shadowName)) continue;

            var go = new GameObject(shadowName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "그림자 추가");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(targetRect.GetSiblingIndex());

            rect.anchorMin = targetRect.anchorMin;
            rect.anchorMax = targetRect.anchorMax;
            rect.pivot = targetRect.pivot;
            rect.localRotation = targetRect.localRotation;
            rect.localScale = targetRect.localScale;
            rect.sizeDelta = targetRect.sizeDelta + new Vector2(2f * spread, 2f * spread);

            // 크기를 키운 만큼 pivot 기준으로 밀리는 위치를 되돌려 가운데를 맞춘다
            Vector2 corr = (targetRect.pivot * 2f - Vector2.one) * spread;
            corr = targetRect.localRotation * corr;
            rect.anchoredPosition = targetRect.anchoredPosition + corr + offset;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = false;

            EditorSceneManager.MarkSceneDirty(go.scene);
            created++;
        }

        if (created == 0)
            EditorUtility.DisplayDialog("그림자 추가", "그림자를 만들 UI 오브젝트를 선택하세요. 이미 그림자가 있는 오브젝트는 건너뜁니다.", "확인");
    }

    // 임포트 설정을 스프라이트용으로 맞춘 뒤 불러온다
    private static Sprite LoadEffectSprite(string path, Vector4? border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer)
        {
            Debug.LogError($"[HubDepth] {path} 파일을 찾지 못했습니다.");
            return null;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (border.HasValue && importer.spriteBorder != border.Value) { importer.spriteBorder = border.Value; changed = true; }
        if (changed) importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!sprite) Debug.LogError($"[HubDepth] {path}를 스프라이트로 불러오지 못했습니다.");
        return sprite;
    }
}
