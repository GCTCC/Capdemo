using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화자 머리 옆에 뜨는 이름표(머리 점 + 꺾인 선 꼬리 + 이름 상자).
// 대화창 Canvas(Screen Space - Overlay) 안에 있어야 한다. 매 프레임 캐릭터 머리 위치를 따라간다.
// 꼬리 그림(SpeakerTail)은 곧은 선 한 토막이라 비스듬한 선·가로 선 두 개에 똑같이 늘려 쓴다.
//
// 쓰는 방법 두 가지
// - 맵 대화(DialogueBox.BeginConversation): 참가자마다 복제본을 Open으로 펼쳐 두고, 말하는 사람만 밝게(SetDimmed), 끝나면 Fold로 접는다.
// - 예전 방식(스토리 씬 등): 이 오브젝트 하나를 Show/Hide. 화자가 바뀌면 그 사람에게 바로 옮겨 간다.
//
// 펼칠 때: 꼬리 끝 마름모 → 비스듬한 선 → 가로 선 → 이름표(꼬리 쪽에서부터 드러남). 접을 때는 거꾸로.
public class SpeakerNameTag : MonoBehaviour
{
    [SerializeField] private RectTransform box;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("머리에서 비스듬히 올라가는 선")]
    [SerializeField] private RectTransform tailSlant;
    [Tooltip("비스듬한 선 끝에서 이름 상자까지 가로로 이어지는 선")]
    [SerializeField] private RectTransform tailFlat;
    [Tooltip("머리 점에 찍히는 꼬리 끝 장식(없어도 된다). 왼쪽으로 뒤집힐 때 좌우 반전한다")]
    [SerializeField] private RectTransform tailEnd;
    [Tooltip("이름 상자를 감싼 마스크. 이걸로 이름표를 꼬리 쪽에서부터 드러낸다(글씨가 찌그러지지 않음). 비어 있으면 가로로 늘려서 펼친다")]
    [SerializeField] private RectMask2D boxMask;

    [Tooltip("머리 점에서 이름 상자 왼쪽 아래까지 거리 (1920x1080 기준)")]
    [SerializeField] private Vector2 boxOffset = new Vector2(70f, 55f);
    [Tooltip("선 굵기")]
    [SerializeField, Min(1f)] private float lineWidth = 2f;
    [Tooltip("캐릭터가 화면 이 비율보다 오른쪽에 있으면 이름표를 왼쪽에 띄운다")]
    [SerializeField, Range(0.5f, 1f)] private float flipAt = 0.72f;

    [Header("애니메이션 (초)")]
    [Tooltip("다 펼쳐지는 데 걸리는 시간")]
    [SerializeField, Min(0f)] private float unfoldTime = 0.35f;
    [Tooltip("다 접히는 데 걸리는 시간")]
    [SerializeField, Min(0f)] private float foldTime = 0.25f;
    [Tooltip("말하지 않는 참가자 이름표의 불투명도")]
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.4f;
    [Tooltip("밝아지거나 흐려지는 시간")]
    [SerializeField, Min(0f)] private float fadeTime = 0.15f;

    // 펼침 진행도(0~1) 중 각 부분이 움직이는 구간
    private const float TipEnd = 0.15f;
    private const float SlantEnd = 0.4f;
    private const float FlatEnd = 0.6f;

    private SpeakerTag target;
    private Canvas canvas;
    private CanvasGroup group;
    private float progress;     // 0 = 다 접힘, 1 = 다 펼침
    private bool folding;
    private float alphaTarget = 1f;
    private bool snapAlpha;     // 새로 펼칠 때는 밝기를 서서히 바꾸지 않고 바로 맞춘다

    public SpeakerTag Target => target;
    // 접히는 중이면 true. 다 접히면 스스로 꺼진다
    public bool IsFolding => folding && isActiveAndEnabled;

    private CanvasGroup Group
    {
        get
        {
            if (group) return group;
            group = GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }
    }

    // 예전 방식: 이 화자 옆에 띄운다. 꺼져 있었으면 펼치고, 이미 떠 있으면 바로 옮겨 간다
    public void Show(SpeakerTag speaker, string speakerName)
    {
        if (!speaker)
        {
            Hide();
            return;
        }
        bool wasVisible = gameObject.activeSelf && target;
        if (!wasVisible) progress = 0f;
        folding = false;
        target = speaker;
        alphaTarget = 1f;
        snapAlpha = true;
        gameObject.SetActive(true);
        SetName(speakerName); // 켠 다음에 해야 상자 크기가 계산된다
        Follow();
    }

    // 맵 대화 시작: 처음부터 펼친다
    public void Open(SpeakerTag speaker, string speakerName)
    {
        target = null;
        Show(speaker, speakerName);
    }

    // 말하는 사람이 아니면 흐리게
    public void SetDimmed(bool dim) => alphaTarget = dim ? dimAlpha : 1f;

    // 홀로그램 대화 동안 잠깐 안 보이게 한다 (접지 않고 투명하게만). false면 원래 밝기로 돌아온다
    public void SetHidden(bool value) => hidden = value;
    private bool hidden;

    // 맵 대화 끝: 거꾸로 접은 뒤 스스로 꺼진다
    public void Fold()
    {
        if (!gameObject.activeSelf || !target)
        {
            Hide();
            return;
        }
        folding = true;
    }

    // 애니메이션 없이 바로 끈다
    public void Hide()
    {
        target = null;
        folding = false;
        progress = 0f;
        gameObject.SetActive(false);
    }

    // 부모(대화창)가 꺼지면 다음에 켜질 때 처음부터 펼쳐지도록 접힌 상태로 돌려 둔다
    private void OnDisable()
    {
        target = null;
        folding = false;
        progress = 0f;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (folding)
        {
            progress -= foldTime > 0f ? dt / foldTime : 1f;
            if (progress <= 0f)
            {
                Hide();
                return;
            }
        }
        else progress = unfoldTime > 0f ? Mathf.Min(1f, progress + dt / unfoldTime) : 1f;

        var g = Group;
        float goal = hidden ? 0f : alphaTarget;
        if (snapAlpha)
        {
            g.alpha = goal;
            snapAlpha = false;
        }
        else
        {
            float range = hidden || g.alpha < dimAlpha ? 1f : Mathf.Max(0.01f, 1f - dimAlpha);
            float speed = fadeTime > 0f ? range / fadeTime : 100f;
            g.alpha = Mathf.MoveTowards(g.alpha, goal, dt * speed);
        }
        Follow();
    }

    private void SetName(string speakerName)
    {
        if (nameText) nameText.text = speakerName;
        if (box) LayoutRebuilder.ForceRebuildLayoutImmediate(box); // 이름 길이에 맞춘 상자 크기를 바로 알아야 마스크를 맞춘다
    }

    private void Follow()
    {
        if (!target || !Camera.main)
        {
            if (gameObject.activeSelf && !target) gameObject.SetActive(false);
            return;
        }
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        float scale = canvas ? canvas.scaleFactor : 1f;

        Vector3 screen = Camera.main.WorldToScreenPoint(target.HeadPosition);
        // 화면 밖으로 나가지 않게 가장자리 안쪽으로 붙잡는다
        screen.x = Mathf.Clamp(screen.x, 20f * scale, Screen.width - 20f * scale);
        screen.y = Mathf.Clamp(screen.y, 20f * scale, Screen.height - (boxOffset.y + 50f) * scale);
        transform.position = new Vector3(screen.x, screen.y, 0f);

        float side = screen.x > Screen.width * flipAt ? -1f : 1f;
        var end = new Vector2(boxOffset.x * side, boxOffset.y);
        var bend = new Vector2(end.x * 0.45f, end.y);

        // 부분별 진행도(0~1): 꼬리 끝 → 비스듬한 선 → 가로 선 → 이름표
        float tip = EaseOut(Segment(0f, TipEnd));
        float slant = EaseOut(Segment(TipEnd, SlantEnd));
        float flat = EaseOut(Segment(SlantEnd, FlatEnd));
        float plate = EaseOut(Segment(FlatEnd, 1f));

        if (tailEnd)
        {
            tailEnd.anchoredPosition = Vector2.zero;
            tailEnd.localRotation = Quaternion.identity;
            tailEnd.localScale = new Vector3(tip * side, tip, 1f);
        }
        PlaceLine(tailSlant, Vector2.zero, bend, slant);
        PlaceLine(tailFlat, bend, end, flat);
        PlacePlate(end, side, plate);
    }

    // 이름표: 꼬리가 붙는 쪽 끝을 기준으로 둔다(오른쪽에 뜨면 왼쪽 끝, 왼쪽에 뜨면 오른쪽 끝)
    private void PlacePlate(Vector2 end, float side, float shown)
    {
        var pivot = new Vector2(side > 0f ? 0f : 1f, 0.5f);
        if (boxMask)
        {
            // 마스크를 이름표 크기에 맞추고, 꼬리 반대쪽을 가려 두었다가 걷어 낸다
            var mask = boxMask.rectTransform;
            mask.anchorMin = mask.anchorMax = new Vector2(0.5f, 0.5f);
            mask.pivot = pivot;
            mask.anchoredPosition = end;
            mask.localScale = Vector3.one;
            Vector2 size = Vector2.zero;
            if (box)
            {
                box.anchorMin = box.anchorMax = box.pivot = pivot;
                box.anchoredPosition = Vector2.zero;
                box.localScale = Vector3.one;
                size = box.rect.size;
            }
            mask.sizeDelta = size;
            float hidden = size.x * (1f - shown);
            boxMask.padding = side > 0f ? new Vector4(0f, 0f, hidden, 0f) : new Vector4(hidden, 0f, 0f, 0f);
        }
        else if (box)
        {
            box.pivot = pivot;
            box.anchoredPosition = end;
            box.localScale = new Vector3(shown, 1f, 1f);
        }
    }

    private float Segment(float from, float to) => Mathf.Clamp01((progress - from) / (to - from));

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // from에서 to 방향으로 shown(0~1) 비율만큼만 선을 긋는다
    private void PlaceLine(RectTransform line, Vector2 from, Vector2 to, float shown)
    {
        if (!line) return;
        var delta = to - from;
        line.pivot = new Vector2(0f, 0.5f);
        line.anchoredPosition = from;
        line.sizeDelta = new Vector2(delta.magnitude * shown, lineWidth);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }
}
