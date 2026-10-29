using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 로비, 입력과 리듬게임 기능별 컴포넌트를 연결하는 최상위 컨트롤러다.
[RequireComponent(typeof(RhythmClock))]
[RequireComponent(typeof(NoteManager))]
[RequireComponent(typeof(JudgementScoreManager))]
[RequireComponent(typeof(ChartAutoLoader))]
[RequireComponent(typeof(RhythmPlayField))]
[RequireComponent(typeof(RhythmGameUI))]
[RequireComponent(typeof(PauseMenu))]
[RequireComponent(typeof(SongLobbyMenu))]
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
    private SongLobbyMenu songLobbyMenu;
    private ChartData activeChartData;
    private List<ChartAutoLoader.SongEntry> availableSongs;

    public bool IsGameplayActive
    {
        get { return gameplayActive; }
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
        songLobbyMenu = GetComponent<SongLobbyMenu>();

        BuildInputActions();
        playField.PrepareForPlay();
        gameUI.Initialize(rhythmClock, judgementScoreManager, this);
        pauseMenu.Initialize(this);
        songLobbyMenu.Initialize(this);
        judgementScoreManager.Initialize(noteManager);

        noteManager.NoteMissed += HandleNoteMissed;
        judgementScoreManager.Judged += HandleJudged;

        availableSongs = chartAutoLoader.GetAvailableSongs();
        if (availableSongs.Count > 0)
        {
            // 음악이 하나라도 있으면 재생 전에 반드시 로비에서 선택한다.
            gameplayActive = false;
            gameUI.SetVisible(false);
            songLobbyMenu.Show(availableSongs, false);
        }
        else
        {
            // 음악과 채보가 모두 없으면 기존처럼 무음 랜덤 모드를 바로 시작한다.
            StartGameplay(chartData);
        }
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

        if (gameplayActive)
        {
            HandleLaneActions();
            noteManager.Tick();
        }

        playField.UpdateLaneGlows(IsLaneHeldForView);
    }

    // 로비에서 누른 곡을 ChartData로 만든 뒤 게임을 시작한다.
    public void PlaySelectedSong(ChartAutoLoader.SongEntry song)
    {
        ChartData selectedChart;
        if (!chartAutoLoader.TryLoadSong(song, out selectedChart))
        {
            return;
        }

        StartGameplay(selectedChart);
    }

    public void PlayRandomWithoutMusic()
    {
        StartGameplay(null);
    }

    public void RefreshLobbySongs(bool openedFromPause)
    {
        availableSongs = chartAutoLoader.GetAvailableSongs();
        songLobbyMenu.Show(availableSongs, openedFromPause);
    }

    // 일시정지 메뉴의 나가기 버튼이 호출하는 로비 복귀 진입점이다.
    public void ReturnToLobby()
    {
        StopCurrentGameplay();
        availableSongs = chartAutoLoader.GetAvailableSongs();
        songLobbyMenu.Show(availableSongs, true);
    }

    private void StartGameplay(ChartData selectedChart)
    {
        if (rhythmClock.IsPlaying)
        {
            rhythmClock.StopClock();
        }

        noteManager.ClearNotes();
        activeChartData = selectedChart;
        judgementScoreManager.ResetScore();

        noteManager.Initialize(
            rhythmClock,
            activeChartData,
            leftNoteMaterial,
            rightNoteMaterial,
            playField.NearZ,
            playField.FarZ,
            playField.JudgeZ,
            playField.NearHalfWidth,
            playField.FarHalfWidth,
            noteSpeed,
            minSpawnInterval,
            maxSpawnInterval,
            doubleNoteChance);

        gameplayActive = true;
        songLobbyMenu.Hide();
        gameUI.SetVisible(true);
        rhythmClock.StartClock(activeChartData);
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
        if (!gameplayActive || rhythmClock == null || rhythmClock.IsPaused)
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
