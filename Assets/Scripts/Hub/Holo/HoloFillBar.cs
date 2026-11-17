using System.Collections;
using UnityEngine;

// 게이지가 0에서부터 목표만큼 차오른다 (EXP 바 등). 채움 막대의 오른쪽 끝(Anchor Max X)을 움직인다.
public class HoloFillBar : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField, Range(0f, 1f)] private float amount = 0.68f;
    [SerializeField, Min(0f)] private float delay = 0.9f;
    [SerializeField, Min(0.01f)] private float duration = 1.1f;

    private IEnumerator Start()
    {
        if (!fill) yield break;
        SetFill(0f);
        yield return new WaitForSecondsRealtime(delay);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            SetFill(amount * (1f - Mathf.Pow(1f - k, 3f)));
            yield return null;
        }
        SetFill(amount);
    }

    private void SetFill(float x)
    {
        fill.anchorMax = new Vector2(x, fill.anchorMax.y);
        fill.offsetMax = new Vector2(0f, fill.offsetMax.y);
    }
}
