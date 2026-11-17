using UnityEngine;
using UnityEngine.SceneManagement;

// 본편 스토리의 한 장면을 맵 위에 거는 장치. 맵 씬에 빈 오브젝트를 만들고 붙인다.
// "튜토리얼이 Required Step 단계이고, Must Have Seen의 대화를 다 봤으면" 아래를 순서대로 한다:
//   1) Story를 맵 위에 재생 (씬에 맵용 StoryCanvas가 있어야 한다. 비우면 건너뜀)
//   2) 튜토리얼 단계를 Next Step으로 넘김
//   3) Clear Chapters가 1 이상이면 그만큼 챕터 클리어로 저장
//   4) 끝나면 할 일: 그대로 맵에 남기 / 스토리 모드로 곡 시작 / 다른 씬으로 이동
// 단계가 바뀌므로 같은 장면이 두 번 나오지 않는다. NPC 첫 만남(NpcInteract)과 달리 "본 적 있음"은 저장하지 않는다.
public class StoryBeat : MonoBehaviour
{
    public enum Trigger
    {
        [InspectorName("조건이 맞는 순간 바로")] Auto,
        [InspectorName("플레이어가 이 구역(Collider2D)에 들어오면")] EnterZone,
    }

    public enum AfterAction
    {
        [InspectorName("맵에 그대로 남기")] Stay,
        [InspectorName("스토리 모드로 곡 시작")] PlaySong,
        [InspectorName("다른 씬으로 이동")] LoadScene,
    }

    [Header("언제")]
    [Tooltip("튜토리얼이 이 단계일 때만 동작한다")]
    [SerializeField] private TutorialStep requiredStep = TutorialStep.Exploring;
    [Tooltip("이 대화·스토리를 모두 끝까지 본 뒤에만 동작한다 (NPC의 First Meet Dialogue / First Meet Story 에셋을 넣는다). 비우면 조건 없음")]
    [SerializeField] private ScriptableObject[] mustHaveSeen = new ScriptableObject[0];
    [SerializeField] private Trigger trigger = Trigger.Auto;
    [Tooltip("조건이 맞고 나서 이 시간(초)만큼 기다렸다가 시작한다. 앞 대화 바로 뒤에 이어지는 다른 대사가 먼저 나올 틈을 준다")]
    [SerializeField, Min(0f)] private float delay = 0.3f;

    [Header("무엇을")]
    [Tooltip("맵 위에 재생할 스토리. 비우면 스토리 없이 아래 단계 변경·이동만 한다")]
    [SerializeField] private StoryChapter story;
    [Tooltip("끝나면 튜토리얼을 이 단계로 넘긴다")]
    [SerializeField] private TutorialStep nextStep = TutorialStep.FirstSong;
    [Tooltip("1 이상이면 끝날 때 '챕터 n개 클리어'로 저장한다 (튜토리얼 챕터 끝 = 1)")]
    [SerializeField, Min(0)] private int clearChapters;

    [Header("끝나면")]
    [SerializeField] private AfterAction after = AfterAction.Stay;
    [Tooltip("곡 시작: 플레이 씬 이름 (스토리 모드용 씬)")]
    [SerializeField] private string rhythmSceneName = "RhythmScene_v2";
    [Tooltip("곡 시작: Resources/RhythmSongs 안의 곡 파일 이름 (확장자 없이). 비우면 리듬 씬이 알아서 고른다")]
    [SerializeField] private string songName = "";
    [Tooltip("곡 시작: 곡이 끝나고 이 맵으로 돌아왔을 때 플레이어가 설 도착 지점 이름(MapSpawnPoint). 비우면 씬에 놓인 자리")]
    [SerializeField] private string returnSpawn = "";
    [Tooltip("다른 씬으로 이동: 씬 이름")]
    [SerializeField] private string targetScene = "HoloHubScene";
    [Tooltip("다른 씬으로 이동: 도착 지점 이름(MapSpawnPoint). 맵이 아니면 비운다")]
    [SerializeField] private string targetSpawn = "";

    private StoryOverlay overlay;
    private PlayerController2D player;
    private bool playerInZone;
    private bool running;
    private float waited; // 조건이 계속 맞은 시간

    private void Awake()
    {
        if (trigger == Trigger.EnterZone && TryGetComponent(out Collider2D zone)) zone.isTrigger = true;
    }

    private void Start()
    {
        overlay = FindAnyObjectByType<StoryOverlay>();
        player = FindAnyObjectByType<PlayerController2D>();
        if (story && !overlay)
            Debug.LogWarning($"[StoryBeat] {name}: 스토리를 띄울 맵용 StoryCanvas가 씬에 없습니다.", this);

        // 씬이 열리자마자 틀 장면이면 맵이 잠깐 보였다 어두워지지 않게 처음부터 검게 덮는다
        if (story && overlay && trigger == Trigger.Auto && Ready()) overlay.SnapBlack();
    }

    private void Update()
    {
        if (running) return;
        bool canRun = Ready()
                      && (trigger != Trigger.EnterZone || playerInZone)
                      && !(overlay && overlay.IsPlaying) // NPC 첫 만남 스토리 등이 끝나길 기다린다
                      && !(player && player.IsTalking);  // NPC 대화가 끝나길 기다린다
        if (!canRun)
        {
            waited = 0f;
            return;
        }
        waited += Time.unscaledDeltaTime;
        if (waited >= delay) Run();
    }

    private bool Ready()
    {
        if (TutorialRun.Step != requiredStep) return false;
        foreach (var seen in mustHaveSeen)
            if (seen && !SaveData.HasMetNpc(seen.name)) return false;
        return true;
    }

    private void Run()
    {
        running = true;
        bool leaves = after != AfterAction.Stay;
        if (leaves && !CanLeave())
        {
            leaves = false; // 이동할 씬이 없으면 검은 화면에 갇히지 않게 맵으로 돌아온다
        }

        if (story && overlay) overlay.Play(story, () => Finish(leaves), stayBlack: leaves);
        else Finish(leaves);
    }

    private void Finish(bool leaves)
    {
        TutorialRun.Step = nextStep;
        if (clearChapters > 0) SaveData.MarkChapterCleared(clearChapters);
        running = false;
        if (!leaves) return;

        if (after == AfterAction.PlaySong)
        {
            // 곡이 끝나면 리듬 씬이 이 맵으로 돌려보낸다
            RhythmLaunchContext.RequestStory(songName, SceneManager.GetActiveScene().name);
            MapSpawnPoint.NextSpawn = string.IsNullOrEmpty(returnSpawn) ? null : returnSpawn;
            SceneManager.LoadScene(rhythmSceneName);
        }
        else
        {
            MapSpawnPoint.NextSpawn = string.IsNullOrEmpty(targetSpawn) ? null : targetSpawn;
            SceneManager.LoadScene(targetScene);
        }
    }

    private bool CanLeave()
    {
        string scene = after == AfterAction.PlaySong ? rhythmSceneName : targetScene;
        if (Application.CanStreamedLevelBeLoaded(scene)) return true;
        Debug.LogError($"[StoryBeat] {name}: '{scene}' 씬이 Build Profiles 씬 목록에 없어 이동하지 않습니다.", this);
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other)) playerInZone = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other)) playerInZone = false;
    }

    private static bool IsPlayer(Collider2D other) =>
        other.attachedRigidbody && other.attachedRigidbody.GetComponent<PlayerController2D>();

    private void OnValidate()
    {
        if (nextStep == requiredStep)
            Debug.LogWarning($"[StoryBeat] {name}: Required Step과 Next Step이 같으면 이 장면이 계속 반복됩니다.", this);
    }
}
