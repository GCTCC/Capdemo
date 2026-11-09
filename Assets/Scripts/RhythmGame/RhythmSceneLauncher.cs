using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// 허브와 스토리에서 곡 선택 씬 또는 실제 플레이 씬을 여는 공용 진입점이다.
public class RhythmSceneLauncher : MonoBehaviour, IPointerClickHandler
{
    private enum PointerLaunchMode
    {
        Single,
        Story
    }

    [SerializeField] private string rhythmSceneName = "RhythmScene";
    [SerializeField] private string songSelectSceneName = "SongSelectScene";
    [SerializeField] private string songName = "";
    [SerializeField] private string returnSceneName = "";
    [Header("Optional Pointer Click")]
    [SerializeField] private bool launchOnPointerClick;
    [SerializeField] private PointerLaunchMode pointerLaunchMode = PointerLaunchMode.Single;

    // Button 컴포넌트가 없는 이미지 카드도 클릭만으로 진입할 수 있게 한다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!launchOnPointerClick
            || eventData == null
            || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (pointerLaunchMode == PointerLaunchMode.Story)
        {
            LaunchStoryMode();
        }
        else
        {
            LaunchSingleMode();
        }
    }

    // 싱글모드 버튼에 연결한다. 곡이 없으면 곡 선택 전용 씬을 연다.
    public void LaunchSingleMode()
    {
        string returnScene = ResolveReturnScene();
        RhythmLaunchContext.RequestSingle(songName, returnScene);
        LoadRequestedScene(string.IsNullOrWhiteSpace(songName)
            ? songSelectSceneName
            : rhythmSceneName);
    }

    // 스토리의 리듬게임 진입 이벤트에 연결한다. 완료 또는 나가기 시 원래 씬으로 돌아간다.
    public void LaunchStoryMode()
    {
        string returnScene = ResolveReturnScene();
        RhythmLaunchContext.RequestStory(songName, returnScene);
        LoadRequestedScene(string.IsNullOrWhiteSpace(songName)
            ? songSelectSceneName
            : rhythmSceneName);
    }

    public void LaunchSingleSong(string selectedSongName)
    {
        RhythmLaunchContext.RequestSingle(selectedSongName, ResolveReturnScene());
        LoadRequestedScene(rhythmSceneName);
    }

    public void LaunchStorySong(string selectedSongName)
    {
        RhythmLaunchContext.RequestStory(selectedSongName, ResolveReturnScene());
        LoadRequestedScene(rhythmSceneName);
    }

    private string ResolveReturnScene()
    {
        return string.IsNullOrWhiteSpace(returnSceneName)
            ? SceneManager.GetActiveScene().name
            : returnSceneName.Trim();
    }

    private void LoadRequestedScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("RhythmSceneLauncher: '" + sceneName
                + "' 씬이 Build Settings에 없습니다.");
            RhythmLaunchContext.Clear();
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
