using UnityEngine;
using UnityEngine.UI;

// 숨 쉬듯 밝아졌다 흐려졌다 / 커졌다 작아졌다 (알림 배지, 이벤트 점, D-3 글자 등)
public class HoloPulse : MonoBehaviour
{
    [SerializeField] private Graphic target;
    [SerializeField] private Vector2 alpha = new Vector2(0.45f, 1f);
    [SerializeField] private float scaleAmount = 0f;
    [Tooltip("1초에 몇 번 뛰는지")]
    [SerializeField] private float rate = 1.2f;

    private float baseAlpha = 1f;

    private void Awake()
    {
        if (!target) target = GetComponent<Graphic>();
        if (target) baseAlpha = target.color.a;
    }

    private void Update()
    {
        float s = Mathf.Sin(Time.unscaledTime * rate * Mathf.PI * 2f) * 0.5f + 0.5f;
        if (target)
        {
            var c = target.color;
            c.a = baseAlpha * Mathf.Lerp(alpha.x, alpha.y, s);
            target.color = c;
        }
        if (scaleAmount != 0f) transform.localScale = Vector3.one * (1f + scaleAmount * s);
    }
}
