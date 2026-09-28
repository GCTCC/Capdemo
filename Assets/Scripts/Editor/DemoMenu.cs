using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 에디터 상단 메뉴 Tools > CapDemo > 데모
// 횡스크롤 이동·NPC 대화 테스트용 DemoScene을 만든다. 다른 씬과 연결하지 않고 Build Settings에도 넣지 않는다.
public static class DemoMenu
{
    private const string ScenePath = "Assets/Scenes/DemoScene.unity";
    private const string AssetFolder = "Assets/Demo";
    private const string SquarePath = AssetFolder + "/Square.png";
    private const string NoFrictionPath = AssetFolder + "/NoFriction.physicsMaterial2D";

    [MenuItem("Tools/CapDemo/데모/DemoScene 만들기")]
    private static void CreateDemoScene()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("씬 덮어쓰기",
                ScenePath + " 을(를) 새로 만듭니다.", "덮어쓰기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory(AssetFolder);
        var square = LoadOrCreateSquare();
        var noFriction = LoadOrCreateNoFriction();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StoryMenu.FontPath);
        if (!font) Debug.LogWarning($"[CapDemo] {StoryMenu.FontPath} 폰트를 찾지 못해 기본 폰트를 사용합니다.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FollowCamera2D))
            { tag = "MainCamera" };
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = cameraGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f);

        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;

        // 지형: 바닥(윗면 y=0), 양쪽 벽, 발판
        var groundColor = new Color(0.35f, 0.35f, 0.38f);
        var level = new GameObject("Level").transform;
        CreateBlock("Ground", level, square, new Vector2(0f, -0.5f), new Vector2(40f, 1f), groundColor);
        CreateBlock("WallLeft", level, square, new Vector2(-20.5f, 5f), new Vector2(1f, 12f), groundColor);
        CreateBlock("WallRight", level, square, new Vector2(20.5f, 5f), new Vector2(1f, 12f), groundColor);
        CreateBlock("Platform1", level, square, new Vector2(-6f, 1.8f), new Vector2(3f, 0.4f), groundColor);
        CreateBlock("Platform2", level, square, new Vector2(9f, 1.6f), new Vector2(3f, 0.4f), groundColor);
        CreateBlock("Platform3", level, square, new Vector2(13f, 3.2f), new Vector2(3f, 0.4f), groundColor);

        // 대사창
        var canvasGo = new GameObject("DialogueCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(DemoDialogue));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var box = StoryMenu.CreateDialogueBox(canvasGo.transform, font, null);

        // 플레이어: 파란 사각형
        var player = CreateSprite("Player", null, square, new Vector2(-8f, 0.8f), new Vector2(0.8f, 1.6f),
            new Color(0.2f, 0.4f, 0.95f), 10);
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 3f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sharedMaterial = noFriction;
        player.AddComponent<BoxCollider2D>();
        var controller = player.AddComponent<PlayerController2D>();

        // NPC: 초록 사각형 + 대화 범위(트리거) + 머리 위 안내 표시
        var npc = new GameObject("NPC", typeof(BoxCollider2D), typeof(NpcInteract));
        npc.transform.position = new Vector3(3f, 0.8f, 0f);
        var area = npc.GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(3f, 2f);
        CreateSprite("Body", npc.transform, square, Vector2.zero, new Vector2(0.8f, 1.6f),
            new Color(0.25f, 0.75f, 0.35f), 5);
        var prompt = CreatePrompt(npc.transform, font);

        // 참조 연결
        Assign(controller, "dialogue", canvasGo.GetComponent<DemoDialogue>());
        Assign(canvasGo.GetComponent<DemoDialogue>(), "box", box);
        Assign(canvasGo.GetComponent<DemoDialogue>(), "followCamera", cameraGo.GetComponent<FollowCamera2D>());
        Assign(cameraGo.GetComponent<FollowCamera2D>(), "target", player.transform);

        var npcSo = new SerializedObject(npc.GetComponent<NpcInteract>());
        npcSo.FindProperty("prompt").objectReferenceValue = prompt;
        var lines = npcSo.FindProperty("lines");
        lines.arraySize = 3;
        StoryMenu.SetLine(lines.GetArrayElementAtIndex(0), "주민", null, "어서 와. 이 근처는 처음이지?");
        StoryMenu.SetLine(lines.GetArrayElementAtIndex(1), "주민", null, "A, D로 움직이고 스페이스바로 점프할 수 있어.");
        StoryMenu.SetLine(lines.GetArrayElementAtIndex(2), "주민", null, "궁금한 게 생기면 언제든 말 걸어.");
        npcSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[CapDemo] {ScenePath} 를 만들었습니다. 플레이해서 이동·점프와 NPC 대화를 확인하세요.");
    }

    private static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Vector2 position,
        Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static void CreateBlock(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size,
        Color color)
    {
        CreateSprite(name, parent, sprite, position, size, color, 0).AddComponent<BoxCollider2D>();
    }

    private static GameObject CreatePrompt(Transform npc, TMP_FontAsset font)
    {
        var go = new GameObject("Prompt", typeof(TextMeshPro));
        go.transform.SetParent(npc, false);
        go.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        var text = go.GetComponent<TextMeshPro>();
        if (font) text.font = font;
        text.text = "Space";
        text.fontSize = 3f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.1f, 0.1f, 0.12f);
        text.rectTransform.sizeDelta = new Vector2(3f, 1f);
        text.sortingOrder = 20;
        go.SetActive(false);
        return go;
    }

    private static void Assign(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Create > 2D > Sprites > Square와 같은 1x1 유닛 흰 사각형
    private static Sprite LoadOrCreateSquare()
    {
        if (!File.Exists(SquarePath))
        {
            var texture = new Texture2D(4, 4);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels32(pixels);
            File.WriteAllBytes(SquarePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SquarePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
    }

    // 공중에서 벽 쪽으로 이동할 때 벽에 걸려 멈추는 것을 막는 마찰 0 재질
    private static PhysicsMaterial2D LoadOrCreateNoFriction()
    {
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
        if (material) return material;
        material = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(material, NoFrictionPath);
        return material;
    }
}
