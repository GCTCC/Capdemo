using UnityEngine;

// 구역(씬)마다 하나 두는 큰 배경 그림. Sprite 칸에 그림을 넣으면
// 높이를 화면에 맞추고, 바닥과 양쪽 벽(보이지 않는 충돌선)을 그림 크기에 맞춰 다시 그린다.
// 카메라(FollowCamera2D)는 이 그림 밖을 보여 주지 않는다.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer), typeof(EdgeCollider2D))]
public class MapBackground : MonoBehaviour
{
    public const string SortingLayer = "Background";

    [Tooltip("배경 높이(유닛). 카메라 Orthographic Size가 5면 화면 높이가 10이다.")]
    [SerializeField, Min(0.1f)] private float height = 10f;
    [Tooltip("그림 아래끝에서 바닥 윗면까지 높이(유닛). Background를 선택하면 씬 화면에 보이는 초록 선이 바닥과 벽이다.")]
    [SerializeField, Min(0f)] private float groundHeight = 2f;
    [Tooltip("양쪽 끝에 보이지 않는 벽을 세워 플레이어가 그림 밖으로 못 나가게 한다.")]
    [SerializeField] private bool walls = true;

    private SpriteRenderer spriteRenderer;
    private EdgeCollider2D edge;

    public bool HasImage => Renderer.sprite;
    // 배경이 차지하는 월드 영역
    public Bounds Area => Renderer.bounds;

    private SpriteRenderer Renderer => spriteRenderer ? spriteRenderer : spriteRenderer = GetComponent<SpriteRenderer>();

    private void OnValidate() => Refresh();

    private void Awake() => Refresh();

    // 에디터에서는 그림을 바꾸거나 옮길 때마다 바로 맞춘다
    private void Update()
    {
        if (!Application.isPlaying) Refresh();
    }

    private void Refresh()
    {
        if (!edge) edge = GetComponent<EdgeCollider2D>();
        if (Renderer.sortingLayerName != SortingLayer) Renderer.sortingLayerName = SortingLayer;

        var sprite = Renderer.sprite;
        if (!sprite)
        {
            if (edge.enabled) edge.enabled = false;
            return;
        }

        // 그림 높이가 height가 되도록 크기를 맞춘다 (가로세로 비율은 그대로)
        float scale = height / sprite.bounds.size.y;
        var wantScale = new Vector3(scale, scale, 1f);
        if (transform.localScale != wantScale) transform.localScale = wantScale;

        // 충돌선은 그림 기준 좌표(크기 맞추기 전)로 적는다. 벽은 화면 위로 한 화면만큼 더 올려 점프로 못 넘게 한다
        var local = sprite.bounds;
        float groundY = local.min.y + groundHeight / scale;
        float wallTop = local.max.y + local.size.y;
        var points = walls
            ? new[]
            {
                new Vector2(local.min.x, wallTop), new Vector2(local.min.x, groundY),
                new Vector2(local.max.x, groundY), new Vector2(local.max.x, wallTop),
            }
            : new[] { new Vector2(local.min.x, groundY), new Vector2(local.max.x, groundY) };
        if (!SamePoints(edge.points, points)) edge.points = points;
        if (!edge.enabled) edge.enabled = true;
    }

    private static bool SamePoints(Vector2[] a, Vector2[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if ((a[i] - b[i]).sqrMagnitude > 1e-8f) return false;
        return true;
    }
}
