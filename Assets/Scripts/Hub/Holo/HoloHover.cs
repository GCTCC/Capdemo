using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 홀로그램 허브의 호버 효과. 마우스를 올리면
//   - 살짝 커지고 (Hover Scale)
//   - 등록한 그래픽들의 색이 바뀌고 (Color Targets: 배경은 민트로, 글자는 어둡게 등)
//   - 숨겨 둔 것이 나타나고 (Show On Hover: NEW TRACK 태그, 빛 띠 등)
//   - 들어가는 순간 한 번 짧게 지직거린다 (Glitch Target이 옆으로 튐)
// 메인 메뉴 버튼은 HoloMenu가 대신 켜고 끈다 (React To Pointer 끔).
public class HoloHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.Serializable]
    public class ColorTarget
    {
        public Graphic graphic;
        [Tooltip("호버일 때 색 (평소 색은 시작할 때 지금 색을 기억한다)")]
        public Color hoverColor = Color.white;
        [System.NonSerialized] public Color normalColor;
    }

    [Tooltip("마우스를 직접 감지할지. HoloMenu가 조종하는 메뉴 버튼은 끈다.")]
    [SerializeField] private bool reactToPointer = true;

    [Tooltip("호버일 때 크기 배율. 커지는 기준점은 RectTransform의 Pivot이다.")]
    [SerializeField] private float hoverScale = 1.04f;

    [Tooltip("바뀌는 데 걸리는 시간(초)")]
    [SerializeField, Min(0f)] private float duration = 0.14f;

    [SerializeField] private ColorTarget[] colorTargets = new ColorTarget[0];

    [Tooltip("호버일 때만 보이는 묶음")]
    [SerializeField] private CanvasGroup[] showOnHover = new CanvasGroup[0];

    [Header("지직")]
    [Tooltip("지직거릴 때 옆으로 튀는 내용물. 비우면 지직거리지 않는다.")]
    [SerializeField] private RectTransform glitchTarget;
    [SerializeField, Min(0f)] private float enterGlitchTime = 0.12f;
    [SerializeField, Min(0f)] private float glitchShift = 7f;
    [Tooltip("클릭하면 지직거리기만 할지 (아직 기능이 없는 버튼용)")]
    [SerializeField] private bool glitchOnClick = false;

    public bool Highlighted { get; private set; }

    private float blend;
    private float glitchLeft;
    private Vector2 glitchBase;
    private CanvasGroup glitchGroup;
    private bool ready;

    private void Awake() => Init();

    private void Init()
    {
        if (ready) return;
        ready = true;
        foreach (var c in colorTargets)
            if (c != null && c.graphic) c.normalColor = c.graphic.color;
        if (glitchTarget)
        {
            glitchBase = glitchTarget.anchoredPosition;
            glitchGroup = glitchTarget.GetComponent<CanvasGroup>();
        }
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (reactToPointer) SetHighlighted(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (reactToPointer) SetHighlighted(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (glitchOnClick) Glitch(0.3f);
    }

    public void SetHighlighted(bool on, bool instant = false)
    {
        Init();
        if (on && !Highlighted) Glitch(enterGlitchTime);
        Highlighted = on;
        if (instant)
        {
            blend = on ? 1f : 0f;
            Apply();
        }
    }

    // 잠깐 지직거린다 (선택할 때, 아직 준비 안 된 버튼을 눌렀을 때 등)
    public void Glitch(float time)
    {
        glitchLeft = Mathf.Max(glitchLeft, time);
    }

    private void Update()
    {
        float goal = Highlighted ? 1f : 0f;
        if (!Mathf.Approximately(blend, goal))
        {
            blend = duration > 0f ? Mathf.MoveTowards(blend, goal, Time.unscaledDeltaTime / duration) : goal;
            Apply();
        }

        if (!glitchTarget) return;
        if (glitchLeft > 0f)
        {
            glitchLeft -= Time.unscaledDeltaTime;
            bool jump = Random.value < 0.6f;
            glitchTarget.anchoredPosition = glitchBase + new Vector2(jump ? Random.Range(-glitchShift, glitchShift) : 0f, 0f);
            if (glitchGroup) glitchGroup.alpha = Random.value < 0.3f ? Random.Range(0.35f, 0.8f) : 1f;
            if (glitchLeft <= 0f)
            {
                glitchTarget.anchoredPosition = glitchBase;
                if (glitchGroup) glitchGroup.alpha = 1f;
            }
        }
    }

    private void OnDisable()
    {
        if (!ready) return;
        glitchLeft = 0f;
        if (glitchTarget) glitchTarget.anchoredPosition = glitchBase;
        if (glitchGroup) glitchGroup.alpha = 1f;
    }

    private void Apply()
    {
        // 살짝 튕기듯 커진다 (끝에서 조금 넘쳤다 돌아옴)
        float e = blend < 1f ? 1f - (1f - blend) * (1f - blend) : 1f;
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, hoverScale, e);

        foreach (var c in colorTargets)
            if (c != null && c.graphic) c.graphic.color = Color.Lerp(c.normalColor, c.hoverColor, blend);
        foreach (var g in showOnHover)
            if (g) g.alpha = blend;
    }
}
