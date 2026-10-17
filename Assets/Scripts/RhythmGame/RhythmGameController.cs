using UnityEngine;
using UnityEngine.InputSystem;

// 리듬게임의 입력과 각 기능별 컴포넌트 연결만 담당하는 최상위 컨트롤러다.
[RequireComponent(typeof(RhythmClock))]
[RequireComponent(typeof(NoteManager))]
[RequireComponent(typeof(JudgementScoreManager))]
[RequireComponent(typeof(ChartAutoLoader))]
[RequireComponent(typeof(RhythmPlayField))]
[RequireComponent(typeof(RhythmGameUI))]
[RequireComponent(typeof(PauseMenu))]
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
    // 실제 곡과 채보를 연결한다. 비워 두면 음악 자동 탐색 또는 랜덤 채보를 사용한다.
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

    private RhythmClock rhythmClock;
    private NoteManager noteManager;
    private JudgementScoreManager judgementScoreManager;
    private ChartAutoLoader chartAutoLoader;
    private RhythmPlayField playField;
    private RhythmGameUI gameUI;
    private PauseMenu pauseMenu;
    private ChartData activeChartData;

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

        activeChartData = chartData;
        if (activeChartData == null && chartAutoLoader != null)
        {
            chartAutoLoader.TryLoadChart(out activeChartData);
        }

        BuildInputActions();

        // 편집 모드에서 미리 만들어진 필드를 찾아 런타임 참조만 준비한다.
        playField.PrepareForPlay();
        gameUI.Initialize(rhythmClock, judgementScoreManager, this);
        pauseMenu.Initialize(this);

        judgementScoreManager.Initialize(noteManager);
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

        noteManager.NoteMissed += HandleNoteMissed;
        judgementScoreManager.Judged += HandleJudged;

        rhythmClock.StartClock(activeChartData);
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

        HandleLaneActions();
        playField.UpdateLaneGlows(IsLaneHeldForView);
        noteManager.Tick();
    }

    // PauseMenu에서 호출하는 게임 정지 진입점이다.
    public void PauseGame()
    {
        if (rhythmClock != null)
        {
            rhythmClock.PauseClock();
        }
    }

    // PauseMenu의 카운트다운이 끝난 뒤 호출하는 재개 진입점이다.
    public void ResumeGame()
    {
        if (rhythmClock != null)
        {
            rhythmClock.ResumeClock();
        }
    }

    public PlayResult CreatePlayResult()
    {
        return judgementScoreManager.CreatePlayResult();
    }

    // UI와 플레이필드 효과가 현재 레인 입력 상태를 확인할 때 사용한다.
    public bool IsLaneHeldForView(int lane)
    {
        if (rhythmClock == null || rhythmClock.IsPaused)
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
        if (rhythmClock.IsPaused || laneActions == null)
        {
            return;
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            if (laneActions[lane] != null && laneActions[lane].WasPressedThisFrame())
            {
                judgementScoreManager.TryJudge(lane, rhythmClock.SongTime);
            }
        }
    }

    private void HandleNoteMissed(NoteManager.RuntimeNote note)
    {
        judgementScoreManager.RegisterMiss(note);
    }

    private void HandleJudged(JudgementResult result)
    {
        if (gameUI != null)
        {
            gameUI.ShowJudgement(result.judgement.ToString().ToUpperInvariant());
        }
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
