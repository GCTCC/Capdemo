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
    private RhythmRuntimeSettings runtimeSettings;
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

    public event Action<JudgementResult, double> JudgedSigned;

    public bool IsGameplayActive { get { return gameplayActive; } }
    public bool IsTransitioning { get { return sceneTransition != null && sceneTransition.IsBusy; } }
    public string CurrentSongTitle { get { return currentSongTitle; } }
    public ChartAutoLoader.SongEntry CurrentSong { get { return currentSong; } }
    public bool IsStoryMode { get { return entryMode == RhythmEntryMode.Story; } }
    public float BaseNoteSpeed { get { return baseNoteSpeed; } }
    public float NoteSpeed { get { return runtimeSettings != null ? runtimeSettings.EffectiveNoteSpeed : noteSpeed; } }
    public string DifficultyName { get { return RhythmLaunchExtrasV2.HasValue ? RhythmLaunchExtrasV2.Difficulty : "NORMAL"; } }
    public int DifficultyLevel { get { return RhythmLaunchExtrasV2.HasValue ? RhythmLaunchExtrasV2.Level : 0; } }
    public AudioSource MusicSource { get { return GetComponent<AudioSource>(); } }
    public RhythmClock Clock { get { return rhythmClock; } }
    // 곡 시작 전(전환 연출 카운트인)에 HUD가 보여 줄 BPM
    public float PendingBpm { get { return transitionChart != null ? transitionChart.SafeBpm : 0f; } }
    public JudgementScoreManager Scores { get { return judgementScoreManager; } }

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


        BuildInputActions();
        playField.PrepareForPlay();
        runtimeSettings = GetComponent<RhythmRuntimeSettings>();
        if (runtimeSettings == null) runtimeSettings = gameObject.AddComponent<RhythmRuntimeSettings>();
        runtimeSettings.Initialize(rhythmClock, noteManager, playField, baseNoteSpeed);
        noteSpeed = runtimeSettings.EffectiveNoteSpeed;
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
            StartCoroutine(FinishSong());
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
        runtimeSettings.Apply();
        noteSpeed = runtimeSettings.EffectiveNoteSpeed;
        activeChartData = selectedChart;
        if (selectedChart != null && !string.IsNullOrWhiteSpace(selectedChart.title))
        {
            currentSongTitle = selectedChart.title;
        }
        judgementScoreManager.ResetScore();
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

    private IEnumerator FinishSong()
    {
        if (isEndingSong) yield break;

        isEndingSong = true;
        gameplayActive = false;
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
        return judgementScoreManager.CreatePlayResult();
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
        if (transitionChart != null) Destroy(transitionChart);
    }
}
