using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 대사창: 화자 이름·스탠딩 일러스트를 보여 주고, 대사를 한 글자씩 출력한다.
// 출력이 끝나면 ▼ 표시를 깜빡인다. 입력 처리는 StoryManager가 한다.
public class DialogueBox : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [Tooltip("화자 이름이 없을 때 숨길 이름표 오브젝트")]
    [SerializeField] private GameObject namePlate;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image standing;
    [Tooltip("출력이 끝나면 깜빡일 ▼ 표시")]
    [SerializeField] private Graphic nextIndicator;

    [Header("화자 옆 이름표 (맵 대화용)")]
    [Tooltip("켜면 화자 캐릭터(SpeakerTag)가 맵에 있을 때 그 머리 옆에 이름표를 띄우고, 대화창 왼쪽 위 이름표는 숨긴다.\n" +
             "끄면 예전처럼 대화창 왼쪽 위 이름표만 쓴다. 맵에 없는 화자(내레이션 등)는 켜 있어도 왼쪽 위 이름표로 나온다.")]
    [SerializeField] private bool nameBesideSpeaker;
    [Tooltip("화자 옆 이름표 원본. 맵 대화에서는 꺼 둔 채 참가자 수만큼 복제해서 쓴다")]
    [SerializeField] private SpeakerNameTag speakerNameTag;
    [Tooltip("이름표 버전일 때 테두리 패널 대신 켤 하단 꾸밈(그라데이션·선·파형). 비어 있으면 아래 배치 전환도 하지 않는다")]
    [SerializeField] private GameObject besideDecor;
    [Tooltip("이름표 버전 하단 선 가운데의 화자 이름·내레이션 파형. 있으면 왼쪽 위 금테 이름표 대신 이걸 쓴다")]
    [SerializeField] private DialogueLineName lineName;
    [Tooltip("패널 버전(Name Beside Speaker 끔)의 대사·▼ 배치. 대사 글씨 위치는 여기서 고친다")]
    [SerializeField] private BoxLayout panelLayout = new BoxLayout();
    [Tooltip("이름표 버전(Name Beside Speaker 켬)의 대사·▼ 배치. 대사 글씨 위치는 여기서 고친다")]
    [SerializeField] private BoxLayout besideLayout = new BoxLayout();

    [Header("소개 카드")]
    [Tooltip("대사 줄의 Intro Card를 띄울 카드. 비우면 이 아래에서 찾는다 (Tools > CapDemo > 스토리 > 소개 카드 넣기)")]
    [SerializeField] private IntroCard introCard;

    // 대사 글씨 영역과 ▼ 자리. BodyText·NextIndicator의 Rect Transform에 그대로 들어간다
    [System.Serializable]
    public class BoxLayout
    {
        [Tooltip("대사 글씨 영역의 왼쪽·아래 여백 (대화창 테두리 기준)")]
        public Vector2 bodyOffsetMin = new Vector2(70f, 50f);
        [Tooltip("대사 글씨 영역의 오른쪽·위 여백. 안쪽으로 들어오려면 음수")]
        public Vector2 bodyOffsetMax = new Vector2(-90f, -62f);
        public TextAlignmentOptions bodyAlignment = TextAlignmentOptions.TopLeft;
        [Tooltip("▼ 기준점. (0,0) 대화창 왼쪽 아래 ~ (1,1) 오른쪽 위")]
        public Vector2 indicatorAnchor = new Vector2(1f, 0f);
        [Tooltip("기준점에서 ▼까지 거리")]
        public Vector2 indicatorPosition = new Vector2(-60f, 42f);
        [Min(1f)] public float indicatorSize = 28f;
    }

    [Tooltip("대화창·이름표 이미지와 글자 색을 정하는 스킨 에셋 (Create > CapDemo > Story Skin). 비워 두면 씬에 있는 모양 그대로")]
    [SerializeField] private StorySkin skin;

    [Tooltip("초당 출력할 글자 수")]
    [SerializeField, Min(1f)] private float charsPerSecond = 30f;
    [Tooltip("▼가 한 번 켜졌다 꺼지는 시간(초)")]
    [SerializeField, Min(0.05f)] private float blinkPeriod = 0.8f;
    [Tooltip("홀로그램(폰) 대화 줄에서 대화창이 사라지고 나타나는 시간(초)")]
    [SerializeField, Min(0f)] private float holoFadeTime = 0.15f;

    [Header("스탠딩 위치 프리셋 (화면 왼쪽 끝 0 ~ 오른쪽 끝 1)")]
    [SerializeField, Range(0f, 1f)] private float leftX = 0.22f;
    [SerializeField, Range(0f, 1f)] private float centerX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float rightX = 0.78f;

    private float typedChars;
    private int totalChars;

    // 맵 대화(BeginConversation~EndConversation) 동안 참가자마다 띄운 이름표. 화자 이름 → 이름표
    private readonly Dictionary<string, SpeakerNameTag> conversationTags = new Dictionary<string, SpeakerNameTag>();
    private bool inConversation;
    private bool endingConversation;

    // 홀로그램(폰) 대화 중이면 true. 머리 옆 이름표·왼쪽 위 이름표를 숨긴다 (DemoDialogue가 정한다)
    private bool holoMode;
    // 홀로그램 말풍선 줄에서는 대화창 전체를 투명하게 숨긴다
    private CanvasGroup boxGroup;
    private float boxAlphaTarget = 1f;

    // 씬에 배치한 스탠딩 자리. 위치 프리셋은 이 높이·크기를 기준으로 옮긴다
    private bool standingBaseCaptured;
    private Vector2 standingBaseAnchor;
    private Vector2 standingBasePosition;
    private Vector3 standingBaseScale;

    public bool IsTyping { get; private set; }
    // EndConversation 뒤 참가자 이름표가 아직 접히는 중이면 true. 대화창은 이게 false가 된 뒤에 끈다
    public bool IsFolding
    {
        get
        {
            if (!endingConversation) return false;
            if (introCard && introCard.IsFolding) return true;
            foreach (var tag in conversationTags.Values)
                if (tag && tag.IsFolding) return true;
            return false;
        }
    }

    // 지금 대화 중인 NPC. 같은 이름의 캐릭터가 여럿일 때 이 캐릭터 옆에 이름표를 띄운다 (DemoDialogue가 정한다)
    public Transform PreferredSpeaker { get; set; }
    public StorySkin Skin => skin;

    // 지금까지 화면에 나온 글자 수 (띄어쓰기 포함)
    public int VisibleCharacters => IsTyping ? Mathf.Min((int)typedChars, totalChars) : totalChars;

    // 지금 대사에서 text가 시작하는 글자 위치(0부터). 없으면 -1
    public int FindText(string text)
    {
        if (string.IsNullOrEmpty(text) || !bodyText) return -1;
        return bodyText.GetParsedText().IndexOf(StoryText.Format(text), System.StringComparison.Ordinal);
    }

    private void Awake()
    {
        FindIntroCard();
        if (introCard) introCard.Hide();
        ApplySkin();
        ApplyStyle();
        ResetStanding();
        Clear();
    }

    public void Show(StoryLine line)
    {
        string speaker = SpeakerTag.FormatName(line.Speaker);
        if (nameText) nameText.text = speaker;
        if (inConversation)
        {
            // 맵 대화: 이름표는 모두 떠 있고, 말하는 사람만 밝게. 맵에 없는 화자(내레이션 등)는 왼쪽 아래 이름표로
            SpeakerNameTag current = null;
            if (!string.IsNullOrEmpty(speaker)) conversationTags.TryGetValue(SpeakerKey(line.Speaker, speaker), out current);
            foreach (var tag in conversationTags.Values)
                if (tag) tag.SetDimmed(tag != current);
            ShowSpeakerOnBox(speaker, !current);
        }
        else
        {
            var speakerTag = !holoMode && nameBesideSpeaker && speakerNameTag ? SpeakerTag.FindFor(line.Speaker, PreferredSpeaker) : null;
            if (speakerTag) speakerNameTag.Show(speakerTag, speaker);
            else if (speakerNameTag) speakerNameTag.Hide();
            ShowSpeakerOnBox(speaker, !speakerTag);
        }

        ApplyStanding(line);
        ShowIntroCard(line);

        bodyText.text = StoryText.Format(line.Text);
        bodyText.ForceMeshUpdate();
        totalChars = bodyText.textInfo.characterCount;
        typedChars = 0f;
        bodyText.maxVisibleCharacters = 0;
        IsTyping = true;
        SetIndicator(false);
    }

    // 출력 중인 대사를 한 번에 다 보여 준다
    public void Complete()
    {
        if (!IsTyping) return;
        IsTyping = false;
        bodyText.maxVisibleCharacters = totalChars;
    }

    // 컷이 바뀌는 동안 대사와 이름을 비운다 (스탠딩은 유지)
    public void Clear()
    {
        IsTyping = false;
        if (bodyText) bodyText.text = "";
        if (nameText) nameText.text = "";
        if (namePlate) namePlate.SetActive(false);
        if (UsesLineName) lineName.ShowNothing();
        if (speakerNameTag && !inConversation) speakerNameTag.Hide();
        FoldIntroCard();
        SetIndicator(false);
    }

    public IntroCard IntroCard
    {
        get
        {
            FindIntroCard();
            return introCard;
        }
    }

    private void FindIntroCard()
    {
        if (!introCard) introCard = GetComponentInChildren<IntroCard>(true);
    }

    // 이 줄에 소개 카드 내용이 있으면 펼치고, 없으면 떠 있던 카드를 접는다.
    // 맵 대화는 화자 머리를 따라가고(맵에 없는 화자면 줄의 Position 자리), 챕터는 줄의 Position 자리에 띄운다.
    // 홀로그램 중에는 띄우지 않는다.
    private void ShowIntroCard(StoryLine line)
    {
        FindIntroCard();
        if (!introCard) return;
        if (!line.HasIntroCard || holoMode)
        {
            introCard.Fold();
            return;
        }
        var info = line.IntroCard;
        string title = SpeakerTag.FormatName(string.IsNullOrEmpty(info.displayName) ? line.Speaker : info.displayName);
        string profile = StoryText.Format(info.profile);
        var speaker = inConversation ? SpeakerTag.FindFor(line.Speaker, PreferredSpeaker) : null;
        if (speaker) introCard.ShowOn(title, profile, speaker, info);
        else introCard.ShowAt(title, profile, info);
    }

    // 떠 있는 소개 카드를 접는다 (장면이 끝날 때 등)
    public void FoldIntroCard()
    {
        if (introCard) introCard.Fold();
    }

    // 새 스타일(이름표 버전 + 선 가운데 이름)을 쓰는지. 이때는 왼쪽 위 금테 이름표를 절대 띄우지 않는다
    private bool UsesLineName => lineName && besideDecor && nameBesideSpeaker;

    // 머리 옆 이름표가 없는 화자 이름을 대화창에 보인다.
    // 새 스타일: 선 가운데 이름, 내레이션(화자 없음)이면 파형만. 예전 패널 스타일: 왼쪽 위 금테 이름표.
    // 홀로그램 중 독백은 어느 쪽이든 이름 없이.
    private void ShowSpeakerOnBox(string speaker, bool noHeadTag)
    {
        bool named = !string.IsNullOrEmpty(speaker);
        if (!UsesLineName)
        {
            if (namePlate) namePlate.SetActive(!holoMode && noHeadTag && named);
            return;
        }
        if (namePlate) namePlate.SetActive(false);
        if (holoMode) lineName.ShowNothing();
        else if (!named) lineName.ShowNarration();
        else if (noHeadTag) lineName.ShowName(speaker);
        else lineName.ShowNothing();
    }

    // 맵 대화 시작: 대사에 나오는 화자 중 맵에 있는 사람마다 머리 옆 이름표를 한꺼번에 펼친다.
    // Name Beside Speaker가 꺼져 있거나 이름표 원본이 없으면 아무것도 하지 않는다(예전 방식 그대로).
    // PreferredSpeaker를 먼저 정한 뒤에 부른다(같은 이름의 캐릭터가 여럿이면 그 캐릭터를 고른다).
    public void BeginConversation(IReadOnlyList<StoryLine> lines)
    {
        DestroyConversationTags();
        if (!nameBesideSpeaker || !speakerNameTag || lines == null) return;

        speakerNameTag.Hide(); // 원본은 복제용으로 꺼 둔다
        var found = new HashSet<SpeakerTag>();
        foreach (var line in lines)
        {
            if (line == null || line.HoloMessage) continue; // 홀로그램 말풍선에만 나오는 사람은 머리 옆 이름표를 띄우지 않는다
            string speaker = SpeakerTag.FormatName(line.Speaker);
            string key = SpeakerKey(line.Speaker, speaker);
            if (string.IsNullOrEmpty(speaker) || conversationTags.ContainsKey(key)) continue;
            var target = SpeakerTag.FindFor(line.Speaker, PreferredSpeaker);
            if (!target || !found.Add(target)) continue;

            var tag = Instantiate(speakerNameTag, speakerNameTag.transform.parent);
            tag.name = $"{speakerNameTag.name} ({speaker})";
            tag.Open(target, speaker);
            tag.SetDimmed(true); // 첫 대사의 Show에서 화자만 밝아진다
            conversationTags[key] = tag;
        }
        inConversation = true;
    }

    // 참가자 이름표를 찾는 열쇠. 주인공 줄은 이름과 상관없이 하나로 묶는다(이름 없는 "???" 다른 인물과 섞이지 않게)
    private const string PlayerKey = "{player}";
    private static string SpeakerKey(string rawSpeaker, string formatted) =>
        SpeakerTag.IsPlayerLine(rawSpeaker) ? PlayerKey : formatted;

    // 맵 대화 끝: 이름표를 모두 거꾸로 접는다. IsFolding이 false가 되면 대화창을 끄면 된다
    public void EndConversation()
    {
        if (!inConversation) return;
        endingConversation = true;
        FoldIntroCard();
        foreach (var tag in conversationTags.Values)
            if (tag) tag.Fold();
    }

    private void DestroyConversationTags()
    {
        foreach (var tag in conversationTags.Values)
        {
            if (!tag) continue;
            if (Application.isPlaying) Destroy(tag.gameObject);
            else DestroyImmediate(tag.gameObject);
        }
        conversationTags.Clear();
        inConversation = false;
        endingConversation = false;
    }

    // 홀로그램(폰) 대화 시작·끝. 켜져 있는 동안 이름표들을 투명하게 숨긴다(접지는 않는다)
    public void SetHoloMode(bool on)
    {
        holoMode = on;
        foreach (var tag in conversationTags.Values)
            if (tag) tag.SetHidden(on);
        if (!on) return;
        if (namePlate) namePlate.SetActive(false);
        if (UsesLineName) lineName.ShowNothing();
        if (speakerNameTag && !inConversation) speakerNameTag.Hide();
    }

    // 홀로그램 말풍선 줄에서 대화창을 숨긴다(서서히 투명하게). false면 다시 보인다
    public void SetBoxHidden(bool hidden)
    {
        if (!boxGroup)
        {
            boxGroup = GetComponent<CanvasGroup>();
            if (!boxGroup) boxGroup = gameObject.AddComponent<CanvasGroup>();
        }
        boxAlphaTarget = hidden ? 0f : 1f;
    }

    // 대화창이 꺼지면 대화 중에 만든 이름표도 정리하고 홀로그램 상태도 되돌린다
    private void OnDisable()
    {
        DestroyConversationTags();
        holoMode = false;
        boxAlphaTarget = 1f;
        if (boxGroup) boxGroup.alpha = 1f;
    }

    // 이 줄의 스탠딩 그림·위치만 반영한다 (중간 컷부터 재생할 때 앞 줄들을 빠르게 따라가는 데도 쓴다)
    public void ApplyStanding(StoryLine line)
    {
        if (line.HideStanding) SetStanding(null);
        else if (line.Standing) SetStanding(line.Standing);
        ApplyPose(line.StandingPose);
    }

    // 스탠딩을 숨기고 씬에 배치한 자리로 되돌린다
    public void ResetStanding()
    {
        SetStanding(null);
        if (!standing || !standingBaseCaptured) return;
        var rect = standing.rectTransform;
        rect.anchorMin = rect.anchorMax = standingBaseAnchor;
        rect.anchoredPosition = standingBasePosition;
        rect.localScale = standingBaseScale;
    }

    // 에디터 미리보기가 끝난 뒤, 다음 기준 자리를 씬에서 새로 읽게 한다
    public void ForgetStandingBase() => standingBaseCaptured = false;

    // 에디터 미리보기용: 대사를 출력 없이 다 보여 주고 ▼를 켠다
    public void ShowInstant(StoryLine line)
    {
        Show(line);
        Complete();
        SetIndicator(true);
        if (introCard) introCard.Snap();
    }

    private void Update()
    {
        if (endingConversation && !IsFolding) DestroyConversationTags();
        if (boxGroup && boxGroup.alpha != boxAlphaTarget)
            boxGroup.alpha = holoFadeTime > 0f
                ? Mathf.MoveTowards(boxGroup.alpha, boxAlphaTarget, Time.unscaledDeltaTime / holoFadeTime)
                : boxAlphaTarget;

        if (IsTyping)
        {
            typedChars += charsPerSecond * GamePause.Delta; // 일시정지 중에는 멈춘다
            bodyText.maxVisibleCharacters = Mathf.Min((int)typedChars, totalChars);
            if (typedChars >= totalChars) Complete();
            return;
        }

        if (bodyText && !string.IsNullOrEmpty(bodyText.text))
            SetIndicator(Time.unscaledTime % blinkPeriod < blinkPeriod * 0.5f);
    }

    private void ApplyPose(StandingPose pose)
    {
        if (!standing || pose.position == StandingPosition.Keep) return;
        CaptureStandingBase();

        float x = pose.position switch
        {
            StandingPosition.Left => leftX,
            StandingPosition.Right => rightX,
            _ => centerX,
        };
        var rect = standing.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(x, standingBaseAnchor.y);
        rect.anchoredPosition = new Vector2(0f, standingBasePosition.y) + pose.offset;
        float scale = pose.Scale;
        rect.localScale = new Vector3(standingBaseScale.x * scale * (pose.flip ? -1f : 1f), standingBaseScale.y * scale, 1f);
    }

    private void CaptureStandingBase()
    {
        if (standingBaseCaptured) return;
        standingBaseCaptured = true;
        var rect = standing.rectTransform;
        standingBaseAnchor = rect.anchorMin;
        standingBasePosition = rect.anchoredPosition;
        standingBaseScale = rect.localScale;
    }

    // 스킨 에셋의 그림·색·폰트를 대화창에 입힌다. 스킨에서 비워 둔 그림·폰트는 건드리지 않는다
    public void ApplySkin()
    {
        if (!skin) return;
        ApplyImage(GetComponent<Image>(), skin.BoxSprite, skin.BoxColor);
        if (namePlate) ApplyImage(namePlate.GetComponent<Image>(), skin.PlateSprite, skin.PlateColor);
        ApplyText(bodyText, skin.BodyFont, skin.BodyColor);
        ApplyText(nameText, skin.NameFont, skin.NameColor);
        if (nextIndicator && nextIndicator.color != skin.IndicatorColor)
        {
            nextIndicator.color = skin.IndicatorColor;
            MarkDirty(nextIndicator);
        }
    }

    // Name Beside Speaker에 맞춰 패널 버전/이름표 버전 겉모양과 배치를 고른다 (Beside Decor가 있을 때만)
    public void ApplyStyle()
    {
        if (!besideDecor) return;
        bool beside = nameBesideSpeaker;
        if (besideDecor.activeSelf != beside)
        {
            besideDecor.SetActive(beside);
            MarkDirty(besideDecor);
        }
        var panel = GetComponent<Image>();
        if (panel && panel.enabled == beside)
        {
            panel.enabled = !beside;
            MarkDirty(panel);
        }

        var layout = beside ? besideLayout : panelLayout;
        if (bodyText)
        {
            var rect = bodyText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = layout.bodyOffsetMin;
            rect.offsetMax = layout.bodyOffsetMax;
            bodyText.alignment = layout.bodyAlignment;
            MarkDirty(rect);
            MarkDirty(bodyText);
        }
        if (nextIndicator)
        {
            var rect = nextIndicator.rectTransform;
            rect.anchorMin = rect.anchorMax = layout.indicatorAnchor;
            rect.anchoredPosition = layout.indicatorPosition;
            if (nextIndicator is TMP_Text arrow) arrow.fontSize = layout.indicatorSize;
            MarkDirty(rect);
            MarkDirty(nextIndicator);
        }
    }

    private static void ApplyImage(Image image, Sprite sprite, Color color)
    {
        if (!image || !sprite) return;
        var type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        if (image.sprite == sprite && image.color == color && image.type == type) return;
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        MarkDirty(image);
    }

    private static void ApplyText(TMP_Text text, TMP_FontAsset font, Color color)
    {
        if (!text) return;
        bool changed = false;
        if (font && text.font != font)
        {
            text.font = font;
            changed = true;
        }
        if (text.color != color)
        {
            text.color = color;
            changed = true;
        }
        if (changed) MarkDirty(text);
    }

    // 에디터에서 바꾼 값이 씬 저장에 포함되도록 표시한다
    private static void MarkDirty(Object target)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) EditorUtility.SetDirty(target);
#endif
    }

#if UNITY_EDITOR
    // 인스펙터에서 Skin 칸을 바꾸면 바로 반영한다
    private void OnValidate()
    {
        EditorApplication.delayCall += () =>
        {
            if (!this) return;
            ApplySkin();
            ApplyStyle();
        };
    }
#endif

    private void SetStanding(Sprite sprite)
    {
        if (!standing) return;
        standing.sprite = sprite;
        standing.enabled = sprite;
    }

    private void SetIndicator(bool visible)
    {
        if (nextIndicator) nextIndicator.enabled = visible;
    }
}
