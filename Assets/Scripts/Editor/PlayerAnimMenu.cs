using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 에디터 상단 메뉴 Tools > CapDemo > 데모 > 플레이어 애니메이션 적용
// Idle/Run/Jump 시트를 잘라 애니메이션과 Animator Controller를 만들고 DemoScene의 Player에 붙인다.
// 여러 번 실행해도 결과는 같다. Animator Controller는 실행할 때마다 새로 만든다.
public static class PlayerAnimMenu
{
    private const string ScenePath = "Assets/Scenes/DemoScene.unity";
    private const string SheetFolder = "Assets/image/Character/Player";
    private const string AnimFolder = "Assets/Animations/Player";
    private const string ControllerPath = AnimFolder + "/Player.controller";

    // 시트 한 장을 자르는 방법. 피벗은 칸 안의 비율(0~1, 왼쪽 아래 기준)이다.
    private class Sheet
    {
        public string File, Prefix;
        public int CellWidth, CellHeight, Columns, Rows;
        public float PixelsPerUnit;
        public Vector2[] Pivots;
        public int Count => Columns * Rows;
    }

    // 세 시트 모두 캐릭터 키가 약 1.7유닛이 되도록 PPU를 맞췄다. 콜라이더 높이 1.6과 비슷하다.
    // Idle: 숨쉬기 4프레임을 가로로 붙인 시트. 256px 칸, 캐릭터 키 약 215px. 칸 아래에서 발끝까지 13px.
    private static readonly Sheet IdleSheet = new Sheet
    {
        File = "Idle_Breathing_Sheet", Prefix = "Idle", CellWidth = 256, CellHeight = 256, Columns = 4, Rows = 1,
        PixelsPerUnit = 128f, Pivots = Repeat(new Vector2(0.5f, 13f / 256f), 4),
    };

    // Run: 달리기 GIF(8프레임)를 4x2로 붙인 시트. 256px 칸, 캐릭터 키 약 211px. 칸 아래에서 발끝까지 15px.
    // 1~4번은 몸이 살짝 등을 보이고 5~8번은 앞을 보여서, 반 사이클마다 반대쪽 다리가 나가는 것처럼 보인다.
    private static readonly Sheet RunSheet = new Sheet
    {
        File = "Run_Sheet", Prefix = "Run", CellWidth = 256, CellHeight = 256, Columns = 4, Rows = 2,
        PixelsPerUnit = 128f, Pivots = Repeat(new Vector2(0.5f, 15f / 256f), 8),
    };

    // Jump: 256x320px 칸 9장, 캐릭터 키 약 218px. 그림 속에서 캐릭터가 직접 뛰어오르는데
    // 실제 높이는 물리가 만들기 때문에, 프레임마다 피벗을 따로 잡아 그림 속 이동을 없앤다.
    // 공중 프레임(4~7)은 머리 높이가 4번과 같도록(발끝에서 머리끝까지 221px), 착지 프레임(8~9)은 발끝에 맞춘다.
    // 값은 각 프레임에서 칸 아래부터 잰 픽셀 수다. 1~3번(웅크리기)은 쓰지 않는다.
    private static readonly Sheet JumpSheet = new Sheet
    {
        File = "Jump_Sheet", Prefix = "Jump", CellWidth = 256, CellHeight = 320, Columns = 3, Rows = 3,
        PixelsPerUnit = 128f,
        Pivots = new[] { 13f, 13f, 13f, 23f, 62f, 38f, 12f, 12f, 13f }
            .Select(y => new Vector2(0.5f, y / 320f)).ToArray(),
    };

    // 숨쉬기 4프레임을 1초에 한 번
    private const float IdleFps = 4f;
    // 안내문의 5fps는 이동 속도 5에서 발이 미끄러져 보여서 기존 Walk와 같은 12fps로 둔다
    private const float RunFps = 12f;
    // 착지 8~9번을 합쳐 0.2초
    private const float LandFps = 10f;

    // 공중 프레임을 고르는 위아래 속도 기준. 점프 순간 속도가 약 12라서
    // 빠르게 오를 때 4번, 꼭대기 근처 5번, 막 떨어질 때 6번, 빠르게 떨어질 때 7번이 나온다.
    private const float FastSpeedY = 9f, SlowSpeedY = 3f;

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
        var idle = SliceSheet(IdleSheet);
        var run = SliceSheet(RunSheet);
        var jump = SliceSheet(JumpSheet);
        if (idle == null || run == null || jump == null)
        {
            Debug.LogError($"[CapDemo] {SheetFolder} 에서 Idle_Breathing_Sheet.png, Run_Sheet.png, Jump_Sheet.png 를 찾지 못했습니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(AnimFolder)) AssetDatabase.CreateFolder("Assets/Animations", "Player");
        var clips = new PlayerClips
        {
            Idle = SaveClip("Player_Idle", idle, IdleFps, true),
            Run = SaveClip("Player_Run", run, RunFps, true),
            // 점프 시트 프레임 번호(1부터)는 배열 번호보다 하나 크다
            Rise = SaveClip("Player_JumpRise", new[] { jump[3] }, 1f, true),
            Apex = SaveClip("Player_JumpApex", new[] { jump[4] }, 1f, true),
            Fall = SaveClip("Player_JumpFall", new[] { jump[5] }, 1f, true),
            FallFast = SaveClip("Player_JumpFallFast", new[] { jump[6] }, 1f, true),
            Land = SaveClip("Player_Land", new[] { jump[7], jump[8] }, LandFps, false),
        };
        var controller = CreateController(clips);

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
        Debug.Log("[CapDemo] Player에 Idle/Run/점프 애니메이션을 붙였습니다.");
    }

    private static Vector2[] Repeat(Vector2 value, int count) => Enumerable.Repeat(value, count).ToArray();

    // 시트를 칸으로 잘라 이름순(왼쪽 위부터 오른쪽으로, 윗줄 먼저) 스프라이트를 돌려준다
    private static Sprite[] SliceSheet(Sheet sheet)
    {
        string path = $"{SheetFolder}/{sheet.File}.png";
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;

        var existing = LoadSprites(path, sheet.Prefix);
        if (IsSlicedAs(existing, sheet) && importer.spritePixelsPerUnit == sheet.PixelsPerUnit) return existing;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = sheet.PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        int height = sheet.Rows * sheet.CellHeight;
        var rects = new SpriteRect[sheet.Count];
        for (int row = 0; row < sheet.Rows; row++)
        for (int col = 0; col < sheet.Columns; col++)
        {
            int index = row * sheet.Columns + col;
            rects[index] = new SpriteRect
            {
                name = $"{sheet.Prefix}_{index}",
                spriteID = GUID.Generate(),
                // 텍스처 좌표는 아래에서 위로 올라간다
                rect = new Rect(col * sheet.CellWidth, height - (row + 1) * sheet.CellHeight, sheet.CellWidth, sheet.CellHeight),
                alignment = SpriteAlignment.Custom,
                pivot = sheet.Pivots[index],
            };
        }
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        return LoadSprites(path, sheet.Prefix);
    }

    // 이미 같은 칸 크기·피벗으로 잘려 있으면 다시 자르지 않는다
    private static bool IsSlicedAs(Sprite[] sprites, Sheet sheet)
    {
        if (sprites.Length != sheet.Count) return false;
        for (int i = 0; i < sprites.Length; i++)
        {
            var size = sprites[i].rect.size;
            if (size != new Vector2(sheet.CellWidth, sheet.CellHeight)) return false;
            if (Vector2.Distance(sprites[i].pivot / size, sheet.Pivots[i]) > 0.001f) return false;
        }
        return true;
    }

    // 잘리기 전의 통짜 스프라이트(Idle_Breathing_Sheet)는 이름 뒤가 숫자가 아니라서 걸러진다
    private static Sprite[] LoadSprites(string path, string prefix) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Select(s => (sprite: s, index: FrameIndex(s.name, prefix)))
            .Where(x => x.index >= 0)
            .OrderBy(x => x.index)
            .Select(x => x.sprite)
            .ToArray();

    private static int FrameIndex(string name, string prefix) =>
        name.StartsWith(prefix + "_") && int.TryParse(name.Substring(prefix.Length + 1), out int index) ? index : -1;

    private static AnimationClip SaveClip(string name, Sprite[] frames, float fps, bool loop)
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
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return clip;
    }

    private class PlayerClips
    {
        public AnimationClip Idle, Run, Rise, Apex, Fall, FallFast, Land;
    }

    // 땅 위: Speed > 0.1 이면 Run, 아니면 Idle.
    // 땅에서 떨어지면(점프든 낭떠러지든) 어디서든 Air로 가고, Air는 위아래 속도로 4~7번 중 하나를 보여 준다.
    // 땅에 닿으면 Land(8~9번)를 짧게 보여 주고 Idle로 돌아가는데, 그 사이에 움직이면 바로 Run으로 끊는다.
    private static RuntimeAnimatorController CreateController(PlayerClips clips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath))
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true,
        });

        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle");
        idle.motion = clips.Idle;
        var run = machine.AddState("Run");
        run.motion = clips.Run;
        var land = machine.AddState("Land");
        land.motion = clips.Land;
        machine.defaultState = idle;

        // 스프라이트 클립을 섞으면 가중치가 가장 큰 그림 하나만 보인다. 기준값은 오름차순이어야 한다.
        var air = controller.CreateBlendTreeInController("Air", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "VelocityY";
        tree.useAutomaticThresholds = false;
        tree.AddChild(clips.FallFast, -FastSpeedY);
        tree.AddChild(clips.Fall, -SlowSpeedY);
        tree.AddChild(clips.Apex, SlowSpeedY);
        tree.AddChild(clips.Rise, FastSpeedY);

        AddTransition(idle, run, AnimatorConditionMode.Greater, 0.1f, "Speed");
        AddTransition(run, idle, AnimatorConditionMode.Less, 0.1f, "Speed");

        var toAir = machine.AddAnyStateTransition(air);
        toAir.hasExitTime = false;
        toAir.duration = 0f;
        toAir.canTransitionToSelf = false;
        toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

        AddTransition(air, land, AnimatorConditionMode.If, 0f, "Grounded");
        AddTransition(land, run, AnimatorConditionMode.Greater, 0.1f, "Speed");
        var landToIdle = land.AddTransition(idle);
        landToIdle.hasExitTime = true;
        landToIdle.exitTime = 1f;
        landToIdle.duration = 0f;

        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(mode, threshold, parameter);
    }
}
