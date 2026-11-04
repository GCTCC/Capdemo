using UnityEngine;

// 구역(씬)마다 하나 두는 맵 틀과 배경 그림.
// 맵 범위(길이·높이)는 Map Length/Height로 정하고, 그 안에 보이지 않는 바닥 선과 양쪽 벽을 만든다.
// 카메라(FollowCamera2D)는 이 범위 밖을 보여 주지 않는다. 씬 화면의 노란 상자가 맵 범위다.
// 배경 그림(Image)은 높이를 Image Height(맵 높이와 별개)에 맞춰 맵 아래끝에 붙이고, 플레이 중에는 카메라보다 천천히 움직여
// 맵 왼쪽 끝에서는 그림 왼쪽 끝이, 오른쪽 끝에서는 그림 오른쪽 끝이 보이게 한다. 그래서 그림이 맵보다 짧아도 된다.
// 바닥·오브젝트 그림은 맵 길이만큼 MapObject로 직접 깐다(충돌은 바닥 선이 맡는다).
// Layout을 "여러 장 이어 붙이기"로 바꾸면 Images 목록의 그림을 왼쪽부터 빈틈없이 이어 붙이고 제자리에 고정한다(실내처럼 배경에 바닥·벽이 그려진 맵).
[ExecuteAlways]
[DefaultExecutionOrder(100)] // FollowCamera2D가 이번 프레임에 움직인 뒤에 따라간다
[RequireComponent(typeof(EdgeCollider2D))]
public class MapBackground : MonoBehaviour
{
    public const string SortingLayer = "Background";
    private const string ImageChildName = "Image";
    private const string StripChildName = "Strip";

    public enum Layout
    {
        [InspectorName("한 장 (천천히 따라오기)")] Single,
        [InspectorName("여러 장 이어 붙이기 (고정)")] Strip,
    }

    [Tooltip("배경을 한 장으로 쓸지, 여러 장을 가로로 이어 붙일지")]
    [SerializeField] private Layout layout = Layout.Single;
    [Tooltip("배경 그림 (한 장 방식). 프로젝트 창에서 끌어다 넣는다.")]
    [SerializeField] private Sprite image;
    [Tooltip("이어 붙일 배경 그림들 (여러 장 방식). 왼쪽부터 순서대로 놓인다.")]
    [SerializeField] private Sprite[] images = new Sprite[0];
    [Tooltip("배경 그림 높이(유닛). 맵 높이(Height)와 따로 정한다. 가로는 그림 비율대로 따라간다.")]
    [SerializeField, Min(0f)] private float imageHeight;  // 0이면 처음 한 번 그때의 맵 높이로 채운다
    [Tooltip("배경 그림 위치 조정(유닛). 기본은 그림 아래끝이 맵 아래끝에, 가로 가운데(여러 장은 왼쪽 끝)가 맵에 맞는다.")]
    [SerializeField] private Vector2 imageOffset;
    [Tooltip("켜면 여러 장 방식에서 맵 길이를 그림 폭의 합으로 맞춘다. 켜 두면 Map Length를 직접 바꿔도 다시 돌아간다.")]
    [SerializeField] private bool fitLengthToImages;
    [Tooltip("맵 가로 길이(유닛). 화면 하나가 약 17.8유닛(16:9, Orthographic Size 5)")]
    [SerializeField, Min(1f)] private float mapLength = 30f;
    [Tooltip("맵 범위(노란 상자)를 가로로 옮기는 값(유닛). 0이면 Background 위치가 맵 가운데다. 배경 그림은 움직이지 않고 범위·바닥·벽·카메라 한계만 옮겨진다.")]
    [SerializeField] private float mapOffset;
    [Tooltip("맵 높이(유닛). 카메라 Orthographic Size가 5면 화면 높이가 10이다. 화면보다 높으면 카메라가 위아래로도 따라간다. 바꾸면 아래끝·바닥·배경은 그대로 있고 위쪽만 늘어난다.")]
    [SerializeField, Min(0.1f)] private float height = 10f;
    [Tooltip("맵 아래끝에서 바닥 윗면까지 높이(유닛). Background를 선택하면 보이는 초록 선이 바닥과 벽이다.")]
    [SerializeField, Min(0f)] private float groundHeight = 2f;
    [Tooltip("양쪽 끝에 보이지 않는 벽을 세워 플레이어가 맵 밖으로 못 나가게 한다.")]
    [SerializeField] private bool walls = true;

    // Height를 바꾸기 전 값. 바꾸면 맵 아래끝이 제자리에 있도록 오브젝트를 옮기는 데 쓴다
    [SerializeField, HideInInspector] private float lastHeight;

    private EdgeCollider2D edge;
    private SpriteRenderer imageRenderer;
    private Camera cam;

    // 맵이 차지하는 월드 영역. 오브젝트 위치에서 Map Offset만큼 옆으로 옮긴 곳을 가운데로 한다
    public Bounds Area => new Bounds(transform.position + Vector3.right * mapOffset, new Vector3(mapLength, height, 0f));

    private float ImageHeight => imageHeight > 0f ? imageHeight : height;

    // 예전 씬처럼 Image Height가 비어 있으면 지금 맵 높이로 한 번 채워서, 그 뒤로는 Height를 바꿔도 그림 크기가 그대로 있게 한다
    private void InitImageHeight()
    {
        if (imageHeight <= 0f) imageHeight = height;
    }

    // 맵은 오브젝트 위치를 가운데로 위아래로 퍼지므로, Height만 바꾸면 아래끝(바닥·그림)이 같이 움직인다.
    // 에디터에서 Height를 바꾸면 그만큼 오브젝트를 옮겨 아래끝·바닥·그림은 제자리에 두고 위쪽만 늘어나게 한다
    private void KeepBottomOnHeightChange()
    {
        if (!Application.isPlaying && lastHeight > 0f && !Mathf.Approximately(lastHeight, height))
            transform.position += Vector3.up * ((height - lastHeight) * 0.5f);
        lastHeight = height;
    }

    // 그림 크기 배율: 그림 높이가 ImageHeight가 되도록
    private float ScaleFor(Sprite sprite) => ImageHeight / sprite.bounds.size.y;

    // 그림 아래끝을 맵 아래끝(+조정값)에 맞추는 세로 위치 (피벗 위치와 상관없이)
    private float BottomAlignedY(Sprite sprite, float scale) => -height * 0.5f + imageOffset.y - sprite.bounds.min.y * scale;

    private void Awake() => Refresh();

    // 에디터에서는 값이나 그림을 바꿀 때마다 바로 맞춘다. (OnValidate에서는 자식 오브젝트를 만들 수 없어 Update에서 한다)
    private void Update()
    {
        if (!Application.isPlaying) Refresh();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && layout == Layout.Single) FollowCamera();
    }

    private void Refresh()
    {
        if (!edge) edge = GetComponent<EdgeCollider2D>();
        if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
        AdoptOldSprite();
        InitImageHeight();
        KeepBottomOnHeightChange();
        // 맵 길이가 그림 폭을 따라가야 하므로 그림을 먼저 놓고 충돌선을 그린다
        RefreshImage();
        RefreshStrip();
        RefreshEdge();
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
        float left = mapOffset - halfX, right = mapOffset + halfX;
        float groundY = -halfY + groundHeight;
        float wallTop = halfY + height;
        var points = walls
            ? new[]
            {
                new Vector2(left, wallTop), new Vector2(left, groundY),
                new Vector2(right, groundY), new Vector2(right, wallTop),
            }
            : new[] { new Vector2(left, groundY), new Vector2(right, groundY) };
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
        // 여러 장 방식에서는 한 장짜리 그림을 숨긴다
        var shown = layout == Layout.Single ? image : null;
        if (imageRenderer.sprite != shown) imageRenderer.sprite = shown;
        if (imageRenderer.sortingLayerName != SortingLayer) imageRenderer.sortingLayerName = SortingLayer;
        if (!shown) return;

        // 그림 높이를 ImageHeight로 맞추고(가로세로 비율은 그대로), 가로 가운데를 맵 가운데에, 아래끝을 맵 아래끝에 둔다
        float scale = ScaleFor(image);
        var wantScale = new Vector3(scale, scale, 1f);
        var t = imageRenderer.transform;
        if (t.localScale != wantScale) t.localScale = wantScale;
        if (!Application.isPlaying)
        {
            var wantPos = SingleImagePosition(0f, scale);
            if (t.localPosition != wantPos) t.localPosition = wantPos;
        }
    }

    // 한 장 방식에서 그림 가로 가운데를 x(맵 가운데 기준)에 둘 때의 위치
    private Vector3 SingleImagePosition(float x, float scale) =>
        new Vector3(x + imageOffset.x - image.bounds.center.x * scale, BottomAlignedY(image, scale), 0f);

    // 여러 장 방식: Strip 아래에 그림마다 자식(Strip_0, Strip_1, ...)을 두고, 맵 왼쪽 끝부터 빈틈없이 이어 놓는다
    private void RefreshStrip()
    {
        var strip = transform.Find(StripChildName);
        bool on = layout == Layout.Strip;
        if (!on)
        {
            if (strip && strip.gameObject.activeSelf) strip.gameObject.SetActive(false);
            return;
        }
        if (!strip)
        {
            strip = new GameObject(StripChildName).transform;
            strip.SetParent(transform, false);
        }
        if (!strip.gameObject.activeSelf) strip.gameObject.SetActive(true);

        // 그림 수에 맞춰 자식을 늘리거나 줄인다
        for (int i = strip.childCount; i < images.Length; i++)
            new GameObject($"{StripChildName}_{i}", typeof(SpriteRenderer)).transform.SetParent(strip, false);
        for (int i = strip.childCount - 1; i >= images.Length; i--)
        {
            var extra = strip.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(extra);
            else DestroyImmediate(extra);
        }

        // 그림마다 높이를 ImageHeight에 맞춘 폭의 합
        float total = 0f;
        foreach (var sprite in images)
            if (sprite) total += sprite.bounds.size.x * ScaleFor(sprite);
        if (fitLengthToImages && total >= 1f && !Mathf.Approximately(mapLength, total)) mapLength = total;

        // Map Offset은 범위만 옮기므로 그림은 오브젝트 기준 원래 자리(Map Offset이 0일 때의 맵 왼쪽 끝)부터 놓는다
        float x = -mapLength * 0.5f + imageOffset.x;
        for (int i = 0; i < images.Length; i++)
        {
            var renderer = strip.GetChild(i).GetComponent<SpriteRenderer>();
            var sprite = images[i];
            if (renderer.sprite != sprite) renderer.sprite = sprite;
            if (renderer.sortingLayerName != SortingLayer) renderer.sortingLayerName = SortingLayer;
            if (!sprite) continue;

            float scale = ScaleFor(sprite);
            var t = renderer.transform;
            var wantScale = new Vector3(scale, scale, 1f);
            // 그림 왼쪽 끝을 x에, 아래끝을 맵 아래끝에 맞춘다 (피벗 위치와 상관없이)
            var wantPos = new Vector3(x - sprite.bounds.min.x * scale, BottomAlignedY(sprite, scale), 0f);
            if (t.localScale != wantScale) t.localScale = wantScale;
            if (t.localPosition != wantPos) t.localPosition = wantPos;
            x += sprite.bounds.size.x * scale;
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
            // 맵 가운데(Map Offset 반영) 기준으로 카메라가 얼마나 왔는지 재고, 그림 위치는 오브젝트 기준으로 되돌린다
            float camX = cam.transform.position.x - Area.center.x;
            float progress = Mathf.InverseLerp(-(mapLength - viewWidth) * 0.5f, (mapLength - viewWidth) * 0.5f, camX);
            x = mapOffset + camX - (progress - 0.5f) * (imageWidth - viewWidth);
        }
        imageRenderer.transform.localPosition = SingleImagePosition(x, scale);
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
        Gizmos.DrawWireCube(Area.center, Area.size);
    }
}
