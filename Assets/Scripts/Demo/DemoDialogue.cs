using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// DemoScene의 NPC 대화 진행: 대사창을 열고, 한 줄씩 넘기고, 끝나면 닫는다.
// 입력은 PlayerController2D가 받아서 Advance를 부른다. 대화 중에는 카메라가 플레이어와 NPC를 클로즈업한다.
// 홀로그램(폰) 대화: Holo Start가 체크된 줄에서 카메라가 살짝 오른쪽으로 가고 홀로그램이 펼쳐진다.
// Holo End가 체크된 줄을 넘기면 홀로그램이 접히고 카메라가 돌아온다. 그 사이 Holo Message 줄은 말풍선,
// 체크 안 한 줄은 독백으로 아래 대화창에 이름표 없이 나온다.
// 대사 줄 효과음(Sounds)은 챕터와 같은 타이밍(LineSoundPlayer)으로 낸다. 말풍선 줄은 말풍선 글자가 나오는 것에 맞춘다.
// 오른쪽 위 AUTO(StoryControls)가 켜져 있으면 대사가 다 나온 뒤 잠깐 기다렸다가 저절로 넘어간다. 일시정지 중에는 넘어가지 않는다.
public class DemoDialogue : MonoBehaviour
{
    [Tooltip("스토리와 같은 대사창. 대화하지 않을 때는 꺼 둔다.")]
    [SerializeField] private DialogueBox box;
    [SerializeField] private FollowCamera2D followCamera;
    [Tooltip("홀로그램(폰) 대화 화면. 비우면 이 캔버스 안에서 찾는다 (Tools > CapDemo > 맵 > 홀로그램 폰 만들기)")]
    [SerializeField] private HoloPhone holo;
    [Tooltip("카메라가 옆으로 움직이기 시작하고 이만큼(초) 뒤에 홀로그램이 펼쳐진다")]
    [SerializeField, Min(0f)] private float holoDelay = 0.25f;
    [Tooltip("대사 효과음 재생기(2D). 비우면 실행할 때 이 오브젝트에서 찾고, 없으면 붙인다. NPC마다 따로 둘 필요 없다")]
    [SerializeField] private StoryAudio sfx;

    private NpcInteract npc;
    private IReadOnlyList<StoryLine> lines; // 대화를 연 순간의 대사. 대화 중에 NPC 쪽 대사가 바뀌어도 끝까지 이것을 쓴다
    private int lineIndex;
    private bool closing; // 이름표가 접히는 중. 이 동안은 입력을 받지 않는다
    private bool holoOn;  // 홀로그램이 떠 있는 구간인지
    private bool busy;    // 홀로그램이 펼쳐지거나 접히는 중. 이 동안은 입력을 받지 않는다

    private LineSoundPlayer lineSounds;
    private bool soundFromHolo;      // 지금 줄의 효과음 타이밍을 말풍선 글자에 맞추는지(아니면 아래 대화창 글자)
    private object conversationScope; // 대화 한 번. Loop Until이 CutEnd인 반복음은 대화가 끝날 때 멈춘다
    private float autoWaited;         // AUTO: 지금 줄을 다 보여 준 뒤 기다린 시간

    public bool IsOpen => npc;

    // 지금 줄이 홀로그램 말풍선으로 나오고 있는지
    private bool CurrentIsMessage => holoOn && lines != null && lineIndex < lines.Count && lines[lineIndex].HoloMessage;

    private void Awake()
    {
        if (box) box.gameObject.SetActive(false);
        if (!followCamera && Camera.main) followCamera = Camera.main.GetComponent<FollowCamera2D>();
        if (!holo) holo = GetComponentInChildren<HoloPhone>(true);
        if (!sfx) sfx = GetComponent<StoryAudio>();
        if (!sfx) sfx = gameObject.AddComponent<StoryAudio>();
        lineSounds = new LineSoundPlayer(sfx, this, nameof(DemoDialogue));
    }

    // 출력되는 글자에 맞춰 효과음을 낸다. AUTO가 켜져 있으면 다 나온 줄을 저절로 넘긴다
    private void Update()
    {
        UpdateAuto();
        if (lineSounds == null || lineSounds.Line == null) return;
        if (soundFromHolo)
        {
            if (!holo) return;
            lineSounds.Tick(holo.VisibleCharacters);
            if (!holo.IsTyping) lineSounds.TypingDone();
        }
        else
        {
            lineSounds.Tick(box.VisibleCharacters);
            if (!box.IsTyping) lineSounds.TypingDone();
        }
    }

    public void Open(NpcInteract target, Transform player)
    {
        if (IsOpen || !target || target.Lines.Count == 0 || !box) return;

        npc = target;
        lines = target.Lines;
        npc.SetTalking(true);
        box.gameObject.SetActive(true);
        box.PreferredSpeaker = npc.transform;
        if (followCamera) followCamera.FocusOn(player, npc.transform);

        lineIndex = 0;
        holoOn = false;
        busy = false;
        conversationScope = new object();
        box.BeginConversation(lines); // 참가자 이름표를 한꺼번에 펼친다
        ShowLine(lineIndex);
    }

    // AUTO: 지금 줄이 다 나왔으면 대사 길이에 맞춘 시간만큼 기다렸다가 넘긴다
    private void UpdateAuto()
    {
        if (!IsOpen || closing || busy || !StoryControls.AutoOn || StoryControls.BlocksAdvance)
        {
            autoWaited = 0f;
            return;
        }
        bool typing = CurrentIsMessage ? holo.IsTyping : box.IsTyping;
        if (typing)
        {
            autoWaited = 0f;
            return;
        }
        autoWaited += Time.unscaledDeltaTime;
        int chars = CurrentIsMessage ? holo.VisibleCharacters : box.VisibleCharacters;
        if (autoWaited >= StoryControls.AutoDelay(chars)) Advance();
    }

    // 출력 중이면 대사를 한 번에 보여 주고, 다 나왔으면 다음 줄로. 마지막 줄 다음에는 닫는다
    public void Advance()
    {
        if (!IsOpen || closing || busy) return;
        if (GamePause.IsPaused || HoloPopup.AnyOpen) return; // 일시정지 창이 떠 있는 동안은 넘기지 않는다
        autoWaited = 0f;
        if (CurrentIsMessage)
        {
            if (holo.IsTyping)
            {
                holo.Complete();
                return;
            }
        }
        else if (box.IsTyping)
        {
            box.Complete();
            return;
        }

        lineSounds.Next(); // OnNext 효과음, 이 줄의 반복음 멈춤
        bool endHolo = holoOn && lines[lineIndex].HoloEnd;
        lineIndex++;
        if (endHolo) StartCoroutine(CloseHoloThenShow(lineIndex));
        else if (lineIndex < lines.Count) ShowLine(lineIndex);
        else Close();
    }

    // 이 줄에서 홀로그램이 시작되면 먼저 펼치고, 아니면 바로 보여 준다
    private void ShowLine(int index)
    {
        var line = lines[index];
        // Holo Start를 빼먹고 Holo Message만 체크한 줄도 홀로그램을 켠다
        if (!holoOn && holo && (line.HoloStart || line.HoloMessage))
        {
            StartCoroutine(OpenHoloThenShow(index));
            return;
        }
        ShowContent(line);
    }

    // 말풍선 줄은 홀로그램 안에, 나머지(독백 포함)는 아래 대화창에
    private void ShowContent(StoryLine line)
    {
        if (holoOn && line.HoloMessage)
        {
            box.Clear();
            box.SetBoxHidden(true);
            holo.AddMessage(StoryText.Format(line.Speaker), StoryText.Format(line.Text), line.IsPlayerSpeaker);
            soundFromHolo = true;
            lineSounds.Begin(line, conversationScope, holo.FindText);
            lineSounds.Tick(holo.VisibleCharacters);
        }
        else
        {
            if (holo) holo.ShowIndicator = false;
            box.SetBoxHidden(false);
            box.Show(line);
            soundFromHolo = false;
            lineSounds.Begin(line, conversationScope, box.FindText);
            lineSounds.Tick(box.VisibleCharacters);
        }
    }

    // 카메라가 오른쪽으로 가고, 조금 뒤 홀로그램이 지직거리며 펼쳐진 다음 그 줄을 보여 준다
    private IEnumerator OpenHoloThenShow(int index)
    {
        busy = true;
        holoOn = true;
        box.Clear();
        box.SetHoloMode(true);
        box.SetBoxHidden(true);
        if (followCamera) followCamera.SetHoloShift(true);
        if (holoDelay > 0f) yield return new WaitForSecondsRealtime(holoDelay);

        holo.Open(FindContactName(index));
        while (holo.IsAnimating) yield return null;
        busy = false;
        ShowContent(lines[index]);
    }

    // 홀로그램을 접고, 카메라가 돌아오기 시작하면 다음 줄을 보여 준다 (등장의 역순)
    private IEnumerator CloseHoloThenShow(int next)
    {
        busy = true;
        yield return CloseHolo();
        busy = false;
        if (next < lines.Count) ShowLine(next);
        else Close();
    }

    private IEnumerator CloseHolo()
    {
        box.Clear();
        box.SetBoxHidden(true);
        holo.Close();
        while (holo.IsAnimating) yield return null;
        if (followCamera) followCamera.SetHoloShift(false);
        box.SetHoloMode(false);
        holoOn = false;
        if (holoDelay > 0f) yield return new WaitForSecondsRealtime(holoDelay);
    }

    // 홀로그램 맨 위에 띄울 상대 이름: 이 구간에서 주인공이 아닌 첫 말풍선 화자
    private string FindContactName(int start)
    {
        for (int i = start; i < lines.Count; i++)
        {
            var line = lines[i];
            if (i > start && line.HoloStart) break;
            if (line.HoloMessage && !line.IsPlayerSpeaker && !string.IsNullOrEmpty(line.Speaker))
                return StoryText.Format(line.Speaker);
            if (line.HoloEnd) break;
        }
        return "";
    }

    // 이름표를 모두 접은 뒤에 대화창을 닫는다
    private void Close()
    {
        if (closing) return;
        closing = true;
        StartCoroutine(CloseAfterFold());
    }

    private IEnumerator CloseAfterFold()
    {
        // 접히는 동안 소리가 남지 않게, 대화에 묶인 반복음을 먼저 멈춘다
        lineSounds.Next();
        if (conversationScope != null) sfx.StopLoops(conversationScope);

        // Holo End 없이 대화가 끝나도 홀로그램을 먼저 접는다
        if (holoOn) yield return CloseHolo();
        box.SetBoxHidden(false);

        box.EndConversation();
        while (box.IsFolding) yield return null;

        box.Clear();
        box.PreferredSpeaker = null;
        box.gameObject.SetActive(false);
        if (followCamera) followCamera.ClearFocus();
        if (npc) npc.SetTalking(false);
        npc = null;
        lines = null;
        closing = false;
    }
}
