using System.Collections;
using UnityEngine;

// 허브가 켜질 때 이 묶음이 옆에서 미끄러져 들어오며 지직거리고 나타난다.
// HoloHubIntro가 씬 시작 때 모든 HoloEntrance를 순서(Delay)대로 재생한다.
[RequireComponent(typeof(CanvasGroup))]
public class HoloEntrance : MonoBehaviour
{
    [Tooltip("허브가 켜지고 몇 초 뒤에 나타날지")]
    [SerializeField, Min(0f)] private float delay = 0f;
    [Tooltip("어디서부터 미끄러져 올지 (원래 자리 기준, 픽셀)")]
    [SerializeField] private Vector2 fromOffset = new Vector2(-40f, 0f);
    [SerializeField, Min(0.01f)] private float duration = 0.45f;

    private CanvasGroup group;
    private RectTransform rect;
    private Vector2 basePos;
    private bool ready;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = (RectTransform)transform;
        basePos = rect.anchoredPosition;
        ready = true;
        group.alpha = 0f; // 재생 전에는 숨겨 둔다
    }

    public void Play(float extraDelay)
    {
        if (!ready) return;
        StopAllCoroutines();
        StartCoroutine(Routine(delay + extraDelay));
    }

    private IEnumerator Routine(float wait)
    {
        group.alpha = 0f;
        rect.anchoredPosition = basePos + fromOffset;
        if (wait > 0f) yield return new WaitForSecondsRealtime(wait);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            float e = 1f - Mathf.Pow(1f - k, 3f);
            rect.anchoredPosition = basePos + fromOffset * (1f - e);
            // 처음 40%는 치직거리며 깜빡이고, 그 뒤로 또렷해진다
            group.alpha = k < 0.4f
                ? (Random.value < 0.55f ? Random.Range(0.3f, 0.9f) : 0.05f)
                : Mathf.Lerp(0.85f, 1f, (k - 0.4f) / 0.6f);
            yield return null;
        }
        rect.anchoredPosition = basePos;
        group.alpha = 1f;
    }
}
