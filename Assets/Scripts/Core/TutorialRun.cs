using UnityEngine;

// 튜토리얼 챕터(게임 첫 실행 ~ 첫 곡 ~ 허브 해금) 안에서 지금 어디까지 왔는지.
// 바뀔 때마다 SaveData(save.json)에 저장한다. 게임을 껐다 켜도 마지막 단계가 남는다.
// (타이틀은 아직 튜토리얼을 끝내지 않았으면 처음부터 다시 시작하게 되어 있다 → TitleScreen.StartGame)
public static class TutorialRun
{
    private static TutorialStep step;
    private static bool loaded;

    public static TutorialStep Step
    {
        get
        {
            if (!loaded) LoadStep();
            return step;
        }
        set
        {
            loaded = true;
            if (step == value) return;
            step = value;
            SaveData.SetTutorialStep(value); // 저장하면 진행 관리자가 퀘스트 조건을 다시 검사한다
        }
    }

    // 이 프로젝트는 Play 때 static 값이 자동으로 지워지지 않게 설정되어 있어서(도메인 리로드 끔),
    // Play를 누를 때마다 여기서 직접 다시 읽는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => loaded = false;

    private static void LoadStep()
    {
        loaded = true;
        step = SaveData.GetTutorialStep();
#if UNITY_EDITOR
        // 에디터에서 맵 씬을 바로 Play할 때 쓰는 테스트 단계 (Tools > CapDemo > 튜토리얼 테스트)
        // "저장된 단계"를 고르면 save.json의 단계를 그대로 쓴다. 타이틀부터 시작하면 TitleScreen이 다시 정한다
        int test = UnityEditor.EditorPrefs.GetInt(TestStepPrefKey, (int)TutorialStep.Exploring);
        if (test != UseSavedStep) step = (TutorialStep)test;
#endif
    }

#if UNITY_EDITOR
    public const string TestStepPrefKey = "CapDemo.TutorialTestStep";
    // 테스트 단계 대신 저장된 단계를 쓴다는 표시 (TutorialStep에 없는 숫자)
    public const int UseSavedStep = -1;
#endif
}

// 숫자는 저장된 씬 설정(StoryBeat)과 save.json에 연결되므로 바꾸지 않는다. 새 단계는 빈 숫자 사이에 끼워 넣는다
public enum TutorialStep
{
    [InspectorName("시작 전")] NotStarted = 0,
    [InspectorName("프롤로그 (집)")] Prologue = 10,
    [InspectorName("맵 이동 · NPC 대화")] Exploring = 20,
    [InspectorName("첫 곡 연주")] FirstSong = 40,
    [InspectorName("튜토리얼 끝")] Done = 100,
}
