using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 칸으로 나뉜 진행도 막대 (STORY PROGRESS). 시작하면 칸이 하나씩 차오르고, 진행 중인 칸은 깜빡인다.
public class HoloSegments : MonoBehaviour
{
    [SerializeField] private Image[] segments = new Image[0];
    [Tooltip("다 찬 칸 수 (소수면 마지막 칸은 진행 중으로 깜빡인다)")]
    [SerializeField] private float filled = 3.5f;
    [SerializeField] private Color onColor = new Color(0.64f, 0.92f, 0.84f, 1f);
    [SerializeField] private Color offColor = new Color(0.64f, 0.92f, 0.84f, 0.15f);
    [SerializeField, Min(0f)] private float delay = 1.0f;
    [SerializeField, Min(0f)] private float stepTime = 0.09f;

    private bool done;

    private IEnumerator Start()
    {
        foreach (var s in segments) if (s) s.color = offColor;
        yield return new WaitForSecondsRealtime(delay);
        int full = Mathf.FloorToInt(filled);
        for (int i = 0; i < full && i < segments.Length; i++)
        {
            if (segments[i]) segments[i].color = onColor;
            yield return new WaitForSecondsRealtime(stepTime);
        }
        done = true;
    }

    private void Update()
    {
        if (!done) return;
        int partial = Mathf.FloorToInt(filled);
        if (filled - partial <= 0.01f || partial >= segments.Length || !segments[partial]) return;
        float s = Mathf.Sin(Time.unscaledTime * 5f) * 0.5f + 0.5f;
        segments[partial].color = Color.Lerp(offColor, onColor, 0.25f + 0.6f * s);
    }
}
