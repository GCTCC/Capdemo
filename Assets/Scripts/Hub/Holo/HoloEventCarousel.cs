using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 오른쪽 아래 이벤트 카드. 몇 초마다 지직거리며 다음 소식으로 넘어간다 (아래 점 표시도 같이 바뀜).
// 마우스를 올려 두면 넘어가지 않고, 클릭하면 바로 다음 소식으로 넘어간다. 내용은 모두 가짜 값.
public class HoloEventCarousel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.Serializable]
    public class Entry
    {
        public string kind = "EVENT  ·  LIVE TONIGHT";
        public string title = "폐공장 라이브 나이트";
        public string sub = "한정 곡 3종 · 보상: 무대 의상 「녹슨 별」";
        public string badge = "D-3";
    }

    [SerializeField] private Entry[] entries = new Entry[0];
    [SerializeField] private TMP_Text kindText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMP_Text badgeText;
    [Tooltip("지직거릴 내용물 묶음")]
    [SerializeField] private RectTransform content;
    [SerializeField] private CanvasGroup contentGroup;
    [Tooltip("아래 점 표시 (entries와 같은 개수)")]
    [SerializeField] private Image[] dots = new Image[0];
    [SerializeField] private float interval = 4.5f;
    [SerializeField] private float glitchTime = 0.22f;

    private int index;
    private float timer;
    private float glitchLeft;
    private bool swapped;
    private bool paused;
    private Vector2 contentBase;

    private void Start()
    {
        if (content) contentBase = content.anchoredPosition;
        Show(0);
    }

    public void OnPointerEnter(PointerEventData eventData) => paused = true;
    public void OnPointerExit(PointerEventData eventData) => paused = false;
    public void OnPointerClick(PointerEventData eventData) => Next();

    private void Next()
    {
        if (entries.Length < 2 || glitchLeft > 0f) return;
        glitchLeft = glitchTime;
        swapped = false;
        timer = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!paused && glitchLeft <= 0f)
        {
            timer += dt;
            if (timer >= interval) Next();
        }

        // 지직거리는 동안 반쯤 지나면 내용을 바꾼다
        if (glitchLeft > 0f)
        {
            glitchLeft -= dt;
            if (!swapped && glitchLeft <= glitchTime * 0.5f)
            {
                swapped = true;
                Show((index + 1) % entries.Length);
            }
            if (content) content.anchoredPosition = contentBase + new Vector2(Random.Range(-9f, 9f), 0f);
            if (contentGroup) contentGroup.alpha = Random.value < 0.5f ? Random.Range(0.1f, 0.6f) : 1f;
            if (glitchLeft <= 0f)
            {
                if (content) content.anchoredPosition = contentBase;
                if (contentGroup) contentGroup.alpha = 1f;
            }
        }

        // 선택된 점은 길고 빨갛게
        for (int i = 0; i < dots.Length; i++)
        {
            if (!dots[i]) continue;
            var rt = dots[i].rectTransform;
            float goal = i == index ? 32f : 16f;
            rt.sizeDelta = new Vector2(Mathf.Lerp(rt.sizeDelta.x, goal, 1f - Mathf.Exp(-10f * dt)), rt.sizeDelta.y);
            dots[i].color = Color.Lerp(dots[i].color, i == index ? HoloPalette.Red : HoloPalette.Dim, 1f - Mathf.Exp(-10f * dt));
        }
    }

    private void Show(int i)
    {
        if (entries.Length == 0) return;
        index = i;
        var e = entries[i];
        if (kindText) kindText.text = e.kind;
        if (titleText) titleText.text = e.title;
        if (subText) subText.text = e.sub;
        if (badgeText) badgeText.text = e.badge;
    }
}
