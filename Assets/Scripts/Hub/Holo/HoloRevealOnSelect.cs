using UnityEngine;

// 홀로그램 허브: 메인 메뉴에서 특정 버튼이 선택됐을 때만 펼쳐지는 패널 (예: 스토리 모드 → 스토리 진행도).
// 선택되면 옛날 TV처럼 가는 가로줄이 먼저 뻗고 → 위아래로 펼쳐지며 치직거리고,
// 선택이 다른 버튼으로 옮겨 가면 반대로 빠르게 접혀 사라진다.
// 이 컴포넌트는 펼칠 패널을 감싼 오브젝트(Pivot = 왼쪽 위)에 붙인다. 크기(scale)·투명도는 이 스크립트가 맡는다.
[RequireComponent(typeof(CanvasGroup))]
public class HoloRevealOnSelect : MonoBehaviour
{
    [Tooltip("이 버튼이 선택되면 펼친다")]
    [SerializeField] private HoloMenuItem showWhen;
    [Tooltip("메뉴 (비우면 Show When의 부모에서 찾는다)")]
    [SerializeField] private HoloMenu menu;

    [Header("시간(초)")]
    [Tooltip("가로줄이 옆으로 뻗는 시간")]
    [SerializeField, Min(0.01f)] private float lineTime = 0.12f;
    [Tooltip("위아래로 펼쳐지는 시간")]
    [SerializeField, Min(0.01f)] private float openTime = 0.16f;
    [Tooltip("접혀 사라지는 시간")]
    [SerializeField, Min(0.01f)] private float closeTime = 0.12f;
    [Tooltip("처음 화면이 켜질 때 이만큼 기다린 뒤부터 펼칠 수 있다 (등장 연출과 겹치지 않게)")]
    [SerializeField, Min(0f)] private float startDelay = 0.5f;

    private CanvasGroup group;
    private float progress;  // 0 = 접힘, 1 = 다 펼쳐짐 (가로줄 → 펼침 순서)
    private float age;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (!menu && showWhen) menu = showWhen.GetComponentInParent<HoloMenu>();
        Apply(0f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        bool open = age >= startDelay && menu && showWhen && menu.Selected == showWhen;

        float total = lineTime + openTime;
        if (open) progress = Mathf.Min(1f, progress + dt / total);
        else progress = Mathf.Max(0f, progress - dt / closeTime);
        Apply(progress);
    }

    private void Apply(float p)
    {
        float total = lineTime + openTime;
        float split = lineTime / total;
        float sx, sy;
        if (p <= split)
        {
            // 1) 가는 가로줄이 왼쪽에서 오른쪽으로 뻗는다
            float k = p / Mathf.Max(0.0001f, split);
            sx = 1f - (1f - k) * (1f - k);
            sy = 0.03f;
        }
        else
        {
            // 2) 위아래로 펼쳐진다 (살짝 넘쳤다 돌아오며, 가끔 치직 덜 열림)
            float k = (p - split) / Mathf.Max(0.0001f, 1f - split);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            if (k < 1f && Random.value < 0.18f) e *= Random.Range(0.7f, 1f);
            sx = 1f;
            sy = Mathf.Lerp(0.03f, 1f, e);
        }
        transform.localScale = new Vector3(sx, sy, 1f);

        // 펼쳐지는 동안은 깜빡이고, 다 펼쳐지면 또렷하게
        if (p <= 0f) group.alpha = 0f;
        else if (p >= 1f) group.alpha = 1f;
        else group.alpha = Random.value < 0.25f ? 0.35f : 1f;

        bool interactive = p >= 1f;
        group.blocksRaycasts = interactive;
        group.interactable = interactive;
    }
}
