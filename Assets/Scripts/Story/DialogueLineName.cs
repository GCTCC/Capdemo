using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대화창 하단(이름표 버전) 위쪽 가는 선 가운데에 화자 이름을 띄운다. DialogueBox가 대사마다 부른다.
// - 이름이 있으면: 선이 가운데에서 양옆으로 갈라지며 틈이 생기고(양 끝에 작은 마름모), 그 틈에 이름이 나타난다.
// - 내레이션(화자 없음)이면: 선은 이어진 채 가운데 파형 아이콘만 보인다.
// - 둘 다 아니면(머리 옆 이름표로 보이는 화자, 홀로그램 중 독백 등): 이어진 선만.
// Tools > CapDemo > 맵 > 화자 옆 이름표 이미지 교체 / 스토리 > 챕터 대화창 새 스타일 적용 메뉴가 만들어 붙인다.
public class DialogueLineName : MonoBehaviour
{
    [Tooltip("선 왼쪽 절반 (Filled, 왼쪽부터 채움)")]
    [SerializeField] private Image lineLeft;
    [Tooltip("선 오른쪽 절반 (Filled, 오른쪽부터 채움). 왼쪽 선과 같은 자리·크기")]
    [SerializeField] private Image lineRight;
    [Tooltip("틈 왼쪽 끝 마름모")]
    [SerializeField] private RectTransform capLeft;
    [Tooltip("틈 오른쪽 끝 마름모")]
    [SerializeField] private RectTransform capRight;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("내레이션일 때만 보이는 파형 아이콘")]
    [SerializeField] private Graphic wave;

    [Tooltip("이름 양옆으로 비워 둘 틈")]
    [SerializeField, Min(0f)] private float padding = 22f;
    [Tooltip("틈의 최대 너비 (이름이 길어도 이보다 넓어지지 않는다)")]
    [SerializeField, Min(0f)] private float maxGap = 520f;
    [Tooltip("틈이 열리고 닫히는 시간(초). 이름·파형이 나타나고 사라지는 시간도 같다")]
    [SerializeField, Min(0f)] private float animTime = 0.25f;

    private enum Mode { Nothing, Narration, Name }
    private Mode mode;
    private string targetName = "";

    private float gap;          // 지금 틈 너비
    private float gapFrom;      // 이번 변화가 시작될 때 틈 너비
    private float gapTo;
    private float gapTime;      // 이번 변화가 시작된 뒤 지난 시간
    private float nameAlpha;
    private float waveAlpha;
    private float waveBaseAlpha = -1f;

    public void ShowName(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName))
        {
            ShowNothing();
            return;
        }
        if (mode == Mode.Name && targetName == speakerName) return;
        mode = Mode.Name;
        targetName = speakerName;
        // 다른 이름으로 바뀌면 글자를 바로 바꾸고 다시 서서히 나타나게 한다(틈은 새 이름 길이로 늘거나 준다)
        if (nameText) nameText.text = speakerName;
        nameAlpha = 0f;
        StartGap(NameGap(speakerName));
    }

    public void ShowNarration()
    {
        if (mode == Mode.Narration) return;
        mode = Mode.Narration;
        StartGap(0f);
    }

    public void ShowNothing()
    {
        if (mode == Mode.Nothing) return;
        mode = Mode.Nothing;
        StartGap(0f);
    }

    // 꺼졌다 켜질 때(대화창을 새로 열 때)는 애니메이션 없이 닫힌 선에서 시작한다
    private void OnDisable()
    {
        mode = Mode.Nothing;
        targetName = "";
        gap = gapFrom = gapTo = 0f;
        gapTime = 0f;
        nameAlpha = 0f;
        waveAlpha = 0f;
        Apply();
    }

    private void StartGap(float to)
    {
        gapFrom = gap;
        gapTo = to;
        gapTime = 0f;
    }

    private float NameGap(string speakerName)
    {
        float width = nameText ? nameText.GetPreferredValues(speakerName).x : 0f;
        return Mathf.Min(width + padding * 2f, maxGap);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float step = animTime > 0f ? dt / animTime : 1f;

        gapTime += dt;
        float t = animTime > 0f ? Mathf.Clamp01(gapTime / animTime) : 1f;
        gap = Mathf.Lerp(gapFrom, gapTo, 1f - (1f - t) * (1f - t) * (1f - t));

        // 이름은 틈이 반쯤 열린 뒤부터 나타나고, 닫힐 때는 먼저(두 배 빠르게) 사라진다
        bool nameOn = mode == Mode.Name && (gapTo <= 0f || gap >= gapTo * 0.5f);
        nameAlpha = Mathf.MoveTowards(nameAlpha, nameOn ? 1f : 0f, nameOn ? step : step * 2f);
        waveAlpha = Mathf.MoveTowards(waveAlpha, mode == Mode.Narration ? 1f : 0f, step);
        Apply();
    }

    private void Apply()
    {
        float width = lineLeft ? lineLeft.rectTransform.rect.width : 0f;
        float half = width > 0f ? Mathf.Clamp01((width - gap) * 0.5f / width) : 0.5f;
        if (lineLeft) lineLeft.fillAmount = half;
        if (lineRight) lineRight.fillAmount = half;

        // 마름모: 틈 양 끝. 틈이 아주 좁을 때는 작게
        float capScale = Mathf.Clamp01(gap / 24f);
        PlaceCap(capLeft, -gap * 0.5f, capScale);
        PlaceCap(capRight, gap * 0.5f, capScale);

        if (nameText) nameText.alpha = nameAlpha;
        if (wave)
        {
            if (waveBaseAlpha < 0f) waveBaseAlpha = wave.color.a;
            var c = wave.color;
            c.a = waveBaseAlpha * waveAlpha;
            wave.color = c;
        }
    }

    private static void PlaceCap(RectTransform cap, float x, float scale)
    {
        if (!cap) return;
        cap.anchoredPosition = new Vector2(x, cap.anchoredPosition.y);
        cap.localScale = new Vector3(scale, scale, 1f);
    }
}
