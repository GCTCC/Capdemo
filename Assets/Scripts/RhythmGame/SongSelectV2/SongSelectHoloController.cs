using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 새 곡 선택 화면 (SongSelectScene_v2). 기존 SongSelectSceneController(OnGUI)를 대신한다.
// 화면 모양(배치·그림·색·글꼴)은 씬의 Canvas 오브젝트에서 직접 바꾼다. 이 스크립트는
//  1) Resources/RhythmSongs의 곡으로 목록을 채우고 (BPM, 최고 랭크, 클리어 램프, NEW)
//  2) 고른 곡의 커버를 홀로그램 디스크에 띄우고 미리듣기를 튼다 (30% 지점부터 30초 반복)
//  3) 미리듣기 소리에 맞춰 스펙트럼 링·진행 링·궤도 점·받침 빛·줄 안 파형·재생 배지를 움직이고
//  4) 플레이를 누르면 RhythmScene_v2로 넘어가는 일을 한다.
// 화면은 메뉴 Tools > CapDemo > 리듬게임 > 새 곡 선택 화면 만들기 (v2) 로 만든다.
//
// 조작: 곡 줄 클릭 = 선택(바로 미리듣기), 선택된 줄 한 번 더 클릭 = 플레이
//       ↑↓(W/S) = 곡 이동, ←→ = 난이도(지금은 목업), TAB = 정렬(기본→레벨순→BPM순)
//       Enter/Space = 플레이, Esc = 팝업 닫기 / 이전 화면, F1 배속 · F2 싱크 팝업(목업), F5 = 목록 새로고침
[RequireComponent(typeof(ChartAutoLoader))]
[RequireComponent(typeof(RhythmSceneTransition))]
[DisallowMultipleComponent]
public class SongSelectHoloController : MonoBehaviour
{
    [Header("씬 이동")]
    [SerializeField] private string gameplaySceneName = "RhythmScene_v2";
    [Tooltip("어디서 왔는지 모를 때(이 씬에서 바로 Play) '이전 화면으로'가 갈 씬")]
    [SerializeField] private string fallbackReturnSceneName = "HoloHubScene";

    [Header("목록")]
    [SerializeField] private SongSelectRow rowTemplate;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private ScrollRect listScroll;
    [SerializeField] private TMP_Text trackCountText;
    [Tooltip("{0} = 모드, {1} = 곡 수")]
    [SerializeField] private string trackCountFormat = "{0}  ·  {1:00} TRACKS";
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private string singleModeLabel = "SINGLE PLAY";
    [SerializeField] private string storyModeLabel = "STORY MODE";

    [Header("홀로그램 커버")]
    [SerializeField] private RawImage coverImage;
    [Tooltip("커버를 바꿀 때 살짝 작아졌다 커지는 묶음 (보통 커버의 부모)")]
    [SerializeField] private RectTransform coverStage;
    [Tooltip("커버 그림이 없는 곡에 대신 띄울 그림")]
    [SerializeField] private Texture2D noCoverTexture;
    [SerializeField] private TMP_Text infoText;
    [Tooltip("{0} = BPM, {1} = 길이, {2} = 노트 수")]
    [SerializeField] private string infoFormat = "<color=#8FB2AB>BPM</color> {0}      <color=#8FB2AB>LENGTH</color> {1}      <color=#8FB2AB>NOTES</color> {2}";
    [SerializeField] private HoloConnectorLine connector;
    [SerializeField] private RectTransform orbitSpinner;
    [SerializeField] private float orbitSpinSpeed = -12f;
    [SerializeField, Min(0f)] private float glitchDuration = 0.35f;
    [Tooltip("커버가 바뀌는 시간 (작아짐 → 글리치 → 새 커버)")]
    [SerializeField, Min(0.05f)] private float coverSwapTime = 0.25f;

    [Header("디스크 연출")]
    [Tooltip("커버 둘레 스펙트럼 막대를 담을 곳 (가운데 기준)")]
    [SerializeField] private RectTransform spectrumRoot;
    [SerializeField, Min(8)] private int spectrumBars = 96;
    [SerializeField] private float spectrumRadius = 200f;
    [SerializeField] private Vector2 spectrumLength = new Vector2(8f, 70f);
    [SerializeField] private float spectrumWidth = 3f;
    [SerializeField] private Color spectrumColor = new Color(0.64f, 0.92f, 0.84f, 0.85f);
    [SerializeField, Range(0.5f, 0.99f)] private float spectrumDecay = 0.88f;
    [SerializeField] private float spectrumGain = 9f;
    [Tooltip("흰색 미리듣기 진행 원호 (Image Type = Filled, Radial 360, 위쪽 시작, 시계방향)")]
    [SerializeField] private Image progressRing;
    [SerializeField] private RectTransform progressDot;
    [SerializeField] private float progressRadius = 192f;
    [SerializeField] private RectTransform orbitDotA;
    [SerializeField] private Vector2 orbitRadiusA = new Vector2(345f, 112f);
    [SerializeField] private float orbitTiltA = -18f;
    [SerializeField] private RectTransform orbitDotB;
    [SerializeField] private Vector2 orbitRadiusB = new Vector2(305f, 96f);
    [SerializeField] private float orbitTiltB = 14f;
    [Tooltip("받침 빛·빛기둥 (소리 크기에 맞춰 맥동)")]
    [SerializeField] private Graphic[] pulseGraphics = new Graphic[0];
    [Tooltip("커버 위에 가끔 깜빡이는 가로 슬라이스 (민트, 레드)")]
    [SerializeField] private RectTransform[] glitchSlices = new RectTransform[0];
    [SerializeField] private TMP_Text dataText;
    [SerializeField] private TMP_Text previewTimeText;

    [Header("난이도 탭 (지금은 화면만 바뀌는 목업)")]
    [SerializeField] private Image[] diffTabs = new Image[0];
    [SerializeField] private TMP_Text[] diffNames = new TMP_Text[0];
    [SerializeField] private TMP_Text[] diffLevels = new TMP_Text[0];
    [SerializeField] private string[] diffLabels = { "EASY", "NORMAL", "HARD", "EXPERT" };
    [SerializeField] private Color[] diffColors =
    {
        new Color(0.50f, 0.88f, 0.66f), new Color(0.50f, 0.83f, 1f), new Color(1f, 0.30f, 0.37f), new Color(0.77f, 0.55f, 1f)
    };
    [Tooltip("목업 레벨 숫자")]
    [SerializeField] private int[] diffLevelValues = { 4, 8, 12, 14 };
    [Tooltip("처음 선택된 난이도 (0 EASY ~ 3 EXPERT). 지금 기록은 NORMAL로만 저장된다")]
    [SerializeField] private int startDifficulty = 1;

    [Header("최고 기록")]
    [SerializeField] private TMP_Text bestLabel;
    [SerializeField] private TMP_Text bestScore;
    [SerializeField] private TMP_Text bestRank;
    [SerializeField] private GameObject fullComboTag;

    [Header("편의 버튼 (배속·싱크는 목업: 값만 저장, 인게임에 아직 안 씀)")]
    [SerializeField] private Button speedButton;
    [SerializeField] private Button syncButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private TMP_Text syncLabel;
    [SerializeField] private GameObject optionPopup;
    [SerializeField] private RectTransform optionPopupRect;
    [SerializeField] private TMP_Text optionTitle;
    [SerializeField] private TMP_Text optionValue;
    [SerializeField] private Button optionLeft;
    [SerializeField] private Button optionRight;

    [Header("정렬")]
    [SerializeField] private TMP_Text sortLabel;

    [Header("버튼")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button refreshButton;

    [Header("미리 듣기")]
    [SerializeField] private bool playPreview = true;
    [SerializeField, Range(0f, 1f)] private float previewVolume = 0.55f;
    [Tooltip("곡 길이의 몇 % 지점부터 들려줄지")]
    [SerializeField, Range(0f, 0.9f)] private float previewStartRatio = 0.3f;
    [Tooltip("미리듣기 구간 길이(초). 끝나면 구간 처음부터 반복")]
    [SerializeField, Min(1f)] private float previewLength = 30f;
    [SerializeField, Min(0f)] private float previewFade = 0.4f;

    private const string SpeedKey = "opt_noteSpeed";
    private const string SyncKey = "opt_syncMs";

    private ChartAutoLoader chartAutoLoader;
    private RhythmSceneTransition sceneTransition;
    private AudioSource previewSource;
    private Material coverMaterial;
    private Coroutine glitchRoutine;
    private Coroutine previewRoutine;
    private Coroutine coverRoutine;

    private readonly List<SongSelectRow> rows = new List<SongSelectRow>();
    private List<ChartAutoLoader.SongEntry> songs = new List<ChartAutoLoader.SongEntry>();
    private int selectedIndex = -1;

    private RhythmEntryMode entryMode = RhythmEntryMode.Single;
    private string returnSceneName = "";
    private bool isLoading;

    // 미리듣기 구간
    private float windowStart;
    private float windowLength = 30f;

    // 스펙트럼
    private RectTransform[] specBars;
    private float[] specValues;
    private readonly float[] spectrum = new float[512];
    private float level;   // 전체 소리 크기 0~1 (부드럽게)
    private float orbitAngleA, orbitAngleB;
    private float[] pulseBaseAlpha;
    private float nextSlice;
    private float sliceLeft;

    private int difficulty;
    private int sortMode;               // 0 기본, 1 레벨순, 2 BPM순
    private static readonly string[] SortNames = { "기본", "레벨순", "BPM순" };
    private int popupMode = -1;         // -1 닫힘, 0 배속, 1 싱크
    private float noteSpeed;
    private int syncMs;

    private readonly Dictionary<string, float[]> waveCache = new Dictionary<string, float[]>();
    private readonly Dictionary<string, int> noteCountCache = new Dictionary<string, int>();

    private void Awake()
    {
        chartAutoLoader = GetComponent<ChartAutoLoader>();
        sceneTransition = GetComponent<RhythmSceneTransition>();

        previewSource = gameObject.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
        previewSource.loop = true;
        previewSource.volume = 0f;

        // 커버 머티리얼은 복사본을 써서 글리치 값을 바꿔도 원본 파일이 바뀌지 않게 한다
        if (coverImage && coverImage.material && coverImage.material != coverImage.defaultMaterial)
        {
            coverMaterial = new Material(coverImage.material);
            coverImage.material = coverMaterial;
        }

        if (rowTemplate) rowTemplate.gameObject.SetActive(false);
        if (playButton) playButton.onClick.AddListener(PlaySelected);
        if (backButton) backButton.onClick.AddListener(ReturnToPreviousScene);
        if (refreshButton) refreshButton.onClick.AddListener(RefreshSongs);

        noteSpeed = PlayerPrefs.GetFloat(SpeedKey, 8.5f);
        syncMs = PlayerPrefs.GetInt(SyncKey, 12);
        if (speedButton) speedButton.onClick.AddListener(() => TogglePopup(0));
        if (syncButton) syncButton.onClick.AddListener(() => TogglePopup(1));
        if (settingsButton) settingsButton.onClick.AddListener(() => Debug.Log("[곡 선택] 설정 화면은 아직 준비 중이에요."));
        if (optionLeft) optionLeft.onClick.AddListener(() => AdjustOption(-1));
        if (optionRight) optionRight.onClick.AddListener(() => AdjustOption(1));
        if (optionPopup) optionPopup.SetActive(false);
        RefreshOptionLabels();

        for (int i = 0; i < diffTabs.Length; i++)
        {
            int index = i;
            var button = diffTabs[i] ? diffTabs[i].GetComponent<Button>() : null;
            if (button) button.onClick.AddListener(() => SetDifficulty(index));
        }
        difficulty = Mathf.Clamp(startDifficulty, 0, Mathf.Max(0, diffLabels.Length - 1));

        BuildSpectrum();
        pulseBaseAlpha = new float[pulseGraphics.Length];
        for (int i = 0; i < pulseGraphics.Length; i++) if (pulseGraphics[i]) pulseBaseAlpha[i] = pulseGraphics[i].color.a;
        foreach (var s in glitchSlices) if (s) s.gameObject.SetActive(false);

        RhythmLaunchRequest request;
        if (RhythmLaunchContext.TryConsume(out request))
        {
            entryMode = request.mode == RhythmEntryMode.Direct ? RhythmEntryMode.Single : request.mode;
            returnSceneName = request.returnSceneName;
        }
        if (string.IsNullOrWhiteSpace(returnSceneName)) returnSceneName = fallbackReturnSceneName;
        // 허브는 HoloHubScene 하나로 통일 (옛 허브 이름이 넘어와도 새 허브로 간다)
        if (returnSceneName == "HubScene" || returnSceneName == "NewHubDemoScene") returnSceneName = "HoloHubScene";

        // 씬에 소리 듣는 귀(AudioListener)가 없으면 미리듣기가 안 들리므로 하나 붙인다
        if (!FindFirstObjectByType<AudioListener>()) gameObject.AddComponent<AudioListener>();
        if (modeText) modeText.text = ModeLabel;

        RefreshSongs();
        RefreshDifficulty();
        RefreshSortLabel();
        // 허브에서 지직 전환으로 넘어왔으면 그쪽이 화면을 켜 주므로 검은 페이드는 건너뛴다
        if (HoloTransition.IsBusy) sceneTransition.HideImmediately();
        else StartCoroutine(sceneTransition.RevealScene());
    }

    private string ModeLabel => entryMode == RhythmEntryMode.Story ? storyModeLabel : singleModeLabel;

    private void OnDestroy()
    {
        if (coverMaterial) Destroy(coverMaterial);
    }

    private void Update()
    {
        if (orbitSpinner) orbitSpinner.Rotate(0f, 0f, orbitSpinSpeed * Time.unscaledDeltaTime);
        UpdatePreviewWindow();
        UpdateDisc();
        if (isLoading) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // 배속·싱크 팝업이 열려 있으면 ←→는 값 조절, Esc는 닫기
        if (popupMode >= 0)
        {
            if (kb.leftArrowKey.wasPressedThisFrame) AdjustOption(-1);
            else if (kb.rightArrowKey.wasPressedThisFrame) AdjustOption(1);
            else if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) TogglePopup(-1);
            else if (kb.f1Key.wasPressedThisFrame) TogglePopup(0);
            else if (kb.f2Key.wasPressedThisFrame) TogglePopup(1);
            return;
        }

        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select(selectedIndex + 1);
        else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Select(selectedIndex - 1);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) SetDifficulty(difficulty - 1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) SetDifficulty(difficulty + 1);
        else if (kb.tabKey.wasPressedThisFrame) CycleSort();
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) PlaySelected();
        else if (kb.escapeKey.wasPressedThisFrame) ReturnToPreviousScene();
        else if (kb.f1Key.wasPressedThisFrame) TogglePopup(0);
        else if (kb.f2Key.wasPressedThisFrame) TogglePopup(1);
        else if (kb.f10Key.wasPressedThisFrame) Debug.Log("[곡 선택] 설정 화면은 아직 준비 중이에요.");
        else if (kb.f5Key.wasPressedThisFrame) RefreshSongs();
    }

    private void LateUpdate()
    {
        if (!connector) return;
        bool show = selectedIndex >= 0 && selectedIndex < rows.Count;
        connector.enabled = show;
        if (!show) return;

        Vector3 end = rows[selectedIndex].LinkPointWorld();
        // 선택한 줄이 스크롤로 가려지면 목록 위·아래 끝에 붙여 둔다
        if (listScroll && listScroll.viewport)
        {
            var corners = new Vector3[4];
            listScroll.viewport.GetWorldCorners(corners);
            end.y = Mathf.Clamp(end.y, corners[0].y, corners[1].y);
        }
        connector.SetEnd(end);
    }

    // ── 목록 ──

    public void RefreshSongs()
    {
        string keepName = selectedIndex >= 0 && selectedIndex < songs.Count ? songs[selectedIndex].DisplayName : null;

        songs = chartAutoLoader.GetAvailableSongs();
        SortSongs();
        RebuildRows(keepName);
    }

    private void RebuildRows(string keepName)
    {
        foreach (var row in rows) if (row) Destroy(row.gameObject);
        rows.Clear();
        selectedIndex = -1;

        for (int i = 0; i < songs.Count; i++)
        {
            if (!rowTemplate || !listContent) break;
            var song = songs[i];
            var row = Instantiate(rowTemplate, listContent);
            row.name = "Row " + (i + 1).ToString("00") + " " + song.DisplayName;
            row.gameObject.SetActive(true);
            var record = RhythmBestRecords.Get(song.DisplayName, RhythmBestRecords.DefaultDifficulty);
            var lamp = record.fullCombo ? SongSelectRow.Lamp.FullCombo : record.HasRecord ? SongSelectRow.Lamp.Clear : SongSelectRow.Lamp.None;
            row.Setup(i, song.DisplayName, CoverOf(song), "BPM " + FormatBpm(ReadBpm(song.chartFile)), record.rank, lamp,
                RhythmBestRecords.IsNew(song.DisplayName), OnRowClicked);
            rows.Add(row);
        }

        if (trackCountText) trackCountText.text = string.Format(trackCountFormat, ModeLabel, songs.Count);
        if (playButton) playButton.interactable = songs.Count > 0;

        int keep = 0;
        if (keepName != null)
            for (int i = 0; i < songs.Count; i++)
                if (songs[i].DisplayName == keepName) keep = i;

        if (songs.Count > 0) Select(keep);
        else ShowEmpty();
    }

    private void OnRowClicked(SongSelectRow row)
    {
        if (isLoading) return;
        if (popupMode >= 0) TogglePopup(-1);
        if (row.Index == selectedIndex) PlaySelected();
        else Select(row.Index);
    }

    private void Select(int index)
    {
        if (songs.Count == 0) return;
        index = Mathf.Clamp(index, 0, songs.Count - 1);
        if (index == selectedIndex) return;
        bool first = selectedIndex < 0;
        selectedIndex = index;

        for (int i = 0; i < rows.Count; i++) rows[i].SetSelected(i == index);
        var song = songs[index];

        // 1초 이상 머물면 이 곡의 오디오·채보를 미리 읽어 둔다 (전환 연출 로딩 줄이기)
        if (warmRoutine != null) StopCoroutine(warmRoutine);
        warmRoutine = StartCoroutine(WarmAfterDelay(song, index));

        // 처음 골라 본 곡이면 NEW 해제
        if (RhythmBestRecords.IsNew(song.DisplayName))
        {
            RhythmBestRecords.MarkSeen(song.DisplayName);
            rows[index].SetInfo("BPM " + FormatBpm(ReadBpm(song.chartFile)), false);
        }

        if (coverImage)
        {
            if (coverRoutine != null) StopCoroutine(coverRoutine);
            if (first) SetCover(CoverOf(song));
            else coverRoutine = StartCoroutine(SwapCover(CoverOf(song)));
        }
        if (infoText) infoText.text = string.Format(infoFormat, FormatBpm(ReadBpm(song.chartFile)), FormatTime(song.audioClip), NoteCount(song));
        if (dataText && song.audioClip)
            dataText.text = "SIG ■■■■□\nFREQ " + (song.audioClip.frequency / 1000f).ToString("0.#") + "k\nCH " + song.audioClip.channels +
                            (song.audioClip.channels >= 2 ? " · STEREO" : " · MONO");
        if (connector) connector.Replay();

        rows[index].SetWaveform(Waveform(song.audioClip));
        RefreshBest();

        if (playPreview)
        {
            if (previewRoutine != null) StopCoroutine(previewRoutine);
            previewRoutine = StartCoroutine(Preview(song.audioClip));
        }
        ScrollIntoView(index);
    }

    private void ShowEmpty()
    {
        if (coverImage)
        {
            coverImage.texture = noCoverTexture;
            coverImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
        if (infoText) infoText.text = "Resources/RhythmSongs 폴더에 곡이 없어요";
        if (previewSource) previewSource.Stop();
    }

    private Texture CoverOf(ChartAutoLoader.SongEntry song)
    {
        return song != null && song.albumCover ? song.albumCover : noCoverTexture;
    }

    private void ScrollIntoView(int index)
    {
        if (!listScroll || !listScroll.viewport || index < 0 || index >= rows.Count) return;
        Canvas.ForceUpdateCanvases();
        var content = listScroll.content;
        float viewH = listScroll.viewport.rect.height;
        float contentH = content.rect.height;
        if (contentH <= viewH) return;

        var row = rows[index].Rect;
        // 목록 맨 위에서 이 줄의 위·아래까지 거리
        float top = -(row.localPosition.y + row.rect.yMax);
        float bottom = -(row.localPosition.y + row.rect.yMin);
        float scroll = content.anchoredPosition.y;
        if (top < scroll) scroll = top;
        else if (bottom > scroll + viewH) scroll = bottom - viewH;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(scroll, 0f, contentH - viewH));
    }

    // ── 정렬 (TAB: 기본 → 레벨순 → BPM순) ──

    private void CycleSort()
    {
        sortMode = (sortMode + 1) % SortNames.Length;
        string keep = selectedIndex >= 0 && selectedIndex < songs.Count ? songs[selectedIndex].DisplayName : null;
        SortSongs();
        RebuildRows(keep);
        RefreshSortLabel();
    }

    private void SortSongs()
    {
        if (sortMode == 2) songs.Sort((a, b) => ReadBpm(a.chartFile).CompareTo(ReadBpm(b.chartFile)));
        else if (sortMode == 1) songs.Sort((a, b) => NoteCount(a).CompareTo(NoteCount(b))); // 레벨이 목업이라 노트 수로 대신
        else songs.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
    }

    private void RefreshSortLabel()
    {
        if (sortLabel) sortLabel.text = "정렬 · " + SortNames[sortMode];
    }

    // ── 난이도 (목업) ──

    private string CurrentDifficultyName => difficulty >= 0 && difficulty < diffLabels.Length ? diffLabels[difficulty] : "NORMAL";

    private void SetDifficulty(int index)
    {
        if (diffLabels.Length == 0) return;
        index = Mathf.Clamp(index, 0, diffLabels.Length - 1);
        if (index == difficulty) return;
        difficulty = index;
        RefreshDifficulty();
        RefreshBest();
    }

    private void RefreshDifficulty()
    {
        for (int i = 0; i < diffTabs.Length; i++)
        {
            bool on = i == difficulty;
            Color c = i < diffColors.Length ? diffColors[i] : Color.white;
            if (diffTabs[i]) diffTabs[i].color = on ? new Color(c.r, c.g, c.b, 0.92f) : new Color(0.04f, 0.10f, 0.10f, 0.72f);
            if (i < diffNames.Length && diffNames[i]) diffNames[i].color = on ? new Color(0.03f, 0.08f, 0.08f, 1f) : new Color(c.r, c.g, c.b, 0.9f);
            if (i < diffLevels.Length && diffLevels[i])
            {
                diffLevels[i].text = i < diffLevelValues.Length ? diffLevelValues[i].ToString() : "--";
                diffLevels[i].color = on ? new Color(0.03f, 0.08f, 0.08f, 1f) : Color.white;
            }
            if (diffTabs[i]) diffTabs[i].rectTransform.localScale = on ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
        }
    }

    private void RefreshBest()
    {
        string diff = CurrentDifficultyName;
        if (bestLabel) bestLabel.text = "BEST SCORE  ·  " + diff;
        if (selectedIndex < 0 || selectedIndex >= songs.Count) return;
        var record = RhythmBestRecords.Get(songs[selectedIndex].DisplayName, diff);
        if (bestScore) bestScore.text = record.HasRecord ? record.score.ToString("N0") : "--";
        if (bestRank) bestRank.text = record.HasRecord ? record.rank : "-";
        if (fullComboTag) fullComboTag.SetActive(record.fullCombo);
    }

    // ── 배속·싱크 팝업 (목업: 값만 PlayerPrefs에 저장) ──

    private void TogglePopup(int mode)
    {
        popupMode = mode == popupMode ? -1 : mode;
        if (!optionPopup) return;
        optionPopup.SetActive(popupMode >= 0);
        if (popupMode < 0) return;
        var anchor = popupMode == 0 ? speedButton : syncButton;
        if (optionPopupRect && anchor)
        {
            var at = (RectTransform)anchor.transform;
            optionPopupRect.position = new Vector3(at.position.x, at.position.y, optionPopupRect.position.z);
            optionPopupRect.anchoredPosition += new Vector2(0f, -70f);
        }
        RefreshOptionLabels();
    }

    private void AdjustOption(int dir)
    {
        if (popupMode == 0)
        {
            noteSpeed = Mathf.Clamp(Mathf.Round((noteSpeed + dir * 0.5f) * 2f) / 2f, 1f, 10f);
            PlayerPrefs.SetFloat(SpeedKey, noteSpeed);
        }
        else if (popupMode == 1)
        {
            syncMs = Mathf.Clamp(syncMs + dir * 2, -200, 200);
            PlayerPrefs.SetInt(SyncKey, syncMs);
        }
        PlayerPrefs.Save();
        RefreshOptionLabels();
    }

    private void RefreshOptionLabels()
    {
        string speed = noteSpeed.ToString("0.0");
        string sync = (syncMs >= 0 ? "+" : "") + syncMs + "ms";
        if (speedLabel) speedLabel.text = speed;
        if (syncLabel) syncLabel.text = sync;
        if (optionTitle) optionTitle.text = popupMode == 1 ? "싱크 (판정 시점)" : "배속 (노트 속도)";
        if (optionValue) optionValue.text = popupMode == 1 ? sync : speed;
    }

    // ── 연출 ──

    private void SetCover(Texture tex)
    {
        coverImage.texture = tex;
        coverImage.uvRect = SongSelectRow.SquareCrop(tex);
        if (coverMaterial)
        {
            var r = coverImage.uvRect;
            coverMaterial.SetVector("_UVRect", new Vector4(r.x, r.y, r.width, r.height));
        }
    }

    // 커버 교체: 살짝 축소 → 글리치 → 새 커버로 바뀌며 원래 크기
    private IEnumerator SwapCover(Texture tex)
    {
        var stage = coverStage ? coverStage : coverImage.rectTransform;
        bool swapped = false;
        for (float t = 0f; t < coverSwapTime; t += Time.unscaledDeltaTime)
        {
            float k = t / coverSwapTime;
            float s = k < 0.45f ? Mathf.Lerp(1f, 0.92f, k / 0.45f) : Mathf.Lerp(0.92f, 1f, (k - 0.45f) / 0.55f);
            stage.localScale = new Vector3(s, s, 1f);
            if (coverMaterial) coverMaterial.SetFloat("_Glitch", k < 0.5f ? k * 2f : (1f - k) * 2f);
            if (!swapped && k >= 0.45f)
            {
                swapped = true;
                SetCover(tex);
            }
            yield return null;
        }
        if (!swapped) SetCover(tex);
        stage.localScale = Vector3.one;
        if (coverMaterial) coverMaterial.SetFloat("_Glitch", 0f);
    }

    private IEnumerator Preview(AudioClip clip)
    {
        float from = previewSource.volume;
        for (float t = 0f; t < previewFade && previewSource.isPlaying; t += Time.unscaledDeltaTime)
        {
            previewSource.volume = Mathf.Lerp(from, 0f, t / previewFade);
            yield return null;
        }
        previewSource.Stop();
        previewSource.volume = 0f;
        if (clip == null) yield break;

        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        while (clip.loadState == AudioDataLoadState.Loading) yield return null;
        if (clip.loadState != AudioDataLoadState.Loaded) yield break;

        previewSource.clip = clip;
        windowStart = Mathf.Clamp(clip.length * previewStartRatio, 0f, Mathf.Max(0f, clip.length - 1f));
        windowLength = Mathf.Max(1f, Mathf.Min(previewLength, clip.length - windowStart));
        previewSource.time = windowStart;
        previewSource.Play();
        for (float t = 0f; t < previewFade; t += Time.unscaledDeltaTime)
        {
            previewSource.volume = Mathf.Lerp(0f, previewVolume, t / previewFade);
            yield return null;
        }
        previewSource.volume = previewVolume;
    }

    // 미리듣기 구간(30초)이 끝나면 구간 처음으로 되돌린다
    private void UpdatePreviewWindow()
    {
        if (!previewSource || !previewSource.isPlaying || !previewSource.clip) return;
        float t = previewSource.time;
        if (t >= windowStart + windowLength || t < windowStart - 0.5f) previewSource.time = windowStart;
    }

    private float PreviewProgress =>
        previewSource && previewSource.clip && previewSource.isPlaying
            ? Mathf.Clamp01((previewSource.time - windowStart) / Mathf.Max(0.01f, windowLength)) : 0f;

    private void BuildSpectrum()
    {
        if (!spectrumRoot) return;
        specBars = new RectTransform[spectrumBars];
        specValues = new float[spectrumBars];
        for (int i = 0; i < spectrumBars; i++)
        {
            var go = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(spectrumRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            float ang = 90f - i * 360f / spectrumBars; // 12시부터 시계방향
            float rad = ang * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * spectrumRadius;
            rt.localEulerAngles = new Vector3(0f, 0f, ang - 90f);
            rt.sizeDelta = new Vector2(spectrumWidth, spectrumLength.x);
            var img = go.GetComponent<Image>();
            img.color = spectrumColor;
            img.raycastTarget = false;
            specBars[i] = rt;
        }
    }

    private void UpdateDisc()
    {
        float dt = Time.unscaledDeltaTime;
        bool playing = previewSource && previewSource.isPlaying && previewSource.volume > 0.01f;

        // 스펙트럼 링: 좌우 대칭이 되도록 반 바퀴마다 같은 주파수를 쓴다 (낮은음 = 12시·6시 근처)
        float sum = 0f;
        if (playing) previewSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        if (specBars != null)
        {
            int half = spectrumBars / 2;
            for (int i = 0; i < spectrumBars; i++)
            {
                int k = i < half ? i : spectrumBars - 1 - i;
                float target = 0f;
                if (playing)
                {
                    // 앞쪽 낮은 음은 촘촘히, 높은 음은 넓게 (로그 간격)
                    float f0 = Mathf.Pow(k / (float)half, 2f) * 300f;
                    float f1 = Mathf.Pow((k + 1) / (float)half, 2f) * 300f;
                    int a = Mathf.Clamp((int)f0, 1, spectrum.Length - 1);
                    int b = Mathf.Clamp(Mathf.Max(a + 1, (int)f1), a + 1, spectrum.Length);
                    float v = 0f;
                    for (int j = a; j < b; j++) v = Mathf.Max(v, spectrum[j]);
                    target = Mathf.Clamp01(Mathf.Sqrt(v * spectrumGain * (1f + k * 0.08f)));
                }
                else target = 0.05f + 0.04f * Mathf.Sin(Time.unscaledTime * 2f + i * 0.3f);

                specValues[i] = Mathf.Max(target, specValues[i] * Mathf.Pow(spectrumDecay, dt * 60f));
                sum += specValues[i];
                var rt = specBars[i];
                rt.sizeDelta = new Vector2(spectrumWidth, Mathf.Lerp(spectrumLength.x, spectrumLength.y, specValues[i]));
            }
        }
        float avg = specBars != null && spectrumBars > 0 ? sum / spectrumBars : 0f;
        level = Mathf.Lerp(level, playing ? Mathf.Clamp01(avg * 2.2f) : 0f, 1f - Mathf.Exp(-8f * dt));

        // 진행 링 + 끝 점
        float progress = PreviewProgress;
        if (progressRing) progressRing.fillAmount = progress;
        if (progressDot)
        {
            float rad = (90f - progress * 360f) * Mathf.Deg2Rad;
            progressDot.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * progressRadius;
            progressDot.gameObject.SetActive(playing || progress > 0f);
        }

        // 궤도 점: 재생 중일 때 더 빨리 돈다
        float speed = playing ? 1f : 0.25f;
        orbitAngleA += dt * 22f * speed;
        orbitAngleB -= dt * 15f * speed;
        PlaceOnOrbit(orbitDotA, orbitRadiusA, orbitTiltA, orbitAngleA);
        PlaceOnOrbit(orbitDotB, orbitRadiusB, orbitTiltB, orbitAngleB);

        // 받침 빛 맥동
        for (int i = 0; i < pulseGraphics.Length; i++)
        {
            var g = pulseGraphics[i];
            if (!g) continue;
            var c = g.color;
            c.a = pulseBaseAlpha[i] * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f) * 0.4f + level * 0.9f);
            g.color = c;
        }

        // 커버 위 글리치 슬라이스 (1~3초마다 잠깐)
        if (glitchSlices.Length > 0)
        {
            if (sliceLeft > 0f)
            {
                sliceLeft -= dt;
                foreach (var s in glitchSlices)
                {
                    if (!s) continue;
                    s.gameObject.SetActive(Random.value < 0.75f);
                    var parent = (RectTransform)s.parent;
                    s.anchoredPosition = new Vector2(Random.Range(-60f, 60f), Random.Range(-parent.rect.height * 0.35f, parent.rect.height * 0.35f));
                }
                if (sliceLeft <= 0f) foreach (var s in glitchSlices) if (s) s.gameObject.SetActive(false);
            }
            else if (Time.unscaledTime > nextSlice)
            {
                sliceLeft = Random.Range(0.06f, 0.18f);
                nextSlice = Time.unscaledTime + Random.Range(1f, 3f);
            }
        }

        // 시간 표시 (커버 오른쪽 아래 + 선택한 줄 배지)
        float elapsed = progress * windowLength;
        string time = FormatSeconds(elapsed) + " / " + FormatSeconds(windowLength);
        if (previewTimeText) previewTimeText.text = FormatSeconds(elapsed) + "<size=60%><color=#8FB2AB> / " + FormatSeconds(windowLength) + "</color></size>";
        if (selectedIndex >= 0 && selectedIndex < rows.Count) rows[selectedIndex].SetPlayback(progress, time);
        if (dataText && playing)
        {
            int sig = Mathf.Clamp(Mathf.RoundToInt(level * 5f + 1f), 1, 5);
            var lines = dataText.text.Split('\n');
            lines[0] = "SIG " + new string('■', sig) + new string('□', 5 - sig);
            dataText.text = string.Join("\n", lines);
        }
    }

    private static void PlaceOnOrbit(RectTransform dot, Vector2 radius, float tilt, float angleDeg)
    {
        if (!dot) return;
        float a = angleDeg * Mathf.Deg2Rad;
        Vector2 p = new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
        float t = tilt * Mathf.Deg2Rad;
        dot.anchoredPosition = new Vector2(p.x * Mathf.Cos(t) - p.y * Mathf.Sin(t), p.x * Mathf.Sin(t) + p.y * Mathf.Cos(t));
        // 뒤쪽(위쪽 반)을 지날 때는 흐리게
        var g = dot.GetComponent<Graphic>();
        if (g)
        {
            var c = g.color;
            c.a = Mathf.Sin(a) > 0f ? 0.35f : 1f;
            g.color = c;
        }
    }

    // 미리듣기 구간의 파형 (줄 안에 그림). 압축을 풀어 둔 소리가 아니면 대신 부드러운 가짜 파형
    private float[] Waveform(AudioClip clip)
    {
        const int count = 80;
        if (!clip) return null;
        if (waveCache.TryGetValue(clip.name, out var cached)) return cached;

        var values = new float[count];
        bool ok = false;
        try
        {
            if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.LoadAudioData() && clip.loadState == AudioDataLoadState.Loaded)
            {
                float start = clip.length * previewStartRatio;
                float len = Mathf.Min(previewLength, clip.length - start);
                var chunk = new float[1024 * clip.channels];
                float peak = 0.0001f;
                for (int i = 0; i < count; i++)
                {
                    int offset = Mathf.Clamp((int)((start + len * i / count) * clip.frequency), 0, Mathf.Max(0, clip.samples - 1024));
                    clip.GetData(chunk, offset);
                    float v = 0f;
                    for (int j = 0; j < chunk.Length; j++) v = Mathf.Max(v, Mathf.Abs(chunk[j]));
                    values[i] = v;
                    peak = Mathf.Max(peak, v);
                }
                for (int i = 0; i < count; i++) values[i] = Mathf.Pow(values[i] / peak, 1.5f);
                ok = true;
            }
        }
        catch { ok = false; }

        if (!ok)
        {
            float seed = clip.name.GetHashCode() % 1000;
            for (int i = 0; i < count; i++)
                values[i] = Mathf.Clamp01(0.2f + 0.8f * Mathf.PerlinNoise(seed * 0.1f, i * 0.35f) * (0.6f + 0.4f * Mathf.Sin(i * 0.9f)));
        }
        waveCache[clip.name] = values;
        return values;
    }

    // ── 씬 이동 ──

    public void PlaySelected()
    {
        if (isLoading || selectedIndex < 0 || selectedIndex >= songs.Count) return;
        StartCoroutine(LoadGameplay(songs[selectedIndex].DisplayName));
    }

    public void ReturnToPreviousScene()
    {
        if (popupMode >= 0)
        {
            TogglePopup(-1);
            return;
        }
        if (!isLoading) StartCoroutine(LoadReturnScene());
    }

    private IEnumerator LoadGameplay(string songName)
    {
        isLoading = true;

        // 인게임 v2로 갈 때는 전환 연출 A(레코드 회전)가 로딩과 씬 이동을 맡는다
        if (gameplaySceneName == "RhythmScene_v2" && CanLoad(gameplaySceneName))
        {
            RhythmLaunchContext.Request(entryMode, songName, returnSceneName);
            RhythmLaunchExtrasV2.Set(CurrentDifficultyName,
                difficulty >= 0 && difficulty < diffLevelValues.Length ? diffLevelValues[difficulty] : 0);
            if (previewRoutine != null) StopCoroutine(previewRoutine); // 미리듣기는 오버레이가 0.4초에 걸쳐 끈다
            var start = new SongTransitionOverlay.StartInfo
            {
                song = songs[selectedIndex],
                sceneName = gameplaySceneName,
                difficulty = CurrentDifficultyName,
                level = difficulty >= 0 && difficulty < diffLevelValues.Length ? diffLevelValues[difficulty] : 0,
                discRect = coverImage ? coverImage.rectTransform : null,
                playButtonRect = playButton ? (RectTransform)playButton.transform : null,
                preview = previewSource,
            };
            if (SongTransitionOverlay.TryStart(start)) yield break;
        }

        StopPreviewNow();
        yield return sceneTransition.FadeToBlack();
        if (!CanLoad(gameplaySceneName))
        {
            isLoading = false;
            yield return sceneTransition.RevealScene();
            yield break;
        }
        RhythmLaunchContext.Request(entryMode, songName, returnSceneName);
        // 인게임 v2 곡 정보 패널에 보일 난이도 (HARD 12 등)
        RhythmLaunchExtrasV2.Set(CurrentDifficultyName,
            difficulty >= 0 && difficulty < diffLevelValues.Length ? diffLevelValues[difficulty] : 0);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator LoadReturnScene()
    {
        isLoading = true;
        StopPreviewNow();

        // 돌아갈 때도 허브와 같은 지직 전환 (화면이 파밧 꺼짐 → 허브가 켜짐)
        if (CanLoad(returnSceneName))
        {
            RhythmLaunchContext.Clear();
            if (HoloTransition.LoadScene(returnSceneName, showLogo: false)) yield break;
        }

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

    private Coroutine warmRoutine;
    private bool transitionPrewarmed;

    private IEnumerator WarmAfterDelay(ChartAutoLoader.SongEntry song, int index)
    {
        yield return new WaitForSecondsRealtime(1f);
        // 전환 연출 그림·글꼴은 처음 한 번만 여러 프레임에 나눠 미리 만든다
        if (!transitionPrewarmed)
        {
            transitionPrewarmed = true;
            yield return StartCoroutine(SongTransitionOverlay.Prewarm());
        }
        if (index == selectedIndex && !isLoading) SongPreloader.Warm(song);
    }

    private void StopPreviewNow()
    {
        if (previewRoutine != null) StopCoroutine(previewRoutine);
        if (previewSource) previewSource.Stop();
    }

    private static bool CanLoad(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName)) return true;
        Debug.LogError("SongSelectHoloController: '" + sceneName + "' 씬이 Build Settings에 없습니다.");
        return false;
    }

    // ── 곡 정보 ──

    // 채보 JSON에서 BPM만 읽는다 (NoteEditor는 "BPM", 기존 형식은 "bpm" / "initialBpm")
    [System.Serializable]
    private class BpmProbe
    {
        public float BPM;
        public float bpm;
        public float initialBpm;
    }

    // 노트 수 세기 (NoteEditor: notes[] 안에 롱노트 끝점 notes[])
    [System.Serializable] private class NoteProbeChild { public int num; }
    [System.Serializable] private class NoteProbe { public int num; public NoteProbeChild[] notes; }
    [System.Serializable] private class NotesProbe { public NoteProbe[] notes; }

    private static float ReadBpm(TextAsset chart)
    {
        if (!chart) return 0f;
        try
        {
            var probe = JsonUtility.FromJson<BpmProbe>(chart.text);
            if (probe == null) return 0f;
            if (probe.BPM > 0f) return probe.BPM;
            if (probe.initialBpm > 0f) return probe.initialBpm;
            return probe.bpm;
        }
        catch
        {
            return 0f;
        }
    }

    private int NoteCount(ChartAutoLoader.SongEntry song)
    {
        if (song == null || !song.chartFile) return 0;
        if (noteCountCache.TryGetValue(song.DisplayName, out int cached)) return cached;
        int count = 0;
        try
        {
            var probe = JsonUtility.FromJson<NotesProbe>(song.chartFile.text);
            if (probe != null && probe.notes != null)
                foreach (var n in probe.notes) count += 1 + (n.notes != null ? n.notes.Length : 0);
        }
        catch { count = 0; }
        noteCountCache[song.DisplayName] = count;
        return count;
    }

    private static string FormatBpm(float bpm)
    {
        if (bpm <= 0f) return "---";
        return Mathf.Approximately(bpm, Mathf.Round(bpm)) ? Mathf.RoundToInt(bpm).ToString() : bpm.ToString("0.#");
    }

    private static string FormatTime(AudioClip clip)
    {
        if (!clip) return "--:--";
        return FormatSeconds(clip.length);
    }

    private static string FormatSeconds(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
