using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 휴대폰 문자 장면을 사이버펑크 홀로콜처럼 보여 주는 홀로그램 화면.
// 대화 에셋에서 Holo Start ~ Holo End 사이 줄 동안 떠 있고, Holo Message가 체크된 줄이 말풍선으로 쌓인다.
// 상대 문자는 왼쪽(프로필 사진과 함께), 주인공 답장은 오른쪽. 맨 위에는 상대 이름이 계속 떠 있다.
// 진행(언제 펼치고 접을지)은 DemoDialogue(맵 대화)·StoryManager(챕터)가 정한다. 이 스크립트는 보여 주기만 한다.
// 챕터에서는 Holo Start 줄의 Holo Position·Holo Scale로 자리와 크기를 정한다(SetPlacement). 맵에서는 프리팹에 놓인 자리 그대로.
// 만들기: Tools > CapDemo > 맵 > 홀로그램 폰 만들기 (DialogueCanvas 프리팹에 붙는다)
//         Tools > CapDemo > 스토리 > 홀로그램 폰 붙이기 (열린 씬의 StoryCanvas에 붙는다)
// 그림 교체: Assets/Art/UI/Holo 폴더의 PNG를 같은 이름으로 덮어쓰거나, 아래 칸에 다른 그림을 넣는다.
public class HoloPhone : MonoBehaviour
{
    [System.Serializable]
    public class Contact
    {
        [Tooltip("대사의 화자 이름과 똑같이 적는다")]
        public string name;
        [Tooltip("이 사람의 프로필 사진. 비우면 기본 프로필")]
        public Sprite profile;
    }

    [Header("연결 (메뉴로 만들면 자동으로 채워진다)")]
    [Tooltip("펼치고 접히는 본체. 가운데를 기준으로 위아래로 늘어난다")]
    [SerializeField] private RectTransform body;
    [SerializeField] private CanvasGroup group;
    [Tooltip("맨 위 상대 이름")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("말풍선이 쌓이는 곳 (Viewport 안, 아래에서부터 쌓인다)")]
    [SerializeField] private RectTransform content;
    [Tooltip("말풍선 출력이 끝나면 깜빡일 ▼")]
    [SerializeField] private Graphic nextIndicator;
    [Tooltip("흘러가는 스캔라인 (반복 텍스처)")]
    [SerializeField] private RawImage scanlines;

    [Header("말풍선")]
    [SerializeField] private Sprite bubbleSprite;
    [Tooltip("연락처에 없는 사람의 프로필 사진")]
    [SerializeField] private Sprite defaultProfile;
    [Tooltip("사람마다 다른 프로필 사진을 쓰고 싶을 때 추가")]
    [SerializeField] private List<Contact> contacts = new List<Contact>();
    [SerializeField] private TMP_FontAsset font;
    [SerializeField, Min(1f)] private float fontSize = 28f;
    [SerializeField] private Color textColor = new Color(0.85f, 1f, 1f, 1f);
    [Tooltip("상대 말풍선 색")]
    [SerializeField] private Color otherBubbleColor = new Color(0.2f, 0.9f, 1f, 0.28f);
    [Tooltip("주인공 말풍선 색")]
    [SerializeField] private Color myBubbleColor = new Color(0.35f, 1f, 0.9f, 0.5f);
    [Tooltip("말풍선 안 글자가 이 너비를 넘으면 줄바꿈")]
    [SerializeField, Min(50f)] private float maxTextWidth = 300f;
    [Tooltip("말풍선 안쪽 여백 (가로, 세로)")]
    [SerializeField] private Vector2 bubblePadding = new Vector2(22f, 14f);
    [SerializeField, Min(0f)] private float profileSize = 56f;
    [Tooltip("프로필 사진과 말풍선 사이 간격")]
    [SerializeField, Min(0f)] private float profileGap = 12f;
    [Tooltip("말풍선 사이 세로 간격")]
    [SerializeField, Min(0f)] private float spacing = 16f;
    [Tooltip("말풍선 영역 양옆 여백")]
    [SerializeField, Min(0f)] private float sidePadding = 18f;
    [Tooltip("초당 출력할 글자 수")]
    [SerializeField, Min(1f)] private float charsPerSecond = 30f;
    [Tooltip("이보다 많이 쌓이면 가장 오래된 말풍선부터 지운다")]
    [SerializeField, Min(1)] private int maxMessages = 12;
    [Tooltip("새 말풍선이 나타나고 이전 말풍선이 밀려 올라가는 시간(초)")]
    [SerializeField, Min(0.01f)] private float slideTime = 0.18f;
    [SerializeField, Min(0.05f)] private float blinkPeriod = 0.8f;

    [Header("등장 · 퇴장")]
    [Tooltip("가운데에 가는 선이 옆으로 그어지는 시간(초)")]
    [SerializeField, Min(0f)] private float lineTime = 0.12f;
    [Tooltip("가는 선이 위아래로 펼쳐지는 시간(초). 접힐 때는 거꾸로")]
    [SerializeField, Min(0f)] private float expandTime = 0.28f;
    [Tooltip("펼쳐지기 전 가는 선의 두께 (본체 높이 기준 비율)")]
    [SerializeField, Range(0.002f, 0.1f)] private float lineThickness = 0.01f;
    [Tooltip("펼치고 접는 동안 지직거리는 정도 (0이면 깜빡임 없음)")]
    [SerializeField, Range(0f, 1f)] private float openFlicker = 0.6f;

    [Header("홀로그램 효과")]
    [Tooltip("스캔라인이 흘러가는 속도")]
    [SerializeField] private float scanlineSpeed = 0.6f;
    [Tooltip("스캔라인 간격(픽셀). 작을수록 촘촘하다")]
    [SerializeField, Min(1f)] private float scanlineSpacing = 4f;
    [Tooltip("글리치(지직) 사이 간격(초). 이 범위에서 무작위")]
    [SerializeField] private Vector2 glitchInterval = new Vector2(1.5f, 4f);
    [Tooltip("글리치 한 번이 이어지는 시간(초). 이 범위에서 무작위")]
    [SerializeField] private Vector2 glitchDuration = new Vector2(0.05f, 0.15f);
    [Tooltip("글리치 때 옆으로 흔들리는 거리(픽셀)")]
    [SerializeField, Min(0f)] private float glitchShift = 8f;
    [Tooltip("글리치 때 가장 흐려지는 밝기")]
    [SerializeField, Range(0f, 1f)] private float glitchMinAlpha = 0.5f;

    private enum State { Closed, Opening, Open, Closing }
    private State state = State.Closed;
    private float animTime; // 0 = 다 접힘 ~ lineTime + expandTime = 다 펼침

    private class Message
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float height;
        public float y;        // 지금 높이 (아래에서부터)
        public float appear;   // 0 → 1 나타나는 정도
    }
    private readonly List<Message> messages = new List<Message>();

    private TMP_Text typingText;
    private float typedChars;
    private int totalChars;
    private bool waitingMessage; // 펼쳐지는 중에 들어온 말풍선. 다 펼친 뒤 출력을 시작한다

    private Vector2 bodyBasePosition;
    private float nextGlitch;
    private float glitchLeft;

    public bool IsTyping { get; private set; }
    // 지금 출력 중인 말풍선에서 화면에 나온 글자 수 (효과음 타이밍용). 출력이 끝났으면 전체 글자 수
    public int VisibleCharacters => IsTyping ? Mathf.Min((int)typedChars, totalChars) : totalChars;
    // 펼치거나 접는 중이면 true
    public bool IsAnimating => state == State.Opening || state == State.Closing;
    public bool IsOpen => state != State.Closed;
    // 말풍선 대사를 보고 있을 때만 ▼를 깜빡인다. 독백 줄에서는 DemoDialogue가 끈다
    public bool ShowIndicator { get; set; }

    private float FullTime => Mathf.Max(0.0001f, lineTime + expandTime);

    // 화면 기준 크기 (1920x1080). Holo Position은 이 기준의 좌표(왼쪽 아래 0,0)
    public const float RefWidth = 1920f;
    public const float RefHeight = 1080f;

    // 프리팹에 놓인 원래 자리. SetPlacement로 옮긴 뒤 ResetPlacement로 돌아온다
    private bool placementCaptured;
    private Vector2 baseAnchorMin, baseAnchorMax, baseAnchoredPosition;
    private Vector3 baseScale;

    // 챕터용: 홀로그램 가운데를 refPoint(1920x1080 기준)에 두고 크기를 scale배로 한다
    public void SetPlacement(Vector2 refPoint, float scale)
    {
        var rect = (RectTransform)transform;
        CapturePlacement();
        var anchor = new Vector2(refPoint.x / RefWidth, refPoint.y / RefHeight);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        float s = scale > 0f ? scale : 1f;
        rect.localScale = new Vector3(baseScale.x * s, baseScale.y * s, 1f);
    }

    // 프리팹에 놓인 원래 자리·크기로 되돌린다
    public void ResetPlacement()
    {
        if (!placementCaptured) return;
        var rect = (RectTransform)transform;
        rect.anchorMin = baseAnchorMin;
        rect.anchorMax = baseAnchorMax;
        rect.anchoredPosition = baseAnchoredPosition;
        rect.localScale = baseScale;
        placementCaptured = false; // 다음에 옮길 때 지금 자리를 새로 기억한다 (그 사이 재이가 옮긴 자리를 지키려고)
    }

    private void CapturePlacement()
    {
        if (placementCaptured) return;
        placementCaptured = true;
        var rect = (RectTransform)transform;
        baseAnchorMin = rect.anchorMin;
        baseAnchorMax = rect.anchorMax;
        baseAnchoredPosition = rect.anchoredPosition;
        baseScale = rect.localScale;
    }

    private RectTransform CanvasRect
    {
        get
        {
            var canvas = GetComponentInParent<Canvas>(true);
            return canvas ? (RectTransform)canvas.rootCanvas.transform : null;
        }
    }

    // 1920x1080 기준 좌표 ↔ 월드 좌표 (씬 뷰에서 끌어서 자리를 정할 때 쓴다)
    public Vector3 ReferenceToWorld(Vector2 refPoint)
    {
        var root = CanvasRect;
        if (!root) return refPoint;
        var size = root.rect.size;
        var local = new Vector2((refPoint.x / RefWidth - 0.5f) * size.x, (refPoint.y / RefHeight - 0.5f) * size.y);
        return root.TransformPoint(local);
    }

    public Vector2 WorldToReference(Vector3 world)
    {
        var root = CanvasRect;
        if (!root) return world;
        Vector2 local = root.InverseTransformPoint(world);
        var size = root.rect.size;
        return new Vector2((local.x / size.x + 0.5f) * RefWidth, (local.y / size.y + 0.5f) * RefHeight);
    }

    // 에디터 미리보기용: 애니메이션 없이 다 펼친 상태로, 말풍선들을 출력이 끝난 채로 바로 보여 준다
    public void ShowInstant(string contactName, IReadOnlyList<StoryLine> messageLines)
    {
        HideNow();
        if (titleText) titleText.text = contactName;
        if (body) body.gameObject.SetActive(true);
        state = State.Open;
        animTime = FullTime;
        glitchLeft = 0f;
        if (messageLines != null)
            foreach (var line in messageLines)
                AddMessage(StoryText.Format(line.Speaker), StoryText.Format(line.Text), line.IsPlayerSpeaker);
        Complete();
        UpdateMessages(1000f); // 밀려 올라가는 움직임 없이 제자리로
        Apply();
        SetIndicator(messageLines != null && messageLines.Count > 0);
    }

    // 실행 중에는 Destroy, 에디터(미리보기)에서는 바로 지운다
    private static void Kill(Object target)
    {
        if (!target) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private void Awake()
    {
        if (body) bodyBasePosition = body.anchoredPosition;
        HideNow();
    }

    // 홀로그램을 펼친다. 상대 이름은 맨 위에 계속 떠 있다
    public void Open(string contactName)
    {
        ClearMessages();
        if (titleText) titleText.text = contactName;
        ShowIndicator = false;
        if (body) body.gameObject.SetActive(true);
        if (state == State.Closed) animTime = 0f;
        state = State.Opening;
        ScheduleGlitch();
        Apply();
    }

    // 홀로그램을 거꾸로 접는다. 다 접히면 말풍선도 지운다
    public void Close()
    {
        if (state == State.Closed) return;
        Complete();
        ShowIndicator = false;
        state = State.Closing;
    }

    // 애니메이션 없이 바로 끈다
    public void HideNow()
    {
        state = State.Closed;
        animTime = 0f;
        IsTyping = false;
        waitingMessage = false;
        ShowIndicator = false;
        SetIndicator(false);
        ClearMessages();
        if (body)
        {
            body.anchoredPosition = bodyBasePosition;
            body.gameObject.SetActive(false);
        }
    }

    // 말풍선 하나를 아래에 추가하고 한 글자씩 출력한다. mine이면 오른쪽(주인공), 아니면 왼쪽(상대 + 프로필)
    public void AddMessage(string speaker, string text, bool mine)
    {
        if (!content) return;
        Complete();

        var row = new GameObject(mine ? "Message (나)" : $"Message ({speaker})", typeof(RectTransform), typeof(CanvasGroup));
        var rect = (RectTransform)row.transform;
        rect.SetParent(content, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        var rowGroup = row.GetComponent<CanvasGroup>();
        rowGroup.blocksRaycasts = false;

        // 글자
        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        var bubbleRect = (RectTransform)bubble.transform;
        bubbleRect.SetParent(rect, false);
        var bubbleImage = bubble.GetComponent<Image>();
        bubbleImage.sprite = bubbleSprite;
        bubbleImage.type = bubbleSprite && bubbleSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        bubbleImage.color = mine ? myBubbleColor : otherBubbleColor;
        bubbleImage.raycastTarget = false;

        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)label.transform;
        labelRect.SetParent(bubbleRect, false);
        var tmp = label.GetComponent<TextMeshProUGUI>();
        if (font) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.text = text;

        // 글자 크기에 맞춰 말풍선 크기를 정한다 (출력 중에 말풍선 크기가 바뀌지 않도록 미리)
        float areaWidth = content.rect.width - sidePadding * 2f - (mine ? 0f : profileSize + profileGap);
        float wrapWidth = Mathf.Max(50f, Mathf.Min(maxTextWidth, areaWidth - bubblePadding.x * 2f));
        Vector2 size = tmp.GetPreferredValues(text, wrapWidth, 0f);
        float textWidth = Mathf.Min(wrapWidth, Mathf.Ceil(size.x) + 2f);
        float textHeight = Mathf.Ceil(tmp.GetPreferredValues(text, textWidth, 0f).y);

        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.sizeDelta = new Vector2(textWidth, textHeight);
        labelRect.anchoredPosition = new Vector2(bubblePadding.x, -bubblePadding.y);

        Vector2 bubbleSize = new Vector2(textWidth, textHeight) + bubblePadding * 2f;
        float rowHeight = Mathf.Max(bubbleSize.y, mine ? 0f : profileSize);
        float side = mine ? 1f : 0f;
        bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(side, 1f);
        bubbleRect.pivot = new Vector2(side, 1f);
        bubbleRect.sizeDelta = bubbleSize;
        bubbleRect.anchoredPosition = mine
            ? new Vector2(-sidePadding, 0f)
            : new Vector2(sidePadding + profileSize + profileGap, 0f);

        // 상대: 프로필 사진
        if (!mine)
        {
            var profile = new GameObject("Profile", typeof(RectTransform), typeof(Image));
            var profileRect = (RectTransform)profile.transform;
            profileRect.SetParent(rect, false);
            profileRect.anchorMin = profileRect.anchorMax = new Vector2(0f, 1f);
            profileRect.pivot = new Vector2(0f, 1f);
            profileRect.sizeDelta = new Vector2(profileSize, profileSize);
            profileRect.anchoredPosition = new Vector2(sidePadding, 0f);
            var profileImage = profile.GetComponent<Image>();
            profileImage.sprite = FindProfile(speaker);
            profileImage.preserveAspect = true;
            profileImage.raycastTarget = false;
            profileImage.color = profileImage.sprite ? Color.white : otherBubbleColor;
        }

        rect.sizeDelta = new Vector2(0f, rowHeight);

        // 이전 말풍선들은 새 말풍선 높이만큼 위로 밀려 올라간다
        var message = new Message { rect = rect, group = rowGroup, height = rowHeight, y = 0f, appear = 0f };
        messages.Add(message);
        while (messages.Count > maxMessages)
        {
            Kill(messages[0].rect.gameObject);
            messages.RemoveAt(0);
        }
        rowGroup.alpha = 0f;
        rect.anchoredPosition = new Vector2(0f, -rowHeight * 0.5f);
        message.y = -rowHeight * 0.5f;

        typingText = tmp;
        tmp.ForceMeshUpdate();
        totalChars = tmp.textInfo.characterCount;
        typedChars = 0f;
        tmp.maxVisibleCharacters = 0;
        IsTyping = true;
        waitingMessage = state == State.Opening;
        ShowIndicator = true;
        SetIndicator(false);
    }

    // 지금 말풍선 대사에서 text가 시작하는 글자 위치(0부터). 없으면 -1 (DialogueBox.FindText와 같은 방식)
    public int FindText(string text)
    {
        if (string.IsNullOrEmpty(text) || !typingText) return -1;
        return typingText.GetParsedText().IndexOf(StoryText.Format(text), System.StringComparison.Ordinal);
    }

    // 출력 중인 말풍선을 한 번에 다 보여 준다
    public void Complete()
    {
        if (!IsTyping) return;
        IsTyping = false;
        waitingMessage = false;
        if (typingText) typingText.maxVisibleCharacters = totalChars;
    }

    private Sprite FindProfile(string speaker)
    {
        foreach (var contact in contacts)
            if (contact != null && contact.profile && contact.name == speaker) return contact.profile;
        return defaultProfile;
    }

    private void ClearMessages()
    {
        foreach (var message in messages)
            if (message.rect) Kill(message.rect.gameObject);
        messages.Clear();
        typingText = null;
        IsTyping = false;
    }

    private void Update()
    {
        if (state == State.Closed) return;
        float dt = GamePause.Delta; // 일시정지 중에는 멈춘다

        if (state == State.Opening)
        {
            animTime += dt;
            if (animTime >= FullTime)
            {
                animTime = FullTime;
                state = State.Open;
                waitingMessage = false;
            }
        }
        else if (state == State.Closing)
        {
            animTime -= dt;
            if (animTime <= 0f)
            {
                HideNow();
                return;
            }
        }

        UpdateMessages(dt);
        UpdateTyping(dt);
        UpdateEffects(dt);
        Apply();
    }

    private void UpdateTyping(float dt)
    {
        if (!IsTyping || waitingMessage || !typingText) return;
        typedChars += charsPerSecond * dt;
        typingText.maxVisibleCharacters = Mathf.Min((int)typedChars, totalChars);
        if (typedChars >= totalChars) Complete();
    }

    // 말풍선을 아래에서부터 쌓고, 새 말풍선이 들어오면 부드럽게 밀어 올린다
    private void UpdateMessages(float dt)
    {
        float k = 1f - Mathf.Exp(-dt * 4.6f / slideTime); // slideTime 동안 거의 다 도착
        float y = 0f;
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var m = messages[i];
            m.y = Mathf.Lerp(m.y, y, k);
            m.appear = Mathf.MoveTowards(m.appear, 1f, dt / slideTime);
            m.rect.anchoredPosition = new Vector2(0f, m.y);
            m.group.alpha = m.appear;
            y += m.height + spacing;
        }
    }

    private void UpdateEffects(float dt)
    {
        if (scanlines)
        {
            var uv = scanlines.uvRect;
            float lines = scanlines.rectTransform.rect.height / scanlineSpacing;
            uv.height = Mathf.Max(1f, lines); // 텍스처 한 장 = 줄 하나
            uv.y = Mathf.Repeat(uv.y - scanlineSpeed * dt, 1f);
            scanlines.uvRect = uv;
        }

        bool blinkOn = ShowIndicator && !IsTyping && state == State.Open && Time.unscaledTime % blinkPeriod < blinkPeriod * 0.5f;
        if (nextIndicatorVisible != blinkOn) SetIndicator(blinkOn);

        if (state != State.Open) return;
        if (glitchLeft > 0f)
        {
            glitchLeft -= dt;
            if (glitchLeft <= 0f) ScheduleGlitch();
        }
        else if (Time.unscaledTime >= nextGlitch)
            glitchLeft = Random.Range(glitchDuration.x, Mathf.Max(glitchDuration.x, glitchDuration.y));
    }

    private bool nextIndicatorVisible;

    private void ScheduleGlitch()
    {
        glitchLeft = 0f;
        nextGlitch = Time.unscaledTime + Random.Range(glitchInterval.x, Mathf.Max(glitchInterval.x, glitchInterval.y));
    }

    // 펼침 정도·지직거림을 본체에 반영한다
    private void Apply()
    {
        if (!body) return;
        float lineT = lineTime > 0f ? Mathf.Clamp01(animTime / lineTime) : 1f;
        float expandT = expandTime > 0f ? Mathf.Clamp01((animTime - lineTime) / expandTime) : (animTime >= lineTime ? 1f : 0f);
        float sx = EaseOut(lineT);
        float sy = Mathf.Lerp(lineThickness, 1f, EaseOut(expandT));
        body.localScale = new Vector3(sx, sy, 1f);

        float alpha = 1f;
        float shift = 0f;
        if (IsAnimating && openFlicker > 0f)
        {
            // 펼치고 접는 동안 치직: 무작위로 흐려지고 옆으로 튄다
            if (Random.value < openFlicker * 0.5f) alpha = Random.Range(0.25f, 0.85f);
            if (Random.value < openFlicker * 0.3f) shift = Random.Range(-glitchShift, glitchShift) * 1.5f;
        }
        else if (glitchLeft > 0f)
        {
            alpha = Random.Range(glitchMinAlpha, 1f);
            shift = Random.Range(-glitchShift, glitchShift);
        }
        if (group) group.alpha = alpha;
        body.anchoredPosition = bodyBasePosition + new Vector2(shift, 0f);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    private void SetIndicator(bool visible)
    {
        nextIndicatorVisible = visible;
        if (nextIndicator) nextIndicator.enabled = visible;
    }
}
