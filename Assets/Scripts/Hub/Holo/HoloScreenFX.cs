using UnityEngine;
using UnityEngine.UI;

// 화면 전체 홀로그램 느낌: 가끔 아주 약하게 깜빡이고, 가끔 얇은 지직 띠가 위에서 아래로 훑고 지나간다.
// (스캔라인은 HoloUVScroll, 가장자리 어둡게는 비네트 그림이 맡는다)
public class HoloScreenFX : MonoBehaviour
{
    [Tooltip("화면 전체를 살짝 어둡게 깜빡일 검은 판")]
    [SerializeField] private Graphic flicker;
    [SerializeField, Range(0f, 0.3f)] private float flickerStrength = 0.06f;

    [Tooltip("위에서 아래로 훑고 지나가는 얇은 띠")]
    [SerializeField] private RectTransform band;
    [SerializeField] private Vector2 bandInterval = new Vector2(5f, 11f);
    [SerializeField, Min(0.05f)] private float bandTime = 0.35f;

    private float nextBand;
    private float bandT = -1f;

    private void Start()
    {
        if (band) band.gameObject.SetActive(false);
        nextBand = Time.unscaledTime + Random.Range(2f, 4f);
    }

    private void Update()
    {
        if (flicker)
        {
            var c = flicker.color;
            c.a = Random.value < 0.06f ? Random.Range(0f, flickerStrength) : Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 0.5f);
            flicker.color = c;
        }

        if (!band) return;
        if (bandT < 0f)
        {
            if (Time.unscaledTime < nextBand) return;
            bandT = 0f;
            band.gameObject.SetActive(true);
        }

        bandT += Time.unscaledDeltaTime / bandTime;
        var parent = (RectTransform)band.parent;
        float h = parent.rect.height;
        band.anchoredPosition = new Vector2(Random.Range(-8f, 8f), Mathf.Lerp(h * 0.5f, -h * 0.5f, bandT));
        band.localScale = new Vector3(1f, Random.Range(0.6f, 1.6f), 1f);
        if (bandT >= 1f)
        {
            bandT = -1f;
            band.gameObject.SetActive(false);
            nextBand = Time.unscaledTime + Random.Range(bandInterval.x, Mathf.Max(bandInterval.x, bandInterval.y));
        }
    }
}
