using UnityEngine;

// 튜토리얼 챕터(게임 첫 실행 ~ 첫 곡 ~ 허브 해금) 안에서 지금 어디까지 왔는지.
// 디스크에 저장하지 않는다: 게임을 끄면 사라지고, 다음에 켜면 튜토리얼을 처음부터 한다.
// 씬이 바뀌어도(리듬 씬에 갔다 와도) 게임이 켜져 있는 동안은 유지된다.
public static class TutorialRun
{
    public static TutorialStep Step { get; set; }

    // 이 프로젝트는 Play 때 static 값이 자동으로 지워지지 않게 설정되어 있어서(도메인 리로드 끔),
    // Play를 누를 때마다 여기서 직접 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Step = TutorialStep.NotStarted;
#if UNITY_EDITOR
        // 에디터에서 맵 씬을 바로 Play할 때 쓰는 테스트 단계 (Tools > CapDemo > 튜토리얼 테스트)
        // 타이틀부터 시작하면 TitleScreen이 다시 정하므로 상관없다
        Step = (TutorialStep)UnityEditor.EditorPrefs.GetInt(TestStepPrefKey, (int)TutorialStep.Exploring);
#endif
    }

#if UNITY_EDITOR
    public const string TestStepPrefKey = "CapDemo.TutorialTestStep";
#endif
}

// 숫자는 저장된 씬 설정(StoryBeat)과 연결되므로 바꾸지 않는다. 새 단계는 빈 숫자 사이에 끼워 넣는다
public enum TutorialStep
{
    [InspectorName("시작 전")] NotStarted = 0,
    [InspectorName("프롤로그 (집)")] Prologue = 10,
    [InspectorName("맵 이동 · NPC 대화")] Exploring = 20,
    [InspectorName("첫 곡 연주")] FirstSong = 40,
    [InspectorName("튜토리얼 끝")] Done = 100,
}
