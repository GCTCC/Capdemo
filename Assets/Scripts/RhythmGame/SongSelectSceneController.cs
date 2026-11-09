using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 곡 목록만 담당하는 SongSelectScene의 최상위 컨트롤러다.
// 선택 결과는 RhythmLaunchContext에 저장한 뒤 실제 플레이 씬으로 넘긴다.
[RequireComponent(typeof(ChartAutoLoader))]
[RequireComponent(typeof(SongLobbyMenu))]
[RequireComponent(typeof(RhythmSceneTransition))]
[DisallowMultipleComponent]
public class SongSelectSceneController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "RhythmScene";
    [SerializeField] private string fallbackReturnSceneName = "NewHubDemoScene";

    private ChartAutoLoader chartAutoLoader;
    private SongLobbyMenu songLobbyMenu;
    private RhythmSceneTransition sceneTransition;
    private List<ChartAutoLoader.SongEntry> availableSongs;
    private RhythmEntryMode entryMode = RhythmEntryMode.Single;
    private string returnSceneName = "";
    private bool isLoading;

    public string CurrentModeLabel
    {
        get
        {
            return entryMode == RhythmEntryMode.Story
                ? "STORY MODE"
                : "SINGLE PLAY";
        }
    }

    private void Awake()
    {
        chartAutoLoader = GetComponent<ChartAutoLoader>();
        songLobbyMenu = GetComponent<SongLobbyMenu>();
        sceneTransition = GetComponent<RhythmSceneTransition>();
        songLobbyMenu.Initialize(this);

        RhythmLaunchRequest request;
        if (RhythmLaunchContext.TryConsume(out request))
        {
            entryMode = request.mode == RhythmEntryMode.Direct
                ? RhythmEntryMode.Single
                : request.mode;
            returnSceneName = request.returnSceneName;
        }

        if (string.IsNullOrWhiteSpace(returnSceneName))
        {
            returnSceneName = fallbackReturnSceneName;
        }

        RefreshLobbySongs(false);
        StartCoroutine(sceneTransition.RevealScene());
    }

    public void PlaySelectedSong(ChartAutoLoader.SongEntry song)
    {
        if (song == null || isLoading)
        {
            return;
        }

        StartCoroutine(LoadGameplay(song.DisplayName, false));
    }

    public void PlayRandomWithoutMusic()
    {
        if (!isLoading)
        {
            StartCoroutine(LoadGameplay(string.Empty, true));
        }
    }

    public void RefreshLobbySongs(bool openedFromPause)
    {
        availableSongs = chartAutoLoader.GetAvailableSongs();
        songLobbyMenu.Show(availableSongs, openedFromPause);
    }

    public void ReturnToPreviousScene()
    {
        if (!isLoading)
        {
            StartCoroutine(LoadReturnScene());
        }
    }

    private IEnumerator LoadGameplay(string songName, bool forceRandom)
    {
        isLoading = true;
        yield return sceneTransition.FadeToBlack();

        if (!CanLoad(gameplaySceneName))
        {
            isLoading = false;
            yield return sceneTransition.RevealScene();
            yield break;
        }

        if (forceRandom)
        {
            RhythmLaunchContext.RequestRandom(entryMode, returnSceneName);
        }
        else
        {
            RhythmLaunchContext.Request(entryMode, songName, returnSceneName);
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator LoadReturnScene()
    {
        isLoading = true;
        yield return sceneTransition.FadeToBlack();

        if (!CanLoad(returnSceneName))
        {
            isLoading = false;
            yield return sceneTransition.RevealScene();
            yield break;
        }

        RhythmLaunchContext.Clear();
        SceneManager.LoadScene(returnSceneName);
    }

    private static bool CanLoad(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName)
            && Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return true;
        }

        Debug.LogError("SongSelectSceneController: '" + sceneName
            + "' 씬이 Build Settings에 없습니다.");
        return false;
    }
}
