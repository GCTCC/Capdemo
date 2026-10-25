using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 에디터 상단 메뉴 Tools > CapDemo > 맵
// 구역(씬)을 만들고, 맵 부품 프리팹(플레이어·대화창·맵 오브젝트·NPC·포탈·도착 지점)과 그리기 순서(Sorting Layer)를 준비한다.
// 프리팹은 없을 때만 만들기 때문에, 직접 고친 프리팹을 덮어쓰지 않는다.
public static class MapMenu
{
    private const string SceneFolder = "Assets/Scenes/Map";
    private const string PrefabFolder = "Assets/Prefabs/Map";
    private const string DialogueFolder = "Assets/Dialogue";
    private const string PlayerControllerPath = "Assets/Animations/Player/Player.controller";
    private const string PlayerIdlePath = "Assets/image/Character/Player/Idle_Breathing_Sheet.png";

    private const string PlayerPrefab = PrefabFolder + "/Player.prefab";
    private const string DialoguePrefab = PrefabFolder + "/DialogueCanvas.prefab";
    private const string ObjectPrefab = PrefabFolder + "/MapObject.prefab";
    private const string NpcPrefab = PrefabFolder + "/NPC.prefab";
    private const string PortalPrefab = PrefabFolder + "/Portal.prefab";
    private const string SpawnPrefab = PrefabFolder + "/SpawnPoint.prefab";

    // 뒤에 있을수록 먼저(뒤에) 그린다. Default는 맨 뒤에 남는다
    private static readonly string[] SortingLayers =
        { MapBackground.SortingLayer, MapObject.BackLayer, NpcLayout.SortingLayer, MapObject.FrontLayer };

    // 새 씬에서 배경 그림의 기본 바닥 높이(MapBackground 기본값과 같다): 화면 아래끝 -5에서 2 위
    private const float DefaultGroundY = -3f;
    // 플레이어 루트는 몸 중심, 발끝은 0.8 아래
    private const float PlayerHalfHeight = 0.8f;

    [MenuItem("Tools/CapDemo/맵/새 구역 씬 만들기", priority = 0)]
    private static void CreateAreaScene()
    {
        Directory.CreateDirectory(SceneFolder);
        string path = EditorUtility.SaveFilePanelInProject("새 구역 씬", "Area_", "unity",
            "새 구역 씬 이름을 정하세요. (예: Area_Market)", SceneFolder);
        if (string.IsNullOrEmpty(path)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!PrepareParts()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(FollowCamera2D))
            { tag = "MainCamera" };
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = cameraGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;

        new GameObject("Background", typeof(SpriteRenderer), typeof(EdgeCollider2D), typeof(MapBackground));
        new GameObject("Objects");
        new GameObject("NPCs");
        var portals = new GameObject("Portals").transform;

        var spawn = (GameObject)PrefabUtility.InstantiatePrefab(Load(SpawnPrefab), portals);
        spawn.name = "Spawn_Start";
        spawn.transform.position = new Vector3(0f, DefaultGroundY + PlayerHalfHeight, 0f);

        var player = (GameObject)PrefabUtility.InstantiatePrefab(Load(PlayerPrefab));
        player.transform.position = spawn.transform.position;
        PrefabUtility.InstantiatePrefab(Load(DialoguePrefab));

        EditorSceneManager.SaveScene(scene, path);
        AddToBuildSettings(path);
        Selection.activeGameObject = GameObject.Find("Background");
        Debug.Log($"[CapDemo] {path} 를 만들었습니다. Background의 Sprite 칸에 배경 그림을 넣으세요.");
    }

    [MenuItem("Tools/CapDemo/맵/맵 부품 준비 (프리팹·그리기 순서·폴더)", priority = 1)]
    private static void PreparePartsFromMenu()
    {
        if (PrepareParts())
            Debug.Log($"[CapDemo] 맵 부품을 준비했습니다. 프리팹은 {PrefabFolder} 에 있습니다.");
    }

    // 선택한 오브젝트를 아래로 내려 발밑(그림 아래끝)을 바로 아래 바닥·오브젝트 윗면에 붙인다
    [MenuItem("Tools/CapDemo/맵/선택한 오브젝트 바닥에 붙이기", priority = 20)]
    private static void DropSelectionToGround()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (!go.TryGetComponent(out SpriteRenderer renderer) || !renderer.sprite) continue;
            var bounds = renderer.bounds;
            var origin = new Vector2(bounds.center.x, bounds.min.y + 0.01f);
            var own = go.GetComponentsInChildren<Collider2D>();
            var hit = Physics2D.RaycastAll(origin, Vector2.down, 100f)
                .FirstOrDefault(h => !h.collider.isTrigger && !own.Contains(h.collider));
            if (!hit.collider)
            {
                Debug.LogWarning($"[CapDemo] '{go.name}' 아래에서 바닥을 찾지 못했습니다.", go);
                continue;
            }
            Undo.RecordObject(go.transform, "바닥에 붙이기");
            go.transform.position += Vector3.down * (bounds.min.y - hit.point.y);
        }
    }

    [MenuItem("Tools/CapDemo/맵/선택한 오브젝트 바닥에 붙이기", true)]
    private static bool CanDropSelection() => Selection.gameObjects.Length > 0;

    private static bool PrepareParts()
    {
        if (!AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath))
        {
            Debug.LogError("[CapDemo] 플레이어 애니메이션이 없습니다. 먼저 Tools > CapDemo > 데모 > 플레이어 애니메이션 적용을 실행하세요.");
            return false;
        }
        foreach (var folder in new[] { MapImageImporter.BackgroundFolder, MapImageImporter.ObjectFolder,
                     MapImageImporter.NpcFolder, PrefabFolder, DialogueFolder })
            Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        EnsureSortingLayers();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StoryMenu.FontPath);
        if (!Load(PlayerPrefab)) CreatePlayerPrefab();
        if (!Load(DialoguePrefab)) CreateDialoguePrefab(font);
        if (!Load(ObjectPrefab)) CreateObjectPrefab();
        if (!Load(NpcPrefab)) CreateNpcPrefab(font);
        if (!Load(PortalPrefab)) CreatePortalPrefab();
        if (!Load(SpawnPrefab)) CreateSpawnPrefab();
        return true;
    }

    private static void CreatePlayerPrefab()
    {
        var player = new GameObject("Player");
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 3f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sharedMaterial = DemoMenu.LoadOrCreateNoFriction();
        player.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, PlayerHalfHeight * 2f);
        player.AddComponent<PlayerController2D>();

        var visual = new GameObject("Sprite", typeof(SpriteRenderer), typeof(Animator));
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, -PlayerHalfHeight, 0f);
        var renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAllAssetsAtPath(PlayerIdlePath).OfType<Sprite>()
            .OrderBy(s => s.name).FirstOrDefault(s => s.name.StartsWith("Idle_"));
        renderer.sortingLayerName = NpcLayout.SortingLayer;
        renderer.sortingOrder = 10;
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);

        var playerAnimator = player.AddComponent<PlayerAnimator2D>();
        Assign(playerAnimator, "animator", animator);
        Assign(playerAnimator, "spriteRenderer", renderer);
        SavePrefab(player, PlayerPrefab);
    }

    private static void CreateDialoguePrefab(TMP_FontAsset font)
    {
        var canvasGo = new GameObject("DialogueCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(DemoDialogue));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var box = StoryMenu.CreateDialogueBox(canvasGo.transform, font, null);
        Assign(canvasGo.GetComponent<DemoDialogue>(), "box", box);
        SavePrefab(canvasGo, DialoguePrefab);
    }

    private static void CreateObjectPrefab()
    {
        var go = new GameObject("MapObject", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(MapObject));
        SavePrefab(go, ObjectPrefab);
    }

    private static void CreateNpcPrefab(TMP_FontAsset font)
    {
        var npc = new GameObject("NPC", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(NpcInteract),
            typeof(NpcLayout));
        npc.GetComponent<BoxCollider2D>().isTrigger = true;
        var prompt = DemoMenu.CreatePrompt(npc.transform, font);
        prompt.GetComponent<MeshRenderer>().sortingLayerName = MapObject.FrontLayer;
        Assign(npc.GetComponent<NpcInteract>(), "prompt", prompt);
        Assign(npc.GetComponent<NpcLayout>(), "prompt", prompt.transform);
        SavePrefab(npc, NpcPrefab);
    }

    private static void CreatePortalPrefab()
    {
        var portal = new GameObject("Portal", typeof(BoxCollider2D), typeof(MapPortal));
        var box = portal.GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1f, 2f);
        box.offset = new Vector2(0f, 1f);
        SavePrefab(portal, PortalPrefab);
    }

    private static void CreateSpawnPrefab() => SavePrefab(new GameObject("SpawnPoint", typeof(MapSpawnPoint)), SpawnPrefab);

    private static void EnsureSortingLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("m_SortingLayers");
        foreach (string name in SortingLayers)
        {
            bool exists = false;
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) exists = true;
            if (exists) continue;

            layers.InsertArrayElementAtIndex(layers.arraySize);
            var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            layer.FindPropertyRelative("name").stringValue = name;
            layer.FindPropertyRelative("uniqueID").longValue = (uint)name.GetHashCode() & 0x7fffffff;
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    private static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void Assign(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
