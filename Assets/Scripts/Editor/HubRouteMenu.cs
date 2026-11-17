using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 상단 메뉴 Tools > CapDemo > 허브 > 허브 가는 길을 HoloHubScene으로 통일
// 재이 결정(2026-11-17): 앞으로 허브는 HoloHubScene 하나.
// Assets/Scenes 안의 모든 씬을 차례로 열어서, 스크립트 칸에 "HubScene" 또는 "NewHubDemoScene"이라고 적힌 곳을
// 전부 "HoloHubScene"으로 바꾸고 저장한다 (타이틀의 허브 이름, 곡 선택 뒤로가기, 결과 화면 허브 버튼, 스토리 장치 등).
// HoloHubScene을 씬 목록(빌드 프로필)에도 넣고, 옛 허브·데모 씬(HubScene, NewHubDemoScene, DemoScene)은 Assets/Scenes/OldDemo 폴더로 옮겨 씬 목록에서 꺼 둔다 (지우지 않는다).
public static class HubRouteMenu
{
    private const string NewHub = "HoloHubScene";
    private const string NewHubPath = "Assets/Scenes/HoloHubScene.unity";
    private static readonly HashSet<string> OldHubs = new HashSet<string> { "HubScene", "NewHubDemoScene" };

    [MenuItem("Tools/CapDemo/허브/허브 가는 길을 HoloHubScene으로 통일")]
    private static void Unify()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = EditorSceneManager.GetActiveScene().path;

        var report = new List<string>();
        var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.EndsWith("/HubScene.unity") && !p.EndsWith("/NewHubDemoScene.unity") && !p.EndsWith("/HoloHubScene.unity"))
            .OrderBy(p => p).ToList();

        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                EditorUtility.DisplayProgressBar("허브 가는 길 통일", path, (float)i / scenePaths.Count);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int changed = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!mb) continue;
                    var so = new SerializedObject(mb);
                    var prop = so.GetIterator();
                    bool any = false;
                    while (prop.Next(true))
                    {
                        if (prop.propertyType != SerializedPropertyType.String) continue;
                        if (!OldHubs.Contains(prop.stringValue)) continue;
                        report.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)} / {mb.gameObject.name} / {mb.GetType().Name}.{prop.name}: {prop.stringValue} → {NewHub}");
                        prop.stringValue = NewHub;
                        any = true;
                        changed++;
                    }
                    if (any) so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AddToSceneList(NewHubPath);

        // 옛 허브 씬은 Assets/Scenes/OldDemo 폴더로 옮긴다 (Unity 안에서 옮겨서 연결(GUID)이 유지된다)
        var moved = MoveOldHubs();
        report.AddRange(moved);
        if (startScene.EndsWith("/HubScene.unity") || startScene.EndsWith("/NewHubDemoScene.unity")) startScene = NewHubPath;

        if (!string.IsNullOrEmpty(startScene) && System.IO.File.Exists(startScene))
            EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        foreach (var line in report) Debug.Log("[허브 통일] " + line);
        EditorUtility.DisplayDialog("허브 가는 길 통일",
            report.Count == 0
                ? "바꿀 곳이 없었어요. (이미 전부 HoloHubScene이에요)"
                : $"{report.Count}곳을 HoloHubScene으로 바꾸고 저장했어요.\n자세한 목록은 Console 창에 있어요.\nHoloHubScene은 씬 목록에도 들어 있어요.",
            "확인");
    }

    private const string OldFolder = "Assets/Scenes/OldDemo";
    private static readonly string[] OldHubPaths = { "Assets/Scenes/HubScene.unity", "Assets/Scenes/NewHubDemoScene.unity", "Assets/Scenes/DemoScene.unity" };

    // 옛 허브 씬을 OldDemo 폴더로 옮기고, 씬 목록에서는 꺼 둔다 (지우지는 않는다)
    private static List<string> MoveOldHubs()
    {
        var log = new List<string>();
        if (!AssetDatabase.IsValidFolder(OldFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "OldDemo");

        // 옮길 씬이 열려 있으면 먼저 다른 씬으로 바꾼다
        string open = EditorSceneManager.GetActiveScene().path;
        if (OldHubPaths.Contains(open) && System.IO.File.Exists(NewHubPath))
            EditorSceneManager.OpenScene(NewHubPath, OpenSceneMode.Single);

        var movedGuids = new HashSet<string>();
        foreach (var path in OldHubPaths)
        {
            if (!System.IO.File.Exists(path)) continue;
            string target = OldFolder + "/" + System.IO.Path.GetFileName(path);
            string error = AssetDatabase.MoveAsset(path, target);
            if (string.IsNullOrEmpty(error))
            {
                log.Add($"{path} → {target} 로 옮김 (씬 목록에서는 꺼 둠)");
                movedGuids.Add(AssetDatabase.AssetPathToGUID(target));
            }
            else log.Add($"{path} 옮기기 실패: {error}");
        }
        foreach (var path in OldHubPaths)
        {
            string target = OldFolder + "/" + System.IO.Path.GetFileName(path);
            if (System.IO.File.Exists(target)) movedGuids.Add(AssetDatabase.AssetPathToGUID(target));
        }
        if (movedGuids.Count == 0) return log;

        // 씬 목록: 옮긴 씬은 새 경로로 고치고 꺼 둔다
        EditorBuildSettingsScene Fix(EditorBuildSettingsScene s)
        {
            string guid = s.guid.ToString();
            if (!movedGuids.Contains(guid)) return s;
            return new EditorBuildSettingsScene(AssetDatabase.GUIDToAssetPath(guid), false);
        }
        var profile = BuildProfile.GetActiveBuildProfile();
        if (profile && profile.overrideGlobalScenes)
        {
            profile.scenes = profile.scenes.Select(Fix).ToArray();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Select(Fix).ToArray();
        AssetDatabase.SaveAssets();
        return log;
    }

    // 빌드 프로필이 자기 씬 목록을 쓰고 있으면 그 목록에, 아니면 기본 목록에 넣는다
    private static void AddToSceneList(string path)
    {
        if (!System.IO.File.Exists(path)) return;
        var profile = BuildProfile.GetActiveBuildProfile();
        if (profile && profile.overrideGlobalScenes)
        {
            var scenes = profile.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            profile.scenes = scenes.ToArray();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        else
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
