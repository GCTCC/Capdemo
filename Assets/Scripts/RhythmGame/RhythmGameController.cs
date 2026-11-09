using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 실제 리듬게임 플레이, 입력과 기능별 컴포넌트를 연결하는 최상위 컨트롤러다.
[RequireComponent(typeof(RhythmClock))]
[RequireComponent(typeof(NoteManager))]
[RequireComponent(typeof(JudgementScoreManager))]
[RequireComponent(typeof(ChartAutoLoader))]
[RequireComponent(typeof(RhythmPlayField))]
[RequireComponent(typeof(RhythmGameUI))]
[RequireComponent(typeof(PauseMenu))]
[RequireComponent(typeof(RhythmSceneTransition))]
[DisallowMultipleComponent]
public class RhythmGameController : MonoBehaviour
{
    public const int LaneCount = 6;

    [Header("Notes")]
    public float noteSpeed = 15.0f;
    public float minSpawnInterval = 0.52f;
    public float maxSpawnInterval = 0.78f;

    [Range(0.0f, 1.0f)]
    public float doubleNoteChance = 0.32f;

    [Header("Chart")]
    public ChartData chartData;

    [Header("Scene Flow")]
    [SerializeField] private string songSelectSceneName = "SongSelectScene";
    [SerializeField] private string resultSceneName = "RhythmResultScene";

    [Header("Input System")]
    public InputActionAsset inputActions;

    [Header("Materials - Notes")]
    [SerializeField] private Material leftNoteMaterial;
    [SerializeField] private Material rightNoteMaterial;

    private readonly string[] fallbackBindings =
    {
        "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
        "<Keyboard>/j", "<Keyboard>/k", "<Keyboard>/l"
    };

    private InputActionMap gameplayActionMap;
    private InputAction[] laneActions;
    private bool ownsRuntimeActionMap;
    private bool gameplayActive;

    private RhythmClock rhythmClock;
    private NoteManager noteManager;
    private JudgementScoreManager judgementScoreManager;
    private ChartAutoLoader chartAutoLoader;
    private RhythmPlayField playField;
    private RhythmGameUI gameUI;
    private PauseMenu pauseMenu;
    private RhythmSceneTransition sceneTransition;
    private ChartData activeChartData;
    private List<ChartAutoLoader.SongEntry> availableSongs;
    private RhythmEntryMode entryMode = RhythmEntryMode.Direct;
    private string returnSceneName = "";
    private string currentSongTitle = "NO TRACK";
    private bool isEndingSong;

    public bool IsGameplayActive
    {
        get { return gameplayActive; }
    }

    public bool IsTransitioning
    {
        get { return sceneTransition != null && sceneTransition.IsBusy; }
    }

    public string CurrentSongTitle
    {
        get { return currentSongTitle; }
    }

    public string CurrentModeLabel
    {
        get
        {
            switch (entryMode)
            {
                case RhythmEntryMode.Story:
                    return "STORY MODE";
                case RhythmEntryMode.Single:
                    return "SINGLE PLAY";
                default:
                    return "FREE PLAY";
            }
        }
    }

    public bool IsStoryMode
    {
        get { return entryMode == RhythmEntryMode.Story; }
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;

        rhythmClock = GetComponent<RhythmClock>();
        noteManager = GetComponent<NoteManager>();
        judgementScoreManager = GetComponent<JudgementScoreManager>();
        chartAutoLoader = GetComponent<ChartAutoLoader>();
        playField = GetComponent<RhythmPlayField>();
        gameUI = GetComponent<RhythmGameUI>();
        pauseMenu = GetComponent<PauseMenu>();
        sceneTransition = GetComponent<RhythmSceneTransition>();

        BuildInputActions();
        playField.PrepareForPlay();
        gameUI.Initialize(rhythmClock, judgementScoreManager, this);
        pauseMenu.Initialize(this);
        judgementScoreManager.Initialize(noteManager);

        noteManager.NoteMissed += HandleNoteMissed;
        judgementScoreManager.Judged += HandleJudged;

        availableSongs = chartAutoLoader.GetAvailableSongs();

        RhythmLaunchRequest launchRequest;
        if (RhythmLaunchContext.TryConsume(out launchRequest))
        {
            entryMode = launchRequest.mode;
            returnSceneName = launchRequest.returnSceneName;

            if (launchRequest.forceRandomMode)
            {
                StartCoroutine(BeginRandomMode());
                return;
            }

            if (!string.IsNullOrWhiteSpace(launchRequest.songName))
            {
                ChartAutoLoader.SongEntry requestedSong = FindSongByName(
                    launchRequest.songName);
                if (requestedSong != null)
                {
                    StartCoroutine(BeginSelectedSong(requestedSong));
                    return;
                }

                Debug.LogWarning("RhythmGameController: 요청한 곡 '"
                    + launchRequest.songName + "'을 찾지 못했습니다.");
            }
        }

        if (availableSongs.Count > 0)
        {
            if (TryLoadSongSelectScene())
            {
                return;
            }
        }

        // 음악과 채보가 모두 없으면 기존처럼 무음 랜덤 모드를 시작한다.
        StartCoroutine(BeginRandomMode());
    }

    private void OnEnable()
    {
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Enable();
        }
    }

    private void OnDisable()
    {
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Disable();
        }
    }

    private void Update()
    {
        if (rhythmClock == null || noteManager == null || playField == null)
        {
            return;
        }

        if (gameplayActive && !IsTransitioning && !isEndingSong)
        {
            HandleLaneActions();
            noteManager.Tick();

            if (rhythmClock.IsSongFinished)
            {
                StartCoroutine(FinishSong());
            }
        }

        playField.UpdateLaneGlows(IsLaneHeldForView);
    }

    // 일시정지 메뉴의 나가기 버튼이 호출하는 씬 복귀 진입점이다.
    public void ReturnToLobby()
    {
        if (!isEndingSong)
        {
            StartCoroutine(ExitGameplay(entryMode == RhythmEntryMode.Story));
        }
    }

    private IEnumerator BeginSelectedSong(ChartAutoLoader.SongEntry song)
    {
        ChartData selectedChart;
        if (!chartAutoLoader.TryLoadSong(song, out selectedChart))
        {
            yield break;
        }

        gameplayActive = false;
        currentSongTitle = song.DisplayName;
        gameUI.SetVisible(false);

        yield return sceneTransition.PlaySongIntro(
            song.albumCover,
            song.DisplayName,
            CurrentModeLabel,
            delegate { StartGameplay(selectedChart); });
    }

    private IEnumerator BeginRandomMode()
    {
        gameplayActive = false;
        currentSongTitle = "RANDOM PROTOCOL";
        gameUI.SetVisible(false);

        yield return sceneTransition.PlaySongIntro(
            null,
            currentSongTitle,
            CurrentModeLabel,
            delegate { StartGameplay(null); });
    }

    private void StartGameplay(ChartData selectedChart)
    {
        if (rhythmClock.IsPlaying)
        {
            rhythmClock.StopClock();
        }

        noteManager.ClearNotes();
        activeChartData = selectedChart;
        if (activeChartData != null && !string.IsNullOrWhiteSpace(activeChartData.title))
        {
            currentSongTitle = activeChartData.title;
        }
        judgementScoreManager.ResetScore();
        isEndingSong = false;

        noteManager.Initialize(
            rhythmClock,
            activeChartData,
            leftNoteMaterial,
            rightNoteMaterial,
            playField.NearZ,
            playField.NoteSpawnZ,
            playField.JudgeZ,
            playField.NearHalfWidth,
            playField.NoteSpawnHalfWidth,
            noteSpeed,
            minSpawnInterval,
            maxSpawnInterval,
            doubleNoteChance);

        gameplayActive = true;
        gameUI.SetVisible(true);
        rhythmClock.StartClock(activeChartData);
    }

    private IEnumerator FinishSong()
    {
        if (isEndingSong)
        {
            yield break;
        }

        isEndingSong = true;
        gameplayActive = false;
        yield return sceneTransition.FadeToBlack();
        // 점수를 초기화하거나 씬을 내리기 전에 값 형식의 결과 스냅샷을 보관한다.
        if (Application.CanStreamedLevelBeLoaded(resultSceneName))
        {
            RhythmResultContext.Set(currentSongTitle, CreatePlayResult(), entryMode,
                returnSceneName);
            StopCurrentGameplay();
            SceneManager.LoadScene(resultSceneName);
            yield break;
        }

        Debug.LogError("결과 씬이 Build Settings에 없습니다: " + resultSceneName);
        StopCurrentGameplay();

        if (entryMode == RhythmEntryMode.Story && TryLoadReturnScene())
        {
            yield break;
        }

        if (TryLoadSongSelectScene())
        {
            yield break;
        }

        yield return BeginRandomMode();
    }

    private IEnumerator ExitGameplay(bool returnToStory)
    {
        isEndingSong = true;
        gameplayActive = false;
        yield return sceneTransition.FadeToBlack();
        StopCurrentGameplay();

        if (returnToStory && TryLoadReturnScene())
        {
            yield break;
        }

        if (TryLoadSongSelectScene() || TryLoadReturnScene())
        {
            yield break;
        }

        yield return BeginRandomMode();
    }

    private bool TryLoadSongSelectScene()
    {
        if (string.IsNullOrWhiteSpace(songSelectSceneName)
            || !Application.CanStreamedLevelBeLoaded(songSelectSceneName))
        {
            Debug.LogError("RhythmGameController: 곡 선택 씬 '"
                + songSelectSceneName + "'이 Build Settings에 없습니다.");
            return false;
        }

        RhythmEntryMode nextMode = entryMode == RhythmEntryMode.Direct
            ? RhythmEntryMode.Single
            : entryMode;
        RhythmLaunchContext.Request(nextMode, string.Empty, returnSceneName);
        SceneManager.LoadScene(songSelectSceneName);
        return true;
    }

    private bool TryLoadReturnScene()
    {
        if (string.IsNullOrWhiteSpace(returnSceneName))
        {
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
        {
            Debug.LogError("RhythmGameController: 돌아갈 씬 '"
                + returnSceneName + "'이 Build Settings에 없습니다.");
            return false;
        }

        SceneManager.LoadScene(returnSceneName);
        return true;
    }

    private ChartAutoLoader.SongEntry FindSongByName(string songName)
    {
        for (int i = 0; i < availableSongs.Count; i++)
        {
            if (string.Equals(
                availableSongs[i].DisplayName,
                songName,
                StringComparison.OrdinalIgnoreCase))
            {
                return availableSongs[i];
            }
        }

        return null;
    }

    private void StopCurrentGameplay()
    {
        gameplayActive = false;
        rhythmClock.StopClock();
        noteManager.ClearNotes();
        judgementScoreManager.ResetScore();
        gameUI.SetVisible(false);
    }

    public void PauseGame()
    {
        if (gameplayActive)
        {
            rhythmClock.PauseClock();
        }
    }

    public void ResumeGame()
    {
        if (gameplayActive)
        {
            rhythmClock.ResumeClock();
        }
    }

    public PlayResult CreatePlayResult()
    {
        return judgementScoreManager.CreatePlayResult();
    }

    public bool IsLaneHeldForView(int lane)
    {
        if (!gameplayActive
            || IsTransitioning
            || rhythmClock == null
            || rhythmClock.IsPaused)
        {
            return false;
        }

        return laneActions != null
            && lane >= 0
            && lane < laneActions.Length
            && laneActions[lane] != null
            && laneActions[lane].IsPressed();
    }

    private void BuildInputActions()
    {
        laneActions = new InputAction[LaneCount];

        if (inputActions != null)
        {
            gameplayActionMap = inputActions.FindActionMap("Gameplay", true);
            for (int i = 0; i < LaneCount; i++)
            {
                laneActions[i] = gameplayActionMap.FindAction("Lane" + (i + 1), true);
            }
            return;
        }

        ownsRuntimeActionMap = true;
        gameplayActionMap = new InputActionMap("Gameplay");
        for (int i = 0; i < LaneCount; i++)
        {
            laneActions[i] = gameplayActionMap.AddAction(
                "Lane" + (i + 1),
                InputActionType.Button,
                fallbackBindings[i]);
        }

        gameplayActionMap.Enable();
    }

    private void HandleLaneActions()
    {
        if (rhythmClock.IsPaused
            || !rhythmClock.HasStarted
            || laneActions == null)
        {
            return;
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            InputAction laneAction = laneActions[lane];
            if (laneAction == null)
            {
                continue;
            }

            if (laneAction.WasPressedThisFrame())
            {
                judgementScoreManager.TryJudgePress(lane, rhythmClock.ChartTime);
            }

            // 일시정지 중 키를 놓은 경우에도 재개 직후 해제 상태를 감지한다.
            if (laneAction.WasReleasedThisFrame()
                || (judgementScoreManager.HasHoldingNote(lane)
                    && !laneAction.IsPressed()))
            {
                judgementScoreManager.TryReleaseHold(lane, rhythmClock.ChartTime);
            }
        }
    }

    private void HandleNoteMissed(NoteManager.RuntimeNote note)
    {
        judgementScoreManager.RegisterMiss(note);
    }

    private void HandleJudged(JudgementResult result)
    {
        gameUI.ShowJudgement(result.judgement.ToString().ToUpperInvariant());
    }

    private void OnDestroy()
    {
        if (noteManager != null)
        {
            noteManager.NoteMissed -= HandleNoteMissed;
        }

        if (judgementScoreManager != null)
        {
            judgementScoreManager.Judged -= HandleJudged;
        }

        if (rhythmClock != null)
        {
            rhythmClock.StopClock();
        }

        if (ownsRuntimeActionMap && gameplayActionMap != null)
        {
            gameplayActionMap.Dispose();
        }
    }
}
