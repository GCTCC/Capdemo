using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 튜토리얼 흐름
// 프롤로그(Tuto_0)를 TutoScene 대신 Area3_HomeStreet 맵 위에서 재생하도록 한 번에 바꿔 준다.
public static class TutorialFlowMenu
{
    private const string Area3Path = "Assets/Scenes/Map/Area3_HomeStreet.unity";
    private const string Area3Name = "Area3_HomeStreet";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string ProloguePath = "Assets/Story/Tuto_0.asset";
    private const string MapStoryPrefabPath = "Assets/Prefabs/MapStoryCanvas.prefab";
    private const string RhythmV2Path = "Assets/Scenes/RhythmIngame_v2/RhythmScene_v2.unity";

    [MenuItem("Tools/CapDemo/튜토리얼 흐름/프롤로그를 Area3 맵 위에서 재생하기")]
    private static void MovePrologueToArea3()
    {
        var prologue = AssetDatabase.LoadAssetAtPath<StoryChapter>(ProloguePath);
        var storyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapStoryPrefabPath);
        if (!prologue || !storyPrefab)
        {
            EditorUtility.DisplayDialog("튜토리얼 흐름", $"{ProloguePath} 또는 {MapStoryPrefabPath}를 찾지 못했어요.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("튜토리얼 흐름",
                "다음을 바꾸고 저장해요.\n\n" +
                "1. Area3_HomeStreet: 맵용 StoryCanvas와 프롤로그 StoryBeat 추가\n" +
                "2. MainScene: 타이틀이 TutoScene 대신 Area3_HomeStreet로 가게\n" +
                "3. 지금 빌드 프로필 씬 목록에 Area3_HomeStreet, RhythmScene_v2 추가\n\n" +
                "TutoScene 파일은 지우지 않아요.", "진행", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1. Area3에 스토리 화면과 프롤로그 장치를 놓는다
        var area3 = EditorSceneManager.OpenScene(Area3Path, OpenSceneMode.Single);
        if (!Object.FindAnyObjectByType<StoryOverlay>(FindObjectsInactive.Include))
            PrefabUtility.InstantiatePrefab(storyPrefab);
        if (!Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include))
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        bool hasBeat = Object.FindObjectsByType<StoryBeat>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(b => new SerializedObject(b).FindProperty("requiredStep").intValue == (int)TutorialStep.Prologue);
        if (!hasBeat)
        {
            var beat = new GameObject("StoryBeat_프롤로그").AddComponent<StoryBeat>();
            var so = new SerializedObject(beat);
            so.FindProperty("requiredStep").intValue = (int)TutorialStep.Prologue;
            so.FindProperty("trigger").intValue = (int)StoryBeat.Trigger.Auto;
            so.FindProperty("story").objectReferenceValue = prologue;
            so.FindProperty("nextStep").intValue = (int)TutorialStep.Exploring;
            so.FindProperty("after").intValue = (int)StoryBeat.AfterAction.Stay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(area3);
        EditorSceneManager.SaveScene(area3);

        // 2. 타이틀이 Area3로 가게
        var main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var title = Object.FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
        if (title)
        {
            var so = new SerializedObject(title);
            so.FindProperty("tutorialSceneName").stringValue = Area3Name;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
        }
        else Debug.LogWarning("[CapDemo] MainScene에서 TitleScreen을 찾지 못했어요. Tutorial Scene Name을 직접 Area3_HomeStreet로 바꿔 주세요.");

        // 3. 씬 목록
        AddToSceneList(Area3Path);
        AddToSceneList(RhythmV2Path);

        // 테스트 단계도 프롤로그로 맞춰 두면 Area3를 바로 Play했을 때 프롤로그부터 나온다
        EditorPrefs.SetInt(TutorialRun.TestStepPrefKey, (int)TutorialStep.Prologue);

        EditorSceneManager.OpenScene(Area3Path, OpenSceneMode.Single);
        Debug.Log("[CapDemo] 프롤로그를 Area3_HomeStreet 맵 위에서 재생하도록 바꿨어요. " +
                  "Area3를 바로 Play하면 프롤로그부터 나와요 (Tools > CapDemo > 튜토리얼 테스트에서 바꿀 수 있어요).");
    }

    private const string Area2Path = "Assets/Scenes/Map/Area2_LiveHouse.unity";
    private const string LastStoryBeforeShowPath = "Assets/Story/Story1_1.asset"; // 공연 직전 마지막 스토리 (바뀌면 StoryBeat의 Must Have Seen만 바꾸면 된다)
    private const string FirstSongName = "Moonside-Howl-_フリーBGM-FREE-DOWNLOAD_";

    [MenuItem("Tools/CapDemo/튜토리얼 흐름/Area2에 첫 공연·허브 해금 스위치 놓기")]
    private static void PlaceArea2Beats()
    {
        var lastStory = AssetDatabase.LoadAssetAtPath<StoryChapter>(LastStoryBeforeShowPath);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var area2 = EditorSceneManager.OpenScene(Area2Path, OpenSceneMode.Single);

        var beats = Object.FindObjectsByType<StoryBeat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool Has(TutorialStep step) => beats.Any(b => new SerializedObject(b).FindProperty("requiredStep").intValue == (int)step);

        // ① Story1_1을 끝까지 보면 → 스토리 모드로 첫 곡
        if (!Has(TutorialStep.Exploring))
        {
            var so = new SerializedObject(new GameObject("StoryBeat_첫공연").AddComponent<StoryBeat>());
            so.FindProperty("requiredStep").intValue = (int)TutorialStep.Exploring;
            var seen = so.FindProperty("mustHaveSeen");
            seen.arraySize = lastStory ? 1 : 0;
            if (lastStory) seen.GetArrayElementAtIndex(0).objectReferenceValue = lastStory;
            so.FindProperty("trigger").intValue = (int)StoryBeat.Trigger.Auto;
            so.FindProperty("nextStep").intValue = (int)TutorialStep.FirstSong;
            so.FindProperty("after").intValue = (int)StoryBeat.AfterAction.PlaySong;
            so.FindProperty("rhythmSceneName").stringValue = "RhythmScene_v2";
            so.FindProperty("songName").stringValue = FirstSongName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ② 첫 곡을 끝내고 돌아오면 → 튜토리얼 챕터 클리어 저장 → 허브
        if (!Has(TutorialStep.FirstSong))
        {
            var so = new SerializedObject(new GameObject("StoryBeat_허브해금").AddComponent<StoryBeat>());
            so.FindProperty("requiredStep").intValue = (int)TutorialStep.FirstSong;
            so.FindProperty("trigger").intValue = (int)StoryBeat.Trigger.Auto;
            so.FindProperty("nextStep").intValue = (int)TutorialStep.Done;
            so.FindProperty("clearChapters").intValue = 1;
            so.FindProperty("after").intValue = (int)StoryBeat.AfterAction.LoadScene;
            so.FindProperty("targetScene").stringValue = "HoloHubScene";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(area2);
        EditorSceneManager.SaveScene(area2);
        AddToSceneList("Assets/Scenes/HoloHubScene.unity");
        AddToSceneList(RhythmV2Path);
        if (!lastStory) Debug.LogWarning($"[CapDemo] {LastStoryBeforeShowPath}를 찾지 못해 첫 공연 스위치의 Must Have Seen이 비어 있어요. 직접 넣어 주세요.");
        Debug.Log("[CapDemo] Area2에 StoryBeat_첫공연, StoryBeat_허브해금을 놓고 저장했어요.");
    }

    // 빌드 프로필이 자기 씬 목록을 쓰고 있으면 그 목록에, 아니면 기본 목록에 넣는다
    internal static void AddToSceneList(string path)
    {
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
        Debug.Log($"[CapDemo] 씬 목록에 추가: {path}");
    }
}
