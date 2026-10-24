using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 에디터 상단 메뉴 Tools > CapDemo > 데모 > 플레이어 애니메이션 적용
// Idle/Walk 시트(256px 칸, 4x2 = 8프레임)를 잘라 애니메이션을 만들고 DemoScene의 Player에 붙인다.
// 여러 번 실행해도 결과는 같다. Animator Controller는 이미 있으면 손대지 않는다.
public static class PlayerAnimMenu
{
    private const string ScenePath = "Assets/Scenes/DemoScene.unity";
    private const string SheetFolder = "Assets/image/Character/Player";
    private const string AnimFolder = "Assets/Animations/Player";
    private const string ControllerPath = AnimFolder + "/Player.controller";

    private const int CellSize = 256;
    private const int Columns = 4, Rows = 2;
    // 캐릭터 키(약 222px)가 1.7유닛 정도가 되는 값. 콜라이더 높이 1.6과 비슷하다.
    private const float PixelsPerUnit = 128f;
    // 칸 아래에서 발끝까지 16px. 피벗을 발끝에 둔다.
    private static readonly Vector2 FeetPivot = new Vector2(0.5f, 16f / CellSize);

    // Player 콜라이더 (루트가 몸 중심, 발끝은 루트에서 0.8 아래)
    private static readonly Vector2 ColliderSize = new Vector2(0.6f, 1.6f);
    private const float FeetOffset = -0.8f;

    [MenuItem("Tools/CapDemo/데모/플레이어 애니메이션 적용")]
    private static void ApplyFromMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Apply();
    }

    public static void Apply()
    {
        AssetDatabase.Refresh();
        var idle = SliceSheet("Idle_Sheet", "Idle");
        var walk = SliceSheet("Walk_Sheet", "Walk");
        if (idle == null || walk == null)
        {
            Debug.LogError($"[CapDemo] {SheetFolder} 에서 Idle_Sheet.png, Walk_Sheet.png 를 찾지 못했습니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(AnimFolder)) AssetDatabase.CreateFolder("Assets/Animations", "Player");
        var idleClip = SaveClip("Player_Idle", idle, 8f);
        var walkClip = SaveClip("Player_Walk", walk, 12f);
        var controller = LoadOrCreateController(idleClip, walkClip);

        var scene = EditorSceneManager.GetActiveScene();
        var player = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Player");
        if (!player)
        {
            Debug.LogError("[CapDemo] 씬에서 Player 오브젝트를 찾지 못했습니다.");
            return;
        }

        // 사각형 그림을 늘려 쓰던 스케일을 풀고, 크기는 콜라이더로 정한다
        player.transform.localScale = Vector3.one;
        if (player.TryGetComponent(out SpriteRenderer oldRenderer)) Object.DestroyImmediate(oldRenderer);
        if (player.TryGetComponent(out BoxCollider2D box))
        {
            box.size = ColliderSize;
            box.offset = Vector2.zero;
        }

        var visual = player.transform.Find("Sprite");
        if (!visual)
        {
            visual = new GameObject("Sprite", typeof(SpriteRenderer), typeof(Animator)).transform;
            visual.SetParent(player.transform, false);
        }
        visual.localPosition = new Vector3(0f, FeetOffset, 0f);
        var renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sprite = idle[0];
        renderer.sortingOrder = 10;
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        if (!player.TryGetComponent(out PlayerAnimator2D playerAnimator))
            playerAnimator = player.AddComponent<PlayerAnimator2D>();
        var so = new SerializedObject(playerAnimator);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[CapDemo] Player에 Idle/Walk 애니메이션을 붙였습니다.");
    }

    // 시트를 8칸으로 잘라 이름순(왼쪽 위부터 오른쪽으로, 윗줄 먼저) 스프라이트를 돌려준다
    private static Sprite[] SliceSheet(string fileName, string prefix)
    {
        string path = $"{SheetFolder}/{fileName}.png";
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;

        var existing = LoadSprites(path, prefix);
        if (existing.Length == Columns * Rows && importer.spritePixelsPerUnit == PixelsPerUnit) return existing;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        int height = Rows * CellSize;
        var rects = new SpriteRect[Columns * Rows];
        for (int row = 0; row < Rows; row++)
        for (int col = 0; col < Columns; col++)
        {
            int index = row * Columns + col;
            rects[index] = new SpriteRect
            {
                name = $"{prefix}_{index}",
                spriteID = GUID.Generate(),
                // 텍스처 좌표는 아래에서 위로 올라간다
                rect = new Rect(col * CellSize, height - (row + 1) * CellSize, CellSize, CellSize),
                alignment = SpriteAlignment.Custom,
                pivot = FeetPivot,
            };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        return LoadSprites(path, prefix);
    }

    // 잘리기 전의 통짜 스프라이트(Idle_Sheet)는 이름 뒤가 숫자가 아니라서 걸러진다
    private static Sprite[] LoadSprites(string path, string prefix) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Select(s => (sprite: s, index: FrameIndex(s.name, prefix)))
            .Where(x => x.index >= 0)
            .OrderBy(x => x.index)
            .Select(x => x.sprite)
            .ToArray();

    private static int FrameIndex(string name, string prefix) =>
        name.StartsWith(prefix + "_") && int.TryParse(name.Substring(prefix.Length + 1), out int index) ? index : -1;

    private static AnimationClip SaveClip(string name, Sprite[] frames, float fps)
    {
        string path = $"{AnimFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = fps;

        // 마지막 프레임도 한 칸만큼 보이도록 끝에 키를 하나 더 둔다
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return clip;
    }

    // Speed > 0.1 이면 Walk, 아니면 Idle
    private static RuntimeAnimatorController LoadOrCreateController(AnimationClip idleClip, AnimationClip walkClip)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing) return existing;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle");
        idle.motion = idleClip;
        var walk = machine.AddState("Walk");
        walk.motion = walkClip;
        machine.defaultState = idle;

        var toWalk = idle.AddTransition(walk);
        toWalk.hasExitTime = false;
        toWalk.duration = 0f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = walk.AddTransition(idle);
        toIdle.hasExitTime = false;
        toIdle.duration = 0f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AssetDatabase.SaveAssets();
        return controller;
    }
}
