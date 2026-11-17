using UnityEditor;
using UnityEngine;

// 에디터 상단 메뉴 Tools > CapDemo > 플레이 데이터 / 튜토리얼 테스트
// 저장된 진행 상황(클리어한 챕터 수)을 지우거나 바꾸거나 확인하고,
// 맵 씬을 바로 Play할 때 튜토리얼을 몇 단계로 시작할지 고른다.
public static class CapDemoMenu
{
    [MenuItem("Tools/CapDemo/플레이 데이터/초기화")]
    private static void ResetProgress()
    {
        SaveData.ResetProgress();
        Debug.Log("[CapDemo] 플레이 데이터를 초기화했습니다. 다음 시작은 튜토리얼(TutoScene)입니다.");
    }

    [MenuItem("Tools/CapDemo/플레이 데이터/튜토리얼 완료로 표시")]
    private static void MarkTutorialCleared()
    {
        SaveData.MarkTutorialCleared();
        Debug.Log("[CapDemo] 튜토리얼 챕터 완료(챕터 1개 클리어)로 표시했습니다. 다음 시작은 HoloHubScene입니다.");
    }

    [MenuItem("Tools/CapDemo/플레이 데이터/확인")]
    private static void LogProgress()
    {
        Debug.Log($"[CapDemo] 클리어한 챕터: {SaveData.GetChaptersCleared()}, 이름: '{SaveData.GetPlayerName()}', " +
                  $"맵 바로 Play 시 튜토리얼 단계: {TestStep}");
    }

    // ── 튜토리얼 테스트: 맵 씬을 바로 Play할 때의 시작 단계 (타이틀부터 Play하면 무시된다) ──
    private const string TestMenu = "Tools/CapDemo/튜토리얼 테스트/";

    private static TutorialStep TestStep
    {
        get => (TutorialStep)EditorPrefs.GetInt(TutorialRun.TestStepPrefKey, (int)TutorialStep.Exploring);
        set => EditorPrefs.SetInt(TutorialRun.TestStepPrefKey, (int)value);
    }

    [MenuItem(TestMenu + "프롤로그 (집)", false, 0)]
    private static void TestPrologue() => SetTest(TutorialStep.Prologue);

    [MenuItem(TestMenu + "맵 이동 · NPC 대화 (기본)", false, 1)]
    private static void TestExploring() => SetTest(TutorialStep.Exploring);

    [MenuItem(TestMenu + "첫 곡 끝난 직후 (공연 후 장면)", false, 2)]
    private static void TestAfterSong() => SetTest(TutorialStep.FirstSong);

    [MenuItem(TestMenu + "튜토리얼 끝 (장면 없음)", false, 3)]
    private static void TestDone() => SetTest(TutorialStep.Done);

    [MenuItem(TestMenu + "프롤로그 (집)", true)]
    private static bool CheckPrologue() => Check(TestMenu + "프롤로그 (집)", TutorialStep.Prologue);

    [MenuItem(TestMenu + "맵 이동 · NPC 대화 (기본)", true)]
    private static bool CheckExploring() => Check(TestMenu + "맵 이동 · NPC 대화 (기본)", TutorialStep.Exploring);

    [MenuItem(TestMenu + "첫 곡 끝난 직후 (공연 후 장면)", true)]
    private static bool CheckAfterSong() => Check(TestMenu + "첫 곡 끝난 직후 (공연 후 장면)", TutorialStep.FirstSong);

    [MenuItem(TestMenu + "튜토리얼 끝 (장면 없음)", true)]
    private static bool CheckDone() => Check(TestMenu + "튜토리얼 끝 (장면 없음)", TutorialStep.Done);

    private static void SetTest(TutorialStep step)
    {
        TestStep = step;
        if (Application.isPlaying) TutorialRun.Step = step; // Play 중이면 바로 바꾼다
        Debug.Log($"[CapDemo] 맵 씬을 바로 Play하면 튜토리얼 '{step}' 단계로 시작합니다.");
    }

    private static bool Check(string path, TutorialStep step)
    {
        Menu.SetChecked(path, TestStep == step);
        return true;
    }
}
