using UnityEngine;

// DemoScene의 NPC 대화 진행: 대사창을 열고, 한 줄씩 넘기고, 끝나면 닫는다.
// 입력은 PlayerController2D가 받아서 Advance를 부른다. 대화 중에는 카메라가 플레이어와 NPC를 클로즈업한다.
public class DemoDialogue : MonoBehaviour
{
    [Tooltip("스토리와 같은 대사창. 대화하지 않을 때는 꺼 둔다.")]
    [SerializeField] private DialogueBox box;
    [SerializeField] private FollowCamera2D followCamera;

    private NpcInteract npc;
    private int lineIndex;

    public bool IsOpen => npc;

    private void Awake()
    {
        if (box) box.gameObject.SetActive(false);
    }

    public void Open(NpcInteract target, Transform player)
    {
        if (IsOpen || !target || target.Lines.Count == 0 || !box) return;

        npc = target;
        npc.SetTalking(true);
        box.gameObject.SetActive(true);
        if (followCamera) followCamera.FocusOn(player, npc.transform);

        lineIndex = 0;
        box.Show(npc.Lines[lineIndex]);
    }

    // 출력 중이면 대사를 한 번에 보여 주고, 다 나왔으면 다음 줄로. 마지막 줄 다음에는 닫는다
    public void Advance()
    {
        if (!IsOpen) return;
        if (box.IsTyping)
        {
            box.Complete();
            return;
        }

        lineIndex++;
        if (lineIndex < npc.Lines.Count) box.Show(npc.Lines[lineIndex]);
        else Close();
    }

    private void Close()
    {
        box.Clear();
        box.gameObject.SetActive(false);
        if (followCamera) followCamera.ClearFocus();
        npc.SetTalking(false);
        npc = null;
    }
}
