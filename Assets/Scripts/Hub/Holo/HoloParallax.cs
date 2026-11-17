using UnityEngine;
using UnityEngine.InputSystem;

// 마우스를 움직이면 배경이 반대쪽으로 아주 살짝 움직여 깊이감을 준다.
public class HoloParallax : MonoBehaviour
{
    [Tooltip("최대로 움직이는 거리(픽셀)")]
    [SerializeField] private Vector2 strength = new Vector2(18f, 10f);
    [SerializeField, Min(0f)] private float smooth = 4f;

    private RectTransform rect;
    private Vector2 basePos;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePos = rect.anchoredPosition;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 p = mouse.position.ReadValue();
        Vector2 n = new Vector2(p.x / Mathf.Max(1, Screen.width) - 0.5f, p.y / Mathf.Max(1, Screen.height) - 0.5f) * 2f;
        n = Vector2.ClampMagnitude(n, 1.5f);
        Vector2 goal = basePos - Vector2.Scale(n, strength);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, goal, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
    }
}
