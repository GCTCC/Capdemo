using UnityEngine;

// NPC 프리팹용. Sprite 칸에 그림을 넣으면 대화 범위(트리거)와 머리 위 "F" 표시 위치를 그림 크기에 맞춘다.
// 그리기 순서는 Order 하나로 정한다(Sprite Renderer의 Sorting Layer·Order in Layer는 이 값으로 덮어쓴다).
// 맵 오브젝트·플레이어와 같은 층(Map)에 그리고, 플레이어는 0이다. 음수면 플레이어 뒤, 양수면 앞.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class NpcLayout : MonoBehaviour
{
    // 예전 방식(캐릭터 층·오브젝트 뒤를 따로 쓰던 때)의 값. 옮겨 담는 데만 쓴다
    public const string OldCharacterLayer = "Characters";
    public const int OldPlayerOrder = 10;
    public enum Depth { Characters, BehindObjects }

    [Tooltip("앞뒤 순서. 플레이어는 0이다. 음수면 플레이어 뒤, 양수면 앞. 맵 오브젝트의 Order와 같은 기준이다.")]
    [SerializeField] private int order = -1;

    // 예전 방식의 캐릭터 층/오브젝트 뒤 값. 한 번 Order로 옮겨 담고 나면 쓰지 않는다
    [SerializeField, HideInInspector] private Depth depth = Depth.Characters;
    // 0이면 예전 방식 값이다. Tools > CapDemo > 맵 > 그리기 순서 한 층으로 합치기 에서 옮겨 담는다.
    // 에디터에서 새로 붙이면 Reset에서 1이 된다
    [SerializeField, HideInInspector] private int layoutVersion;

    public bool UsesOldLayout => layoutVersion < 1;

    [Tooltip("말을 걸 수 있는 가로 범위(유닛). NPC 중심에서 양옆으로 절반씩")]
    [SerializeField, Min(0.1f)] private float talkWidth = 3f;
    [Tooltip("머리 위 안내 표시")]
    [SerializeField] private Transform prompt;
    [Tooltip("그림 위끝에서 안내 표시까지 간격(유닛)")]
    [SerializeField] private float promptGap = 0.3f;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D area;

    // 에디터에서 새로 붙일 때. 예전 값이 없으니 옮겨 담지 않는다
    private void Reset()
    {
        order = -1;
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
        if (!area) area = GetComponent<BoxCollider2D>();
        // 옮겨 담기 전에는 예전처럼 그려서 보이는 순서가 바뀌지 않게 한다
        bool behind = depth == Depth.BehindObjects;
        string layer = !UsesOldLayout ? MapObject.Layer : behind ? MapObject.OldBackLayer : OldCharacterLayer;
        int sortingOrder = UsesOldLayout && behind ? order - 1 : order;
        if (spriteRenderer.sortingLayerName != layer) spriteRenderer.sortingLayerName = layer;
        if (spriteRenderer.sortingOrder != sortingOrder) spriteRenderer.sortingOrder = sortingOrder;
        if (!area.isTrigger) area.isTrigger = true;

        var sprite = spriteRenderer.sprite;
        if (!sprite) return;
        var bounds = sprite.bounds;
        var size = new Vector2(talkWidth, bounds.size.y);
        var offset = new Vector2(0f, bounds.center.y);
        if (area.size != size) area.size = size;
        if (area.offset != offset) area.offset = offset;

        if (!prompt) return;
        var promptPos = new Vector3(0f, bounds.max.y + promptGap, 0f);
        if (prompt.localPosition != promptPos) prompt.localPosition = promptPos;
    }

    // 예전에는 플레이어(Order 10)와 같은 캐릭터 층, 또는 오브젝트 뒤(MapBack 층, Order - 1)에 그렸다.
    // 보이는 앞뒤가 그대로 남도록 맵 오브젝트와 같은 기준의 Order로 옮긴다. 옮겼으면 true
    public bool MigrateLayout()
    {
        if (!UsesOldLayout) return false;
        order = depth == Depth.BehindObjects ? order - 1 + MapObject.OldBackShift : order - OldPlayerOrder + MapObject.PlayerOrder;
        layoutVersion = 1;
        Refresh();
        return true;
    }
}
