using UnityEngine;

// "아무 키나 눌러 시작" 문구를 천천히 깜빡이게 한다.
// CanvasGroup 알파를 사인 곡선으로 부드럽게 오르내린다.
[RequireComponent(typeof(CanvasGroup))]
public class PressAnyKeyBlink : MonoBehaviour
{
    [Tooltip("한 번 깜빡이는 데 걸리는 시간(초). 클수록 느리다.")]
    [SerializeField, Min(0.1f)] private float blinkPeriod = 3f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

    private CanvasGroup group;
    private float startTime;

    private void Awake() => group = GetComponent<CanvasGroup>();

    private void OnEnable()
    {
        startTime = Time.unscaledTime;
        if (group) group.alpha = maxAlpha;
    }

    private void Update()
    {
        float t = (Time.unscaledTime - startTime) / blinkPeriod;
        // 1에서 시작해 0까지 내려갔다가 다시 1로 돌아오는 곡선
        float wave = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
        group.alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
    }
}
