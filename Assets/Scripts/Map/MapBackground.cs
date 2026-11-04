using UnityEngine;

// 구역(씬)마다 하나 두는 맵 틀과 배경 그림.
// 맵 범위(길이·높이)는 Map Length/Height로 정하고, 그 안에 보이지 않는 바닥 선과 양쪽 벽을 만든다.
// 카메라(FollowCamera2D)는 이 범위 밖을 보여 주지 않는다. 씬 화면의 노란 상자가 맵 범위다.
// 배경 그림(Image)은 높이를 맵 높이에 맞추고, 플레이 중에는 카메라보다 천천히 움직여
// 맵 왼쪽 끝에서는 그림 왼쪽 끝이, 오른쪽 끝에서는 그림 오른쪽 끝이 보이게 한다. 그래서 그림이 맵보다 짧아도 된다.
// 바닥·오브젝트 그림은 맵 길이만큼 MapObject로 직접 깐다(충돌은 바닥 선이 맡는다).
[ExecuteAlways]
[DefaultExecutionOrder(100)] // FollowCamera2D가 이번 프레임에 움직인 뒤에 따라간다
[RequireComponent(typeof(EdgeCollider2D))]
public class MapBackground : MonoBehaviour
{
    public const string SortingLayer = "Background";
    private const string ImageChildName = "Image";

    [Tooltip("배경 그림. 프로젝트 창에서 끌어다 넣는다.")]
    [SerializeField] private Sprite image;
    [Tooltip("맵 가로 길이(유닛). 화면 하나가 약 17.8유닛(16:9, Orthographic Size 5)")]
    [SerializeField, Min(1f)] private float mapLength = 30f;
    [Tooltip("맵 높이(유닛). 카메라 Orthographic Size가 5면 화면 높이가 10이다. 배경 그림도 이 높이에 맞춘다.")]
    [SerializeField, Min(0.1f)] private float height = 10f;
    [Tooltip("맵 아래끝에서 바닥 윗면까지 높이(유닛). Background를 선택하면 보이는 초록 선이 바닥과 벽이다.")]
    [SerializeField, Min(0f)] private float groundHeight = 2f;
    [Tooltip("양쪽 끝에 보이지 않는 벽을 세워 플레이어가 맵 밖으로 못 나가게 한다.")]
    [SerializeField] private bool walls = true;

    private EdgeCollider2D edge;
    private SpriteRenderer imageRenderer;
    private Camera cam;

    // 맵이 차지하는 월드 영역. 오브젝트 위치(가운데)를 중심으로 한다
    public Bounds Area => new Bounds(transform.position, new Vector3(mapLength, height, 0f));

    private void Awake() => Refresh();

    // 에디터에서는 값이나 그림을 바꿀 때마다 바로 맞춘다. (OnValidate에서는 자식 오브젝트를 만들 수 없어 Update에서 한다)
    private void Update()
    {
        if (!Application.isPlaying) Refresh();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying) FollowCamera();
    }

    private void Refresh()
    {
        if (!edge) edge = GetComponent<EdgeCollider2D>();
        if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
        AdoptOldSprite();
        RefreshEdge();
        RefreshImage();
    }

    // 예전 방식(Background 자체의 Sprite Renderer)에 넣어 둔 그림을 Image 칸으로 옮긴다
    private void AdoptOldSprite()
    {
        if (!TryGetComponent(out SpriteRenderer old) || !old.sprite) return;
        if (!image) image = old.sprite;
        old.sprite = null;
    }

    private void RefreshEdge()
    {
        // 충돌선은 이 오브젝트 기준 좌표로 적는다. 벽은 화면 위로 한 화면만큼 더 올려 점프로 못 넘게 한다
        float halfX = mapLength * 0.5f, halfY = height * 0.5f;
        float groundY = -halfY + groundHeight;
        float wallTop = halfY + height;
        var points = walls
            ? new[]
            {
                new Vector2(-halfX, wallTop), new Vector2(-halfX, groundY),
                new Vector2(halfX, groundY), new Vector2(halfX, wallTop),
            }
            : new[] { new Vector2(-halfX, groundY), new Vector2(halfX, groundY) };
        if (!SamePoints(edge.points, points)) edge.points = points;
    }

    // 그림은 자식 오브젝트(Image)에 그린다. 그림만 움직여야 바닥 선이 같이 움직이지 않는다
    private void RefreshImage()
    {
        if (!imageRenderer)
        {
            var child = transform.Find(ImageChildName);
            if (!child)
            {
                child = new GameObject(ImageChildName, typeof(SpriteRenderer)).transform;
                child.SetParent(transform, false);
            }
            imageRenderer = child.GetComponent<SpriteRenderer>();
        }
        if (imageRenderer.sprite != image) imageRenderer.sprite = image;
        if (imageRenderer.sortingLayerName != SortingLayer) imageRenderer.sortingLayerName = SortingLayer;
        if (!image) return;

        // 그림 높이가 맵 높이가 되도록 크기를 맞추고(가로세로 비율은 그대로), 그림 가운데를 맵 가운데에 둔다
        float scale = height / image.bounds.size.y;
        var wantScale = new Vector3(scale, scale, 1f);
        var t = imageRenderer.transform;
        if (t.localScale != wantScale) t.localScale = wantScale;
        if (!Application.isPlaying)
        {
            var wantPos = -image.bounds.center * scale;
            if (t.localPosition != wantPos) t.localPosition = wantPos;
        }
    }

    // 카메라가 맵 왼쪽 끝 → 오른쪽 끝으로 가는 동안, 그림도 왼쪽 끝 → 오른쪽 끝이 보이도록 옮긴다
    private void FollowCamera()
    {
        if (!image || !imageRenderer) return;
        if (!cam) cam = Camera.main;
        if (!cam) return;

        float scale = imageRenderer.transform.localScale.x;
        float imageWidth = image.bounds.size.x * scale;
        float viewWidth = cam.orthographicSize * 2f * cam.aspect;
        float x = 0f;
        // 그림이 화면보다 넓고 맵보다 짧을 때만 따라 움직인다. 아니면 맵 가운데에 고정
        if (imageWidth > viewWidth && imageWidth < mapLength && mapLength > viewWidth)
        {
            float camX = cam.transform.position.x - transform.position.x;
            float progress = Mathf.InverseLerp(-(mapLength - viewWidth) * 0.5f, (mapLength - viewWidth) * 0.5f, camX);
            x = camX - (progress - 0.5f) * (imageWidth - viewWidth);
        }
        imageRenderer.transform.localPosition = new Vector3(x, 0f, 0f) - image.bounds.center * scale;
    }

    private static bool SamePoints(Vector2[] a, Vector2[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if ((a[i] - b[i]).sqrMagnitude > 1e-8f) return false;
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawWireCube(transform.position, new Vector3(mapLength, height, 0f));
    }
}
