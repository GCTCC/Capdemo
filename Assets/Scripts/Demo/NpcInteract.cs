using System.Collections.Generic;
using UnityEngine;

// 대화할 수 있는 NPC. 트리거 콜라이더 안에 플레이어가 들어오면 머리 위에 안내 표시를 띄운다.
// 대사는 NPC 대화 에셋(NpcDialogue)을 넣어 쓰고, 비워 두면 아래 Lines에 직접 적은 대사를 쓴다.
// 형식은 스토리와 같다(StoryLine). 이름 입력(Pause) 설정은 무시한다.
[RequireComponent(typeof(Collider2D))]
public class NpcInteract : MonoBehaviour
{
    [Tooltip("이 NPC의 대사 파일 (Create > CapDemo > NPC 대화)")]
    [SerializeField] private NpcDialogue dialogue;
    [Tooltip("Dialogue가 비어 있을 때만 쓰는 대사")]
    [SerializeField] private List<StoryLine> lines = new List<StoryLine>();
    [Tooltip("플레이어가 가까이 오면 보일 안내 표시 (예: \"Space\")")]
    [SerializeField] private GameObject prompt;

    private PlayerController2D playerInRange; // 상호작용 가능하게 할 범위
    private bool talking; // 대화중에는 이동 제어. 제어를 위한 bool

    public IReadOnlyList<StoryLine> Lines => dialogue ? dialogue.Lines : lines;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        RefreshPrompt();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = FindPlayer(other);
        if (!player) return;
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
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        if (prompt) prompt.SetActive(playerInRange && !talking);
    }

    private static PlayerController2D FindPlayer(Collider2D other) =>
        other.attachedRigidbody ? other.attachedRigidbody.GetComponent<PlayerController2D>() : null;
}
