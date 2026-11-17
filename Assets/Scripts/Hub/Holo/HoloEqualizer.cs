using UnityEngine;

// 막대들의 높이를 음악처럼 출렁이게 한다 (로고 파형, NOW PLAYING 막대, 신호 막대 등).
// 막대의 Pivot은 아래쪽(Y 0)이어야 위로만 자란다.
public class HoloEqualizer : MonoBehaviour
{
    [SerializeField] private RectTransform[] bars = new RectTransform[0];
    [SerializeField] private Vector2 height = new Vector2(4f, 20f);
    [Tooltip("출렁이는 빠르기")]
    [SerializeField] private float speed = 3f;
    [Tooltip("0이면 부드럽게, 클수록 뚝뚝 끊기게 (신호 막대용)")]
    [SerializeField, Min(0f)] private float stepsPerSecond = 0f;

    private float seed;

    private void Awake() => seed = Random.value * 100f;

    private void Update()
    {
        float time = Time.unscaledTime;
        if (stepsPerSecond > 0f) time = Mathf.Floor(time * stepsPerSecond) / stepsPerSecond;
        for (int i = 0; i < bars.Length; i++)
        {
            if (!bars[i]) continue;
            float n = Mathf.PerlinNoise(seed + time * speed + i * 1.9f, i * 0.53f);
            bars[i].sizeDelta = new Vector2(bars[i].sizeDelta.x, Mathf.Lerp(height.x, height.y, n));
        }
    }
}
