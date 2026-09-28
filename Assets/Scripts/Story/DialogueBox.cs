using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("초당 출력할 글자 수")]
    [SerializeField, Min(1f)] private float charsPerSecond = 30f;
    [Tooltip("▼가 한 번 켜졌다 꺼지는 시간(초)")]
    [SerializeField, Min(0.05f)] private float blinkPeriod = 0.8f;

    private float typedChars;
    private int totalChars;

    public bool IsTyping { get; private set; }

    private void Awake()
    {
        SetStanding(null);
        Clear();
    }

    public void Show(StoryLine line)
    {
        string speaker = StoryText.Format(line.Speaker);
        if (nameText) nameText.text = speaker;
        if (namePlate) namePlate.SetActive(!string.IsNullOrEmpty(speaker));

        if (line.HideStanding) SetStanding(null);
        else if (line.Standing) SetStanding(line.Standing);

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
