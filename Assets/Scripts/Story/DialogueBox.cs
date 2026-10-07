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

    [Tooltip("대화창·이름표 이미지와 글자 색을 정하는 스킨 에셋 (Create > CapDemo > Story Skin). 비워 두면 씬에 있는 모양 그대로")]
    [SerializeField] private StorySkin skin;

    [Tooltip("초당 출력할 글자 수")]
    [SerializeField, Min(1f)] private float charsPerSecond = 30f;
    [Tooltip("▼가 한 번 켜졌다 꺼지는 시간(초)")]
    [SerializeField, Min(0.05f)] private float blinkPeriod = 0.8f;

    [Header("스탠딩 위치 프리셋 (화면 왼쪽 끝 0 ~ 오른쪽 끝 1)")]
    [SerializeField, Range(0f, 1f)] private float leftX = 0.22f;
    [SerializeField, Range(0f, 1f)] private float centerX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float rightX = 0.78f;

    private float typedChars;
    private int totalChars;

    // 씬에 배치한 스탠딩 자리. 위치 프리셋은 이 높이·크기를 기준으로 옮긴다
    private bool standingBaseCaptured;
    private Vector2 standingBaseAnchor;
    private Vector2 standingBasePosition;
    private Vector3 standingBaseScale;

    public bool IsTyping { get; private set; }
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
        ApplySkin();
        ResetStanding();
        Clear();
    }

    public void Show(StoryLine line)
    {
        string speaker = StoryText.Format(line.Speaker);
        if (nameText) nameText.text = speaker;
        if (namePlate) namePlate.SetActive(!string.IsNullOrEmpty(speaker));

        ApplyStanding(line);

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
        SetIndicator(false);
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
    }

    private void Update()
    {
        if (IsTyping)
        {
            typedChars += charsPerSecond * Time.unscaledDeltaTime;
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
            if (this) ApplySkin();
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
