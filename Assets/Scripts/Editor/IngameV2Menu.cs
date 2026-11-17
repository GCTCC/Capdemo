using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 에디터 상단 메뉴 Tools > CapDemo > 리듬게임 > 새 인게임 화면 만들기 (v2, 시안 E4)
// RhythmScene_v2를 열고, 'Rhythm Game Prototype' 오브젝트의 원본 화면 컴포넌트를 V2로 바꿔 저장한다.
//  - 떼는 것: RhythmGameController, RhythmGameUI(OnGUI), PauseMenu, RhythmPlayField(+ 미리 만들어 둔 Generated Stage)
//  - 붙이는 것: RhythmGameControllerV2, RhythmPlayFieldV2, NoteSkinV2, RhythmGameUIV2, PauseMenuV2,
//               LifeGaugeV2, BandSyncPanel, ComboSpotlightV2
//  - 원본 판정·노트·시계 컴포넌트(NoteManager, JudgementScoreManager, RhythmClock, ChartAutoLoader,
//    RhythmSceneTransition)는 그대로 둔다
// 원본 RhythmScene과 원본 스크립트는 건드리지 않는다.
public static class IngameV2Menu
{
    private const string ScenePath = "Assets/Scenes/RhythmIngame_v2/RhythmScene_v2.unity";
    private const string RootName = "Rhythm Game Prototype";
    private const string FontFolder = "Assets/Art/Fonts/IngameV2";
    private const string BackgroundPath = "Assets/Art/UI/IngameV2/IngameBackground.png";
    private const string NoCoverPath = "Assets/Art/UI/SongSelect/NoCover.png";
    private const string ShaderPath = "Assets/Art/Shaders/IngameV2World.shader";

    [MenuItem("Tools/CapDemo/리듬게임/새 인게임 화면 만들기 (v2, 시안 E4)", priority = 81)]
    public static void Create()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("새 인게임 화면", ScenePath + " 씬이 없어요.", "확인");
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var root = GameObject.Find(RootName);
        if (!root)
        {
            EditorUtility.DisplayDialog("새 인게임 화면", "씬에 '" + RootName + "' 오브젝트가 없어요.", "확인");
            return;
        }

        PrepareImports();

        // 원본 컨트롤러 설정값 옮겨 오기
        InputActionAsset input = null;
        string songSelect = "SongSelectScene_v2", result = "RhythmResultScene_v2";
        var oldController = root.GetComponent<RhythmGameController>();
        if (oldController)
        {
            var so = new SerializedObject(oldController);
            input = so.FindProperty("inputActions").objectReferenceValue as InputActionAsset;
            songSelect = so.FindProperty("songSelectSceneName").stringValue;
            result = so.FindProperty("resultSceneName").stringValue;
            Object.DestroyImmediate(oldController, true);
        }
        RemoveIfPresent<RhythmGameUI>(root);
        RemoveIfPresent<PauseMenu>(root);
        var oldField = root.GetComponent<RhythmPlayField>();
        if (oldField)
        {
            oldField.ClearGeneratedStage();
            Object.DestroyImmediate(oldField, true);
        }
        var stage = root.transform.Find("Generated Stage");
        if (stage) Object.DestroyImmediate(stage.gameObject);

        // V2 붙이기 (RequireComponent로 나머지가 따라 붙는다)
        var controller = root.GetComponent<RhythmGameControllerV2>();
        if (!controller) controller = root.AddComponent<RhythmGameControllerV2>();
        if (!root.GetComponent<LifeGaugeV2>()) root.AddComponent<LifeGaugeV2>();
        if (!root.GetComponent<BandSyncPanel>()) root.AddComponent<BandSyncPanel>();
        if (!root.GetComponent<ComboSpotlightV2>()) root.AddComponent<ComboSpotlightV2>();

        var cso = new SerializedObject(controller);
        if (input) cso.FindProperty("inputActions").objectReferenceValue = input;
        if (!string.IsNullOrEmpty(songSelect)) cso.FindProperty("songSelectSceneName").stringValue = songSelect;
        if (!string.IsNullOrEmpty(result)) cso.FindProperty("resultSceneName").stringValue = result;
        cso.ApplyModifiedPropertiesWithoutUndo();

        var fso = new SerializedObject(root.GetComponent<RhythmPlayFieldV2>());
        fso.FindProperty("worldShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        fso.ApplyModifiedPropertiesWithoutUndo();

        var uso = new SerializedObject(root.GetComponent<RhythmGameUIV2>());
        uso.FindProperty("oxaniumRegular").objectReferenceValue = LoadFont("Oxanium-SemiBold.ttf");
        uso.FindProperty("oxaniumBold").objectReferenceValue = LoadFont("Oxanium-Bold.ttf");
        uso.FindProperty("sairaCondensed").objectReferenceValue = LoadFont("SairaCondensed-ExtraBold.ttf");
        uso.FindProperty("notoSansKr").objectReferenceValue = LoadFont("NotoSansKR-Bold.ttf");
        uso.FindProperty("background").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        uso.FindProperty("noCover").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(NoCoverPath);
        uso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[IngameV2] RhythmScene_v2를 시안 E4 화면으로 바꿨어요. Play를 눌러 확인하세요.");
    }

    private static void RemoveIfPresent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c) Object.DestroyImmediate(c, true);
    }

    private static Font LoadFont(string file)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + file);
        if (!font) Debug.LogWarning("[IngameV2] 글꼴이 없어요: " + FontFolder + "/" + file);
        return font;
    }

    // 배경은 512로 줄여 불러와 늘리면 살짝 흐려진다 (블러 약 2.5px 느낌)
    private static void PrepareImports()
    {
        var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
        if (importer != null)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.maxTextureSize != 512 || importer.mipmapEnabled;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        foreach (var file in new[] { "Oxanium-SemiBold.ttf", "Oxanium-Bold.ttf", "SairaCondensed-ExtraBold.ttf", "NotoSansKR-Bold.ttf" })
        {
            var fi = AssetImporter.GetAtPath(FontFolder + "/" + file) as TrueTypeFontImporter;
            if (fi != null && !fi.includeFontData)
            {
                fi.includeFontData = true; // 실행 중 TMP 글꼴을 만들 때 필요
                fi.SaveAndReimport();
            }
        }
    }
}
