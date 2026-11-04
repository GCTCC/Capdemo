using System.Collections.Generic;
using UnityEngine;

// 대화할 수 있는 NPC. 트리거 콜라이더 안에 플레이어가 들어오면 머리 위에 안내 표시를 띄운다.
// 대사는 NPC 대화 에셋(NpcDialogue)을 넣어 쓰고, 비워 두면 아래 Lines에 직접 적은 대사를 쓴다.
// 형식은 스토리와 같다(StoryLine). 이름 입력(Pause) 설정은 무시한다.
// First Meet Dialogue를 넣으면, 처음 First Meet Distance 안으로 들어왔을 때 F 없이 그 대사가 강제로 나온다.
// 끝까지 보면 SaveData에 기록되어(게임을 껐다 켜도 유지) 그 뒤로는 F로 평소 대사(Dialogue)가 나온다.
[RequireComponent(typeof(Collider2D))]
public class NpcInteract : MonoBehaviour
{
    [Tooltip("이 NPC의 대사 파일 (Create > CapDemo > NPC 대화)")]
    [SerializeField] private NpcDialogue dialogue;
    [Tooltip("Dialogue가 비어 있을 때만 쓰는 대사")]
    [SerializeField] private List<StoryLine> lines = new List<StoryLine>();
    [Tooltip("플레이어가 가까이 오면 보일 안내 표시 (예: \"F\")")]
    [SerializeField] private GameObject prompt;

    [Header("첫 만남")]
    [Tooltip("처음 만났을 때 한 번만 강제로 나오는 대사 파일. 비워 두면 첫 만남 대화 없이 F로만 말을 건다.\n" +
             "이 파일 이름으로 '봤음'을 기록하므로, NPC마다 다른 파일을 쓰고 이름을 바꾸지 않는다.")]
    [SerializeField] private NpcDialogue firstMeetDialogue;
    [Tooltip("NPC 중심에서 이 가로 거리(유닛) 안으로 들어오면 첫 만남 대화가 시작된다. 씬 화면의 주황 선이 범위다.")]
    [SerializeField, Min(0f)] private float firstMeetDistance = 4f;

    private PlayerController2D playerInRange; // 상호작용 가능하게 할 범위
    private bool talking; // 대화중에는 이동 제어. 제어를 위한 bool
    private bool playingFirstMeet; // 지금 하는 대화가 첫 만남 대화인지. 끝나면 기록한다
    private PlayerController2D player; // 첫 만남 거리를 재기 위한 씬의 플레이어

    [Tooltip("끄면 말을 걸 수 없는 NPC가 된다(F 표시가 안 뜬다).")]
    [SerializeField] private bool canTalk = true;

    // 첫 만남 대화가 아직 남아 있으면 그것을, 아니면 평소 대사를 쓴다 (F로 말을 걸어도 같다)
    public IReadOnlyList<StoryLine> Lines =>
        FirstMeetPending ? firstMeetDialogue.Lines : dialogue ? dialogue.Lines : lines;

    // 첫 만남 대사가 있고 아직 끝까지 본 적이 없는지
    private bool FirstMeetPending =>
        firstMeetDialogue && firstMeetDialogue.Lines.Count > 0 && !SaveData.HasMetNpc(firstMeetDialogue.name);

    // 대사가 하나도 없으면 말을 걸 수 없는 것으로 친다
    private bool Talkable => canTalk && Lines.Count > 0;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        RefreshPrompt();
    }

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController2D>();
    }

    // 첫 만남 대화가 남아 있으면 플레이어와의 가로 거리를 재서 범위 안이면 대화를 연다
    private void Update()
    {
        if (talking || !canTalk || !player || !FirstMeetPending) return;
        if (Mathf.Abs(player.transform.position.x - transform.position.x) <= firstMeetDistance)
            player.StartTalk(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = FindPlayer(other);
        if (!player || !Talkable) return;
        playerInRange = player;
        player.SetNearbyNpc(this);
        RefreshPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var player = FindPlayer(other);
        if (!player || player != playerInRange) return;
        player.ClearNearbyNpc(this);
        playerInRange = null;
        RefreshPrompt();
    }

    // DemoDialogue가 대화 시작·종료 때 부른다
    public void SetTalking(bool value)
    {
        talking = value;
        if (value) playingFirstMeet = FirstMeetPending;
        else if (playingFirstMeet)
        {
            // 첫 만남 대화를 끝까지 봤으니 기록한다. 이제부터 Lines는 평소 대사가 된다
            SaveData.MarkNpcMet(firstMeetDialogue.name);
            playingFirstMeet = false;
        }
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        // 첫 만남이 끝나고 평소 대사가 비어 있으면 F 표시를 띄우지 않는다
        if (prompt) prompt.SetActive(playerInRange && !talking && Talkable);
    }

    private static PlayerController2D FindPlayer(Collider2D other) =>
        other.attachedRigidbody ? other.attachedRigidbody.GetComponent<PlayerController2D>() : null;

    // NPC를 선택하면 첫 만남 범위를 주황 세로선 두 개로 보여 준다
    private void OnDrawGizmosSelected()
    {
        if (!firstMeetDialogue) return;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        var center = transform.position;
        foreach (float side in new[] { -1f, 1f })
        {
            var x = center.x + side * firstMeetDistance;
            Gizmos.DrawLine(new Vector3(x, center.y - 1f, 0f), new Vector3(x, center.y + 4f, 0f));
        }
    }
}
