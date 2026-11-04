using UnityEngine;

// NPC 프리팹용. Sprite 칸에 그림을 넣으면 대화 범위(트리거)와 머리 위 "Space" 표시 위치를 그림 크기에 맞춘다.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class NpcLayout : MonoBehaviour
{
    public const string SortingLayer = "Characters";

    [Tooltip("말을 걸 수 있는 가로 범위(유닛). NPC 중심에서 양옆으로 절반씩")]
    [SerializeField, Min(0.1f)] private float talkWidth = 3f;
    [Tooltip("머리 위 안내 표시")]
    [SerializeField] private Transform prompt;
    [Tooltip("그림 위끝에서 안내 표시까지 간격(유닛)")]
    [SerializeField] private float promptGap = 0.3f;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D area;

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
        if (spriteRenderer.sortingLayerName != SortingLayer) spriteRenderer.sortingLayerName = SortingLayer;
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
}
