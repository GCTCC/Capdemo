using UnityEngine;
using UnityEngine.UI;

// RawImage의 무늬(스캔라인, 사선 줄무늬 등)를 바둑판처럼 반복해서 깔고 천천히 흘린다.
// 무늬 텍스처는 Wrap Mode가 Repeat여야 한다.
[RequireComponent(typeof(RawImage))]
public class HoloUVScroll : MonoBehaviour
{
    [Tooltip("무늬 한 칸의 크기(픽셀)")]
    [SerializeField] private Vector2 tileSize = new Vector2(4f, 4f);
    [Tooltip("1초에 흐르는 칸 수")]
    [SerializeField] private Vector2 speed = new Vector2(0f, -0.5f);

    private RawImage image;

    private void Awake() => image = GetComponent<RawImage>();

    private void Update()
    {
        var size = image.rectTransform.rect.size;
        var uv = image.uvRect;
        uv.width = Mathf.Max(0.01f, size.x / Mathf.Max(0.01f, tileSize.x));
        uv.height = Mathf.Max(0.01f, size.y / Mathf.Max(0.01f, tileSize.y));
        uv.x = Mathf.Repeat(uv.x + speed.x * Time.unscaledDeltaTime, 1f);
        uv.y = Mathf.Repeat(uv.y + speed.y * Time.unscaledDeltaTime, 1f);
        image.uvRect = uv;
    }
}
