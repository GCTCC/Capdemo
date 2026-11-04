using UnityEngine;

// 맵에 놓는 오브젝트 하나(상자, 가로등, 표지판 등). Sprite 칸에 그림을 넣으면 충돌 상자가 그림 크기에 맞춰진다.
// 그림은 언제든 바꿀 수 있고, 바꾸면 충돌 상자도 따라간다.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class MapObject : MonoBehaviour
{
    // 배경을 뺀 맵의 모든 그림(오브젝트·NPC·플레이어)이 함께 쓰는 그리기 층. 앞뒤는 Order 숫자 하나로만 정한다
    public const string Layer = "Map";
    // 플레이어 Order. 이보다 작으면 플레이어 뒤, 크면 앞에 그린다
    public const int PlayerOrder = 0;
    // NPC 머리 위 안내 표시처럼 항상 맨 앞에 그리는 층
    public const string FrontLayer = "MapFront";

    // 예전 방식(캐릭터 뒤/앞 층을 따로 쓰던 때)의 값. 옮겨 담는 데만 쓴다
    public const string OldBackLayer = "MapBack";
    public const int OldBackShift = -20, OldFrontShift = 20;
    public enum Depth { Back, Front }

    [Tooltip("체크하면 플레이어가 밟거나 부딪힌다. 끄면 그림만 보인다.")]
    [SerializeField] private bool solid;
    [Tooltip("앞뒤 순서. 플레이어는 0이다. 음수면 플레이어 뒤, 양수면 앞. 오브젝트·NPC끼리도 클수록 앞에 그린다.")]
    [SerializeField] private int order = -10;

    // 예전 방식의 캐릭터 뒤/앞 값. 한 번 Order로 옮겨 담고 나면 쓰지 않는다
    [SerializeField, HideInInspector] private Depth depth = Depth.Back;
    // 0이면 예전 방식 값이다. Tools > CapDemo > 맵 > 그리기 순서 한 층으로 합치기 에서 옮겨 담는다.
    // 에디터에서 새로 붙이면 Reset에서 1이 된다
    [SerializeField, HideInInspector] private int layoutVersion;

    public bool UsesOldLayout => layoutVersion < 1;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D box;

    // 에디터에서 새로 붙일 때. 예전 값이 없으니 옮겨 담지 않는다
    private void Reset()
    {
        order = -10;
        layoutVersion = 1;
    }

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

        // 옮겨 담기 전에는 예전처럼 그려서 보이는 순서가 바뀌지 않게 한다
        string layer = !UsesOldLayout ? Layer : depth == Depth.Front ? FrontLayer : OldBackLayer;
        if (spriteRenderer.sortingLayerName != layer) spriteRenderer.sortingLayerName = layer;
        if (spriteRenderer.sortingOrder != order) spriteRenderer.sortingOrder = order;

        // 그림이 없어도 Solid면 충돌한다(보이지 않는 발판·벽). 이때 충돌 상자 크기는 직접 정한 값을 그대로 둔다
        if (box.enabled != solid) box.enabled = solid;
        var sprite = spriteRenderer.sprite;
        if (!sprite) return;

        // 충돌 상자를 그림 테두리에 맞춘다 (피벗 위치와 상관없이). Draw Mode가 Tiled/Sliced면 그 크기에 맞춘다
        Bounds bounds = spriteRenderer.drawMode == SpriteDrawMode.Simple
            ? sprite.bounds
            : new Bounds(Vector3.zero, spriteRenderer.size);
        if ((Vector2)bounds.size != box.size) box.size = bounds.size;
        if ((Vector2)bounds.center != box.offset) box.offset = bounds.center;
    }

    // 예전에는 "캐릭터 뒤"(MapBack 층)와 "캐릭터 앞"(MapFront 층)을 따로 썼다.
    // 보이는 앞뒤가 그대로 남도록 뒤는 -20, 앞은 +20을 더해 한 층의 Order로 옮긴다. 옮겼으면 true
    public bool MigrateLayout()
    {
        if (!UsesOldLayout) return false;
        order += depth == Depth.Front ? OldFrontShift : OldBackShift;
        layoutVersion = 1;
        Refresh();
        return true;
    }
}
