using UnityEngine;
using UnityEngine.SceneManagement;

// TutoScene 전용: 프롤로그 스토리가 끝나면 튜토리얼을 "맵 이동" 단계로 넘기고 첫 맵으로 보낸다.
// 튜토리얼 챕터는 첫 곡까지 끝내야 저장되므로 여기서는 저장하지 않는다.
// StoryManager의 onFinished에 OnStoryFinished를 연결한다.
public class TutoStoryEnd : MonoBehaviour
{
    [Tooltip("프롤로그가 끝나고 넘어갈 맵 씬 이름")]
    [SerializeField] private string nextSceneName = "Area3_HomeStreet";
    [Tooltip("그 씬에서 플레이어가 나타날 도착 지점 이름(MapSpawnPoint). 비우면 씬에 놓인 자리에서 시작")]
    [SerializeField] private string nextSpawn = "";

    public void OnStoryFinished()
    {
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[TutoStoryEnd] '{nextSceneName}' 씬이 Build Profiles 씬 목록에 없습니다.");
            return;
        }
        TutorialRun.Step = TutorialStep.Exploring;
        if (!string.IsNullOrEmpty(nextSpawn)) MapSpawnPoint.NextSpawn = nextSpawn;
        SceneManager.LoadScene(nextSceneName);
    }
}
