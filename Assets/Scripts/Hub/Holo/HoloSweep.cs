using UnityEngine;

// 빛 조각이 부모의 왼쪽 끝에서 오른쪽 끝으로 쓱 지나가고, 잠깐 쉬었다 다시 지나간다
// (윗줄을 달리는 빛, 선택된 메뉴 위를 훑는 반짝임 등)
// 빛 조각의 Anchor와 Pivot은 왼쪽 가운데(0, 0.5)로 둔다.
public class HoloSweep : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float travelTime = 1.2f;
    [SerializeField] private Vector2 pause = new Vector2(1.5f, 3.5f);

    private RectTransform rect;
    private float t = -1f;
    private float wait;

    private void Awake()
    {
        rect = (RectTransform)transform;
        wait = Random.Range(0.2f, pause.y);
    }

    private void Update()
    {
        var parent = (RectTransform)rect.parent;
        if (t < 0f)
        {
            rect.anchoredPosition = new Vector2(-rect.rect.width, rect.anchoredPosition.y);
            wait -= Time.unscaledDeltaTime;
            if (wait > 0f) return;
            t = 0f;
        }
        t += Time.unscaledDeltaTime / travelTime;
        float k = Mathf.Clamp01(t);
        float e = k * k * (3f - 2f * k);
        rect.anchoredPosition = new Vector2(Mathf.Lerp(-rect.rect.width, parent.rect.width, e), rect.anchoredPosition.y);
        if (t >= 1f)
        {
            t = -1f;
            wait = Random.Range(pause.x, Mathf.Max(pause.x, pause.y));
        }
    }
}
