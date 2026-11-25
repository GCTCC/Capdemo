using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 인게임 v2(시안 E4)용 최상위 컨트롤러. 원본 RhythmGameController를 복사해 화면 쪽만 V2로 바꿨다.
// 판정·점수·노트 이동·곡 시계·결과 전달은 원본 컴포넌트(NoteManager, JudgementScoreManager, RhythmClock,
// RhythmResultContext …)를 그대로 쓴다. 원본과 다른 점:
//  - 화면: RhythmPlayFieldV2(홀로 하이웨이), NoteSkinV2(노트 슬랩), RhythmGameUIV2(Canvas HUD), PauseMenuV2
//  - 배속·싱크를 GameSettings(곡 선택 화면과 같은 값)에서 읽어 적용
//  - 판정 오차의 +/-(빠름/느림)를 직접 계산해 HUD에 넘김 (원본 판정 이벤트는 부호가 없다)
//  - LIFE 게이지가 0%일 때 실패 처리 옵션 (기본 꺼짐)
[RequireComponent(typeof(RhythmClock))]
[RequireComponent(typeof(NoteManager))]
[RequireComponent(typeof(JudgementScoreManager))]
[RequireComponent(typeof(ChartAutoLoader))]
[RequireComponent(typeof(RhythmPlayFieldV2))]
[RequireComponent(typeof(NoteSkinV2))]
[RequireComponent(typeof(RhythmGameUIV2))]
[RequireComponent(typeof(PauseMenuV2))]
[RequireComponent(typeof(RhythmSceneTransition))]
[DisallowMultipleComponent]
public class RhythmGameControllerV2 : MonoBehaviour
{
    public const int LaneCount = RhythmGameController.LaneCount;

    [Header("Notes")]
    // 배속 8.5(곡 선택 기본값)일 때의 실제 노트 속도. 실제 속도 = baseNoteSpeed x (배속 / 8.5)
    [SerializeField] private float baseNoteSpeed = 15.0f;
    public float minSpawnInterval = 0.52f;
    public float maxSpawnInterval = 0.78f;

    [Range(0.0f, 1.0f)]
    public float doubleNoteChance = 0.32f;

    [Header("Scene Flow")]
    [SerializeField] private string songSelectSceneName = "SongSelectScene_v2";
    [Tooltip("스토리 모드 곡에서 일시정지 → 나가기를 누르면 갈 메인화면")]
    [SerializeField] private string mainSceneName = "HoloHubScene";
    [SerializeField] private string resultSceneName = "RhythmResultScene_v2";

    [Header("Input System")]
    public InputActionAsset inputActions;

    [Header("테스트용")]
    [Tooltip("켜면 노트를 자동으로 PERFECT 처리한다 (스크린샷·연출 확인용). 실제 플레이에서는 꺼 둘 것")]
    [SerializeField] private bool autoPlay = false;

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
    private RhythmPlayFieldV2 playField;
    private NoteSkinV2 noteSkin;
    private RhythmGameUIV2 gameUI;
    private PauseMenuV2 pauseMenu;
    private RhythmSceneTransition sceneTransition;
    private ChartData activeChartData;
    private ChartData syncedChartCopy;
    private ChartData transitionChart;   // 전환 연출이 미리 읽어 준 채보 (이 컨트롤러가 지운다)
    private List<ChartAutoLoader.SongEntry> availableSongs;
    private ChartAutoLoader.SongEntry currentSong;
    private RhythmEntryMode entryMode = RhythmEntryMode.Direct;
    private string returnSceneName = "";
    private string currentSongTitle = "NO TRACK";
    private bool isEndingSong;
    private float noteSpeed = 15f;

    // 판정 오차 부호 계산용: 방금 누른/뗀 노트 id와 (입력 시각 - 노트 시각)
    private int lastInputNoteId = int.MinValue;
    private double lastSignedOffset;
    private readonly bool[] autoHeld = new bool[LaneCount];

    // 콤보 배율 점수 (V2 전용). 원본 JudgementScoreManager 점수는 그대로 두고 여기서 따로 쌓는다.
    // 배율 = 1 + (콤보 / 50) x 0.1, 최대 1.5 (0~49 x1.0, 50~99 x1.1 … 200+ x1.5)
    private const int ComboStep = 50;
    private const float ComboStepBonus = 0.1f;
    private const float MaxComboMultiplier = 1.5f;
    private int comboScore;

    public event Action<JudgementResult, double> JudgedSigned;

    public bool IsGameplayActive { get { return gameplayActive; } }
    public bool IsTransitioning { get { return sceneTransition != null && sceneTransition.IsBusy; } }
    public string CurrentSongTitle { get { return currentSongTitle; } }
    public ChartAutoLoader.SongEntry CurrentSong { get { return currentSong; } }
    public bool IsStoryMode { get { return entryMode == RhythmEntryMode.Story; } }
    public float NoteSpeed { get { return noteSpeed; } }
    public string DifficultyName { get { return RhythmLaunchExtrasV2.HasValue ? RhythmLaunchExtrasV2.Difficulty : "NORMAL"; } }
    public int DifficultyLevel { get { return RhythmLaunchExtrasV2.HasValue ? RhythmLaunchExtrasV2.Level : 0; } }
    public AudioSource MusicSource { get { return GetComponent<AudioSource>(); } }
    public RhythmClock Clock { get { return rhythmClock; } }
    // 곡 시작 전(전환 연출 카운트인)에 HUD가 보여 줄 BPM
    public float PendingBpm { get { return transitionChart != null ? transitionChart.SafeBpm : 0f; } }
    public JudgementScoreManager Scores { get { return judgementScoreManager; } }
    // 화면·결과에 쓰는 V2 점수 (콤보 배율 적용)
    public int ComboScore { get { return comboScore; } }
    public float ComboMultiplier { get { return MultiplierFor(judgementScoreManager != null ? judgementScoreManager.Combo : 0); } }

    private static float MultiplierFor(int combo)
    {
        return Mathf.Min(1f + (combo / ComboStep) * ComboStepBonus, MaxComboMultiplier);
    }

    public string CurrentModeLabel
    {
        get
        {
            switch (entryMode)
            {
                case RhythmEntryMode.Story: return "STORY MODE";
                case RhythmEntryMode.Single: return "SINGLE PLAY";
                default: return "FREE PLAY";
            }
        }
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;

        rhythmClock = GetComponent<RhythmClock>();
        noteManager = GetComponent<NoteManager>();
        judgementScoreManager = GetComponent<JudgementScoreManager>();
        chartAutoLoader = GetComponent<ChartAutoLoader>();
        playField = GetComponent<RhythmPlayFieldV2>();
        noteSkin = GetComponent<NoteSkinV2>();
        gameUI = GetComponent<RhythmGameUIV2>();
        pauseMenu = GetComponent<PauseMenuV2>();
        sceneTransition = GetComponent<RhythmSceneTransition>();

        // 배속: 곡 선택 화면 표시값(1~10, 기본 8.5) → 실제 노트 속도
        noteSpeed = baseNoteSpeed * (GameSettings.NoteSpeed / GameSettings.NoteSpeedDefault);

        BuildInputActions();
        playField.PrepareForPlay();
        playField.SetTiming(rhythmClock, noteSpeed);
        noteSkin.Initialize(noteManager, playField);
        judgementScoreManager.Initialize(noteManager);
        gameUI.Initialize(this, playField, pauseMenu);
        pauseMenu.Initialize(this);

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
                ChartAutoLoader.SongEntry requestedSong = FindSongByName(launchRequest.songName);
                if (requestedSong != null)
                {
                    // 곡 선택의 전환 연출(A)이 이어지고 있으면: 미리 읽은 채보로 준비만 하고 첫 박 신호를 기다린다
                    var overlay = SongTransitionOverlay.Active;
                    if (overlay != null && overlay.SongName == requestedSong.DisplayName)
                    {
                        fromTransition = true;
                        StartCoroutine(BeginFromTransition(requestedSong, overlay));
                        return;
                    }
                    StartCoroutine(BeginSelectedSong(requestedSong));
                    return;
                }

                Debug.LogWarning("RhythmGameControllerV2: 요청한 곡 '" + launchRequest.songName + "'을 찾지 못했습니다.");
            }
        }

        // 이 씬을 바로 Play한 경우(곡 선택을 거치지 않음): 첫 곡을 바로 시작해 화면을 확인할 수 있게 한다
        if (availableSongs.Count > 0)
        {
            StartCoroutine(BeginSelectedSong(availableSongs[0]));
            return;
        }

        StartCoroutine(BeginRandomMode());
    }

    private bool fromTransition;

    private void Start()
    {
        // 전환 연출 중에는 예전 검은 화면(OnGUI)이 오버레이 위에 한 프레임도 보이지 않게 바로 걷는다
        if (fromTransition) sceneTransition.HideImmediately();
    }

    private void OnEnable()
    {
        if (gameplayActionMap != null) gameplayActionMap.Enable();
    }

    private void OnDisable()
    {
        if (gameplayActionMap != null) gameplayActionMap.Disable();
    }

    private void Update()
    {
        if (rhythmClock == null || noteManager == null || playField == null) return;

        if (gameplayActive && !IsTransitioning && !isEndingSong)
        {
            if (autoPlay) HandleAutoPlay();
            else HandleLaneActions();
            noteManager.Tick();

            if (rhythmClock.IsSongFinished)
            {
                StartCoroutine(FinishSong());
            }
        }

        playField.UpdateLaneEffects(IsLaneHeldForView, IsHoldingLongNote);
    }

    // 일시정지를 열 수 있는지: 곡이 진행 중이고, 전환 연출(곡 선택→인게임 카운트인, 곡 끝→결과) 중이 아닐 때
    public bool CanPause
    {
        get
        {
            return gameplayActive && !isEndingSong && !IsTransitioning
                && SongTransitionOverlay.Active == null && ResultTransitionOverlay.Active == null;
        }
    }

    // 일시정지 → 처음부터 다시: 같은 곡·같은 모드로 이 씬을 다시 연다 (곡 선택의 레코드 전환 없이 커버 소개부터)
    public void RestartSong()
    {
        if (!isEndingSong) StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        isEndingSong = true;
        gameplayActive = false;
        yield return sceneTransition.FadeToBlack();
        StopCurrentGameplay();
        string songName = currentSong != null ? currentSong.DisplayName : string.Empty;
        RhythmLaunchContext.Request(entryMode, songName, returnSceneName, currentSong == null);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 일시정지 → 나가기 (스토리 모드 곡): 메인화면(허브)으로 지직 전환. 튜토리얼은 중간 저장이 없어 다음에 처음부터 시작한다
    public void ExitToMainScreen()
    {
        if (!isEndingSong) StartCoroutine(ExitToMainRoutine());
    }

    private IEnumerator ExitToMainRoutine()
    {
        isEndingSong = true;
        gameplayActive = false;
        rhythmClock.StopClock(); // 소리만 먼저 끈다. 화면은 지직 전환이 찍어 간다
        if (Application.CanStreamedLevelBeLoaded(mainSceneName) && HoloTransition.LoadScene(mainSceneName)) yield break;

        yield return sceneTransition.FadeToBlack();
        StopCurrentGameplay();
        if (Application.CanStreamedLevelBeLoaded(mainSceneName)) SceneManager.LoadScene(mainSceneName);
        else if (!TryLoadReturnScene()) TryLoadSongSelectScene();
    }

    public void ReturnToLobby()
    {
        if (!isEndingSong)
        {
            StartCoroutine(ExitGameplay(entryMode == RhythmEntryMode.Story));
        }
    }

    // LIFE 0% 실패 옵션이 켜져 있을 때 LifeGaugeV2가 부른다. 지금은 곡을 끝내고 결과 화면으로 간다
    public void FailSong()
    {
        if (gameplayActive && !isEndingSong)
        {
            StartCoroutine(FinishSong(true));
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
        currentSong = song;
        currentSongTitle = song.DisplayName;
        gameUI.SetVisible(false);

        yield return sceneTransition.PlaySongIntro(
            song.albumCover,
            song.DisplayName,
            CurrentModeLabel,
            delegate { StartGameplay(selectedChart); });
    }

    // 전환 연출로 들어온 경우: 예전 커버 소개(RhythmSceneTransition)는 건너뛰고,
    // HUD를 시작 상태(점수 0, 콤보·판정 숨김, LIFE 100%, 00:00, 노트 없음)로 보여 주다가 첫 박에 곡을 시작한다
    private IEnumerator BeginFromTransition(ChartAutoLoader.SongEntry song, SongTransitionOverlay overlay)
    {
        gameplayActive = false;
        currentSong = song;
        currentSongTitle = song.DisplayName;
        transitionChart = overlay.Preloader.TakeChartCopy();
        if (transitionChart == null)
        {
            ChartData loaded;
            if (chartAutoLoader.TryLoadSong(song, out loaded)) transitionChart = Instantiate(loaded);
        }

        // RhythmSceneTransition은 Awake에서 검은 화면으로 시작하므로 한 프레임 뒤에 걷는다
        yield return null;
        sceneTransition.HideImmediately();
        gameUI.SetVisible(true);

        bool fired = overlay.FirstBeatFired;
        System.Action onBeat = delegate { fired = true; };
        overlay.OnFirstBeat += onBeat;
        while (!fired && overlay != null) yield return null;
        if (overlay != null) overlay.OnFirstBeat -= onBeat;

        StartGameplay(transitionChart);
    }

    private IEnumerator BeginRandomMode()
    {
        gameplayActive = false;
        currentSong = null;
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
        if (rhythmClock.IsPlaying) rhythmClock.StopClock();

        noteManager.ClearNotes();
        activeChartData = ApplySync(selectedChart);
        if (selectedChart != null && !string.IsNullOrWhiteSpace(selectedChart.title))
        {
            currentSongTitle = selectedChart.title;
        }
        judgementScoreManager.ResetScore();
        comboScore = 0;
        isEndingSong = false;
        for (int i = 0; i < autoHeld.Length; i++) autoHeld[i] = false;

        // 원본 노트 재질은 NoteSkinV2가 숨기므로 null을 넘긴다 (노트 모양은 NoteSkinV2가 그린다)
        noteManager.Initialize(
            rhythmClock,
            activeChartData,
            null,
            null,
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

    // 싱크: 곡 선택에서 고른 ms만큼 노트 시간을 민다 (양수 = 노트가 늦게 온다).
    // 원본 채보 데이터는 그대로 두고 복사본에만 적용한다.
    private ChartData ApplySync(ChartData source)
    {
        if (syncedChartCopy != null)
        {
            Destroy(syncedChartCopy);
            syncedChartCopy = null;
        }

        int syncMs = GameSettings.SyncMs;
        if (source == null || syncMs == 0) return source;

        syncedChartCopy = Instantiate(source);
        syncedChartCopy.name = source.name + " (sync " + syncMs + "ms)";
        double shift = syncMs / 1000.0;
        syncedChartCopy.musicOffset = source.musicOffset + shift;
        for (int i = 0; i < syncedChartCopy.notes.Count; i++)
        {
            NoteData n = syncedChartCopy.notes[i];
            n.hitTime += shift;
            n.endTime += shift;
            syncedChartCopy.notes[i] = n;
        }
        return syncedChartCopy;
    }

    // failed = LIFE 0% 실패. 실패는 예전처럼 검은 화면으로 넘어간다
    private IEnumerator FinishSong(bool failed = false)
    {
        if (isEndingSong) yield break;

        isEndingSong = true;
        gameplayActive = false;

        // 곡을 끝까지 마쳤으면: 결과 전환 연출 A (레코드 착지). 결과 씬은 오버레이가 미리 불러 두었다가 켠다
        if (!failed && Application.CanStreamedLevelBeLoaded(resultSceneName))
        {
            PlayResult playResult = CreatePlayResult();
            RhythmResultContext.Set(currentSongTitle, playResult, entryMode, returnSceneName);
            var startInfo = new ResultTransitionOverlay.StartInfo
            {
                song = currentSong,
                result = playResult,
                resultSceneName = resultSceneName,
                playField = playField
            };
            if (ResultTransitionOverlay.TryStart(startInfo))
            {
                // 화면이 검게 가려질 때까지 남은 판정 이펙트는 그대로 두고, 가려지면 정리한다 (씬은 오버레이가 바꾼다)
                var overlay = ResultTransitionOverlay.Active;
                while (overlay != null && !overlay.ScreenCovered) yield return null;
                StopCurrentGameplay();
                yield break;
            }
        }

        yield return sceneTransition.FadeToBlack();
        if (Application.CanStreamedLevelBeLoaded(resultSceneName))
        {
            RhythmResultContext.Set(currentSongTitle, CreatePlayResult(), entryMode, returnSceneName);
            StopCurrentGameplay();
            SceneManager.LoadScene(resultSceneName);
            yield break;
        }

        Debug.LogError("결과 씬이 Build Settings에 없습니다: " + resultSceneName);
        StopCurrentGameplay();

        if (entryMode == RhythmEntryMode.Story && TryLoadReturnScene()) yield break;
        if (TryLoadSongSelectScene()) yield break;

        yield return BeginRandomMode();
    }

    private IEnumerator ExitGameplay(bool returnToStory)
    {
        isEndingSong = true;
        gameplayActive = false;
        yield return sceneTransition.FadeToBlack();
        StopCurrentGameplay();

        if (returnToStory && TryLoadReturnScene()) yield break;
        if (TryLoadSongSelectScene() || TryLoadReturnScene()) yield break;

        yield return BeginRandomMode();
    }

    private bool TryLoadSongSelectScene()
    {
        if (string.IsNullOrWhiteSpace(songSelectSceneName)
            || !Application.CanStreamedLevelBeLoaded(songSelectSceneName))
        {
            Debug.LogError("RhythmGameControllerV2: 곡 선택 씬 '" + songSelectSceneName + "'이 Build Settings에 없습니다.");
            return false;
        }

        RhythmEntryMode nextMode = entryMode == RhythmEntryMode.Direct ? RhythmEntryMode.Single : entryMode;
        RhythmLaunchContext.Request(nextMode, string.Empty, returnSceneName);
        SceneManager.LoadScene(songSelectSceneName);
        return true;
    }

    private bool TryLoadReturnScene()
    {
        if (string.IsNullOrWhiteSpace(returnSceneName)) return false;

        if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
        {
            Debug.LogError("RhythmGameControllerV2: 돌아갈 씬 '" + returnSceneName + "'이 Build Settings에 없습니다.");
            return false;
        }

        SceneManager.LoadScene(returnSceneName);
        return true;
    }

    private ChartAutoLoader.SongEntry FindSongByName(string songName)
    {
        for (int i = 0; i < availableSongs.Count; i++)
        {
            if (string.Equals(availableSongs[i].DisplayName, songName, StringComparison.OrdinalIgnoreCase))
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
        comboScore = 0;
        gameUI.SetVisible(false);
    }

    public void PauseGame()
    {
        if (gameplayActive) rhythmClock.PauseClock();
    }

    public void ResumeGame()
    {
        if (gameplayActive) rhythmClock.ResumeClock();
    }

    public PlayResult CreatePlayResult()
    {
        // 판정 수·콤보는 원본 그대로, 점수만 V2 콤보 배율 점수로 바꿔 넘긴다
        PlayResult result = judgementScoreManager.CreatePlayResult();
        result.score = comboScore;
        return result;
    }

    public bool IsLaneHeldForView(int lane)
    {
        if (!gameplayActive || IsTransitioning || rhythmClock == null || rhythmClock.IsPaused) return false;
        if (autoPlay) return lane >= 0 && lane < autoHeld.Length && autoHeld[lane];

        return laneActions != null
            && lane >= 0
            && lane < laneActions.Length
            && laneActions[lane] != null
            && laneActions[lane].IsPressed();
    }

    public bool IsHoldingLongNote(int lane)
    {
        return gameplayActive && judgementScoreManager != null && judgementScoreManager.HasHoldingNote(lane);
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
            laneActions[i] = gameplayActionMap.AddAction("Lane" + (i + 1), InputActionType.Button, fallbackBindings[i]);
        }
        gameplayActionMap.Enable();
    }

    private void HandleLaneActions()
    {
        if (rhythmClock.IsPaused || !rhythmClock.HasStarted || laneActions == null) return;

        for (int lane = 0; lane < LaneCount; lane++)
        {
            InputAction laneAction = laneActions[lane];
            if (laneAction == null) continue;

            if (laneAction.WasPressedThisFrame())
            {
                Press(lane, rhythmClock.ChartTime);
            }

            // 일시정지 중 키를 놓은 경우에도 재개 직후 해제 상태를 감지한다.
            if (laneAction.WasReleasedThisFrame()
                || (judgementScoreManager.HasHoldingNote(lane) && !laneAction.IsPressed()))
            {
                Release(lane, rhythmClock.ChartTime);
            }
        }
    }

    // 원본 판정을 부르기 직전에 대상 노트를 보고 (입력 - 노트) 시간을 기록한다 → 빠름(-)/느림(+)
    private void Press(int lane, double time)
    {
        NoteManager.RuntimeNote target = noteManager.FindEarliestPendingNote(lane);
        if (target != null)
        {
            lastInputNoteId = target.data.noteId;
            lastSignedOffset = time - target.data.hitTime;
        }
        judgementScoreManager.TryJudgePress(lane, time);
    }

    private void Release(int lane, double time)
    {
        NoteManager.RuntimeNote target = noteManager.FindHoldingNote(lane);
        if (target != null)
        {
            lastInputNoteId = target.data.noteId;
            lastSignedOffset = time - target.data.endTime;
        }
        judgementScoreManager.TryReleaseHold(lane, time);
    }

    // 테스트용 자동 플레이: 노트 시간이 되면 누르고, 롱노트는 끝 시간에 뗀다
    private void HandleAutoPlay()
    {
        if (rhythmClock.IsPaused || !rhythmClock.HasStarted) return;
        double now = rhythmClock.ChartTime;
        for (int lane = 0; lane < LaneCount; lane++)
        {
            NoteManager.RuntimeNote holding = noteManager.FindHoldingNote(lane);
            if (holding != null)
            {
                if (now >= holding.data.endTime)
                {
                    Release(lane, now);
                    autoHeld[lane] = false;
                }
                continue;
            }

            NoteManager.RuntimeNote next = noteManager.FindEarliestPendingNote(lane);
            if (next != null && now >= next.data.hitTime)
            {
                Press(lane, now);
                autoHeld[lane] = true;
                if (next.data.noteType != NoteType.Hold) StartCoroutine(ReleaseAutoKey(lane));
            }
        }
    }

    private IEnumerator ReleaseAutoKey(int lane)
    {
        yield return new WaitForSeconds(0.08f);
        if (noteManager.FindHoldingNote(lane) == null) autoHeld[lane] = false;
    }

    private void HandleNoteMissed(NoteManager.RuntimeNote note)
    {
        judgementScoreManager.RegisterMiss(note);
    }

    private void HandleJudged(JudgementResult result)
    {
        // 콤보 배율 점수: Judged는 원본이 콤보를 올린 뒤, ScoreChanged보다 먼저 온다 → 지금 콤보로 배율 계산
        int basePoints = result.judgement == JudgementType.Perfect ? 1000
            : result.judgement == JudgementType.Great ? 700
            : result.judgement == JudgementType.Good ? 400 : 0;
        if (basePoints > 0) comboScore += Mathf.RoundToInt(basePoints * MultiplierFor(judgementScoreManager.Combo));

        double signed = result.noteId == lastInputNoteId ? lastSignedOffset : result.timeDifference;
        lastInputNoteId = int.MinValue;
        if (JudgedSigned != null) JudgedSigned(result, signed);
    }

    private void OnDestroy()
    {
        if (noteManager != null) noteManager.NoteMissed -= HandleNoteMissed;
        if (judgementScoreManager != null) judgementScoreManager.Judged -= HandleJudged;
        if (rhythmClock != null) rhythmClock.StopClock();
        if (ownsRuntimeActionMap && gameplayActionMap != null) gameplayActionMap.Dispose();
        if (syncedChartCopy != null) Destroy(syncedChartCopy);
        if (transitionChart != null) Destroy(transitionChart);
    }
}
