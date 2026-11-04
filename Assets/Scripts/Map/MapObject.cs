using UnityEngine;

// 맵에 놓는 오브젝트 하나(상자, 가로등, 표지판 등). Sprite 칸에 그림을 넣으면 충돌 상자가 그림 크기에 맞춰진다.
// 그림은 언제든 바꿀 수 있고, 바꾸면 충돌 상자도 따라간다.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class MapObject : MonoBehaviour
{
    public const string BackLayer = "MapBack";
    public const string FrontLayer = "MapFront";

    public enum Depth
    {
        [InspectorName("캐릭터 뒤")] Back,
        [InspectorName("캐릭터 앞")] Front,
    }

    [Tooltip("체크하면 플레이어가 밟거나 부딪힌다. 끄면 그림만 보인다.")]
    [SerializeField] private bool solid;
    [Tooltip("캐릭터보다 뒤에 그릴지 앞에 그릴지")]
    [SerializeField] private Depth depth = Depth.Back;
    [Tooltip("같은 쪽(뒤/앞) 오브젝트끼리 겹칠 때 숫자가 클수록 위에 그린다.")]
    [SerializeField] private int order;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D box;

    private void OnValidate() => Refresh();

    private void Awake() => Refresh();

    private void Update()
    {
        if (!Application.isPlaying) Refresh();
    }

    private void Refresh()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!box) box = GetComponent<BoxCollider2D>();

        string layer = depth == Depth.Front ? FrontLayer : BackLayer;
        if (spriteRenderer.sortingLayerName != layer) spriteRenderer.sortingLayerName = layer;
        if (spriteRenderer.sortingOrder != order) spriteRenderer.sortingOrder = order;

        var sprite = spriteRenderer.sprite;
        bool on = solid && sprite;
        if (box.enabled != on) box.enabled = on;
        if (!sprite) return;

        // 충돌 상자를 그림 테두리에 맞춘다 (피벗 위치와 상관없이)
        var bounds = sprite.bounds;
        if ((Vector2)bounds.size != box.size) box.size = bounds.size;
        if ((Vector2)bounds.center != box.offset) box.offset = bounds.center;
    }
}
