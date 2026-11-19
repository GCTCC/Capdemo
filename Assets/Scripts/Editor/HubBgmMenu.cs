using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 에디터 상단 메뉴 Tools > CapDemo > 허브 > 배경음 넣기 (HoloHubScene)
// HoloHubScene에 "Hub BGM" 오브젝트(AudioSource + SceneBgm)를 만들고 임시 배경음을 넣어 저장한다.
// 곡을 바꾸려면 Hub BGM 오브젝트의 SceneBgm > Clip 칸에 다른 음악을 넣으면 된다.
public static class HubBgmMenu
{
    private const string ScenePath = "Assets/Scenes/HoloHubScene.unity";
    private const string ClipPath = "Assets/Audio/BGM/Hub_Temp_AgainstAllAnswers.mp3"; // 임시 (2026-11-18 재이)

    [MenuItem("Tools/CapDemo/허브/배경음 넣기 (HoloHubScene)", priority = 60)]
    public static void Apply()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 긴 음악은 메모리에 다 풀지 않고 흘려 읽는다
        var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
        if (importer != null)
        {
            var s = importer.defaultSampleSettings;
            if (s.loadType != AudioClipLoadType.Streaming)
            {
                s.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = s;
                importer.SaveAndReimport();
            }
        }

        var go = GameObject.Find("Hub BGM");
        if (!go) go = new GameObject("Hub BGM", typeof(AudioSource), typeof(SceneBgm));
        var bgm = go.GetComponent<SceneBgm>();
        if (!bgm) bgm = go.AddComponent<SceneBgm>();
        var so = new SerializedObject(bgm);
        so.FindProperty("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        var src = go.GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[HubBgm] HoloHubScene에 배경음을 넣었어요: " + ClipPath);
    }
}
