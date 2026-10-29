using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 상단 메뉴 Tools > CapDemo > 데모 > 어드벤처 아트 적용
// Assets/image/Adventure_asset 의 배경·오브젝트·바닥 이미지로 DemoScene을 꾸민다.
// 회색 블록(Ground, 양쪽 벽)은 충돌용으로 남기고 그림만 끄며, 공중 발판은 지운다.
// 여러 번 실행해도 Map을 지우고 다시 만들기 때문에 결과는 같다.
public static class DemoArtMenu
{
    private const string ScenePath = "Assets/Scenes/DemoScene.unity";
    private const string ArtFolder = "Assets/image/Adventure_asset";
    private const string BackgroundName = "Adv_Background";
    private const string ObjectsName = "Adv_Objects";
    private const string FloorName = "Adv_Floor";

    // 처음 받은 이미지 이름 → 정리할 이름
    private static readonly (string from, string to)[] Renames =
    {
        ("ChatGPT 이미지 2026년 10월 13일 오후 06_21_18", BackgroundName),
        ("ChatGPT 이미지 2026년 10월 13일 오후 06_21_41", ObjectsName),
        ("ChatGPT 이미지 2026년 10월 13일 오후 06_22_02", FloorName),
    };

    // 1672x941 이미지 높이가 10유닛, 즉 카메라 Orthographic Size 5의 한 화면에 맞는 값
    private const float PixelsPerUnit = 94f;
    private const float ImageWidth = 1672f / PixelsPerUnit;
    private const float ImageCenterPx = 941f / 2f;
    // 이미지 위에서부터 바닥 윗면, 오브젝트 밑면까지의 픽셀 수
    private const float FloorTopPx = 677f;
    private const float ObjectsBottomPx = 638f;
    // 오브젝트가 바닥 위에 떠 보이지 않게 살짝 묻는 깊이(px)
    private const float ObjectsSinkPx = 2f;

    private static readonly string[] HiddenBlocks = { "Ground", "WallLeft", "WallRight" };
    private static readonly string[] RemovedBlocks = { "Platform1", "Platform2", "Platform3" };

    [MenuItem("Tools/CapDemo/데모/어드벤처 아트 적용")]
    private static void ApplyFromMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Apply();
    }

    // 현재 열린 DemoScene에 아트를 배치하고 저장한다. DemoMenu에서 씬을 만든 직후에도 부른다.
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        RenameImages();
        var background = LoadSprite(BackgroundName);
        var objects = LoadSprite(ObjectsName);
        var floor = LoadSprite(FloorName);
        if (!background || !objects || !floor)
        {
            Debug.LogError($"[CapDemo] {ArtFolder} 에서 {BackgroundName}, {ObjectsName}, {FloorName} 이미지를 찾지 못했습니다.");
            return;
        }

        Camera camera = null;
        Transform level = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Map") Object.DestroyImmediate(root);
            else if (root.name == "Level") level = root.transform;
            else if (!camera) camera = root.GetComponent<Camera>();
        }

        if (level)
        {
            foreach (var name in RemovedBlocks)
            {
                var block = level.Find(name);
                if (block) Object.DestroyImmediate(block.gameObject);
            }
            foreach (var name in HiddenBlocks)
            {
                var block = level.Find(name);
                if (block && block.TryGetComponent(out SpriteRenderer renderer)) renderer.enabled = false;
            }
        }

        var map = new GameObject("Map").transform;

        // 원경: 카메라를 90% 따라가며 천천히 지나간다. 따라가며 생기는 틈을 덮도록 크게 키운다.
        var backgroundGo = CreateSprite("Background", map, background, Vector2.zero, -30);
        backgroundGo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        var parallax = backgroundGo.AddComponent<ParallaxLayer>();
        if (camera)
        {
            var so = new SerializedObject(parallax);
            so.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.11f);
        }

        // 건물·소품: 밑면을 바닥 윗면(y=0)에 맞춰 이어 붙인다
        var objectsRoot = new GameObject("Objects").transform;
        objectsRoot.SetParent(map, false);
        float objectsY = (ObjectsBottomPx + ObjectsSinkPx - ImageCenterPx) / PixelsPerUnit;
        for (int i = -1; i <= 1; i++)
            CreateSprite($"Objects{i + 2}", objectsRoot, objects, new Vector2(i * ImageWidth, objectsY), -20);

        // 바닥: 윗면이 Ground 충돌체 윗면(y=0)과 맞도록 놓고, 카메라가 벽 끝까지 가도 비지 않게 5장 잇는다
        var floorRoot = new GameObject("Floor").transform;
        floorRoot.SetParent(map, false);
        float floorY = (FloorTopPx - ImageCenterPx) / PixelsPerUnit;
        for (int i = -2; i <= 2; i++)
            CreateSprite($"Floor{i + 3}", floorRoot, floor, new Vector2(i * ImageWidth, floorY), -10);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CapDemo] DemoScene에 어드벤처 아트를 배치했습니다.");
    }

    private static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Vector2 position,
        int sortingOrder)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static void RenameImages()
    {
        foreach (var (from, to) in Renames)
        {
            string path = $"{ArtFolder}/{from}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) && !AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{to}.png"))
            {
                string error = AssetDatabase.RenameAsset(path, to);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[CapDemo] {path} 이름 변경 실패: {error}");
            }
        }
    }

    // 픽셀아트가 흐려지지 않게 Point 필터, 무압축으로 불러온다
    private static Sprite LoadSprite(string name)
    {
        string path = $"{ArtFolder}/{name}.png";
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != PixelsPerUnit ||
            importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
