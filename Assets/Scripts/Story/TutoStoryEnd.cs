using UnityEngine;
using UnityEngine.SceneManagement;

// TutoScene 전용: 스토리가 끝나면 튜토리얼 완료를 저장하고 HubScene으로 넘어간다.
// StoryManager의 onFinished에 OnStoryFinished를 연결한다.
public class TutoStoryEnd : MonoBehaviour
{
    [SerializeField] private string hubSceneName = "HubScene";

    public void OnStoryFinished()
    {
        SaveData.MarkTutorialCleared();

        if (!Application.CanStreamedLevelBeLoaded(hubSceneName))
        {
            Debug.LogError($"[TutoStoryEnd] '{hubSceneName}' 씬이 Build Settings에 없습니다.");
            return;
        }
        SceneManager.LoadScene(hubSceneName);
    }
}
