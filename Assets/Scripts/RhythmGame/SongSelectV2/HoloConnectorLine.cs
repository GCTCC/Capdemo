using UnityEngine;
using UnityEngine.UI;

// 홀로그램 커버에서 선택한 곡 줄까지 이어지는 꺾인 선.
// 시작점(start)에서 가로로 조금 가다가 45도로 꺾여 끝점 높이까지 올라가고/내려가고, 다시 가로로 끝점까지 간다.
// 끝점은 SongSelectHoloController가 선택한 줄 왼쪽 가운데로 정해준다.
// 색은 이 오브젝트의 Color, 굵기는 Thickness로 바꾼다.
[RequireComponent(typeof(CanvasRenderer))]
public class HoloConnectorLine : MaskableGraphic
{
    [SerializeField] private RectTransform start;
    [SerializeField, Min(0.5f)] private float thickness = 2f;
    [Tooltip("시작점에서 꺾이기 전까지 가로로 가는 길이")]
    [SerializeField, Min(0f)] private float firstRun = 110f;
    [Tooltip("시작점에 찍는 작은 점 크기 (0이면 안 찍음)")]
    [SerializeField, Min(0f)] private float startDot = 7f;
    [Tooltip("끝점(곡 줄 쪽)에 찍는 작은 사각 노드 크기 (0이면 안 찍음)")]
    [SerializeField, Min(0f)] private float endDot = 7f;
    [Tooltip("선이 그려지는 시간(초). 곡을 바꿀 때마다 다시 그려진다")]
    [SerializeField, Min(0f)] private float drawDuration = 0.25f;

    private Vector3 endWorld;
    private bool hasEnd;
    private float progress = 1f;
    private Vector3 lastStart;
    private Vector3 lastEnd;

    public RectTransform StartPoint
    {
        get { return start; }
        set { start = value; SetVerticesDirty(); }
    }

    public void SetEnd(Vector3 worldPosition)
    {
        endWorld = worldPosition;
        hasEnd = true;
    }

    // 곡을 바꿀 때 불러서 선을 처음부터 다시 그린다
    public void Replay()
    {
        progress = drawDuration > 0f ? 0f : 1f;
        SetVerticesDirty();
    }

    private void LateUpdate()
    {
        if (progress < 1f)
        {
            progress = Mathf.Min(1f, progress + Time.unscaledDeltaTime / drawDuration);
            SetVerticesDirty();
        }

        if (start == null || !hasEnd) return;
        if ((start.position - lastStart).sqrMagnitude > 0.01f || (endWorld - lastEnd).sqrMagnitude > 0.01f)
        {
            lastStart = start.position;
            lastEnd = endWorld;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (start == null || !hasEnd) return;

        Vector2 p0 = rectTransform.InverseTransformPoint(start.position);
        Vector2 p3 = rectTransform.InverseTransformPoint(endWorld);
        float dy = p3.y - p0.y;
        float dx = p3.x - p0.x;
        float run = Mathf.Clamp(firstRun, 0f, Mathf.Max(0f, dx - Mathf.Abs(dy)));
        float diag = Mathf.Min(Mathf.Abs(dy), Mathf.Max(0f, dx - run)); // 너무 가까우면 기울기가 가팔라진다

        Vector2 p1 = new Vector2(p0.x + run, p0.y);
        Vector2 p2 = new Vector2(p1.x + diag, p3.y);

        Vector2[] pts = { p0, p1, p2, p3 };
        float total = 0f;
        for (int i = 0; i < 3; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
        float remain = total * Mathf.SmoothStep(0f, 1f, progress);

        for (int i = 0; i < 3 && remain > 0f; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            float len = Vector2.Distance(a, b);
            if (len < 0.001f) continue;
            if (remain < len) b = a + (b - a) * (remain / len);
            remain -= len;
            AddSegment(vh, a, b, i > 0);
        }

        if (startDot > 0f) AddQuad(vh, p0, startDot * 0.5f, startDot * 0.5f);
        // 끝점 사각 노드 (선이 다 그려진 뒤에)
        if (endDot > 0f && progress >= 1f) AddQuad(vh, p3, endDot * 0.5f, endDot * 0.5f);
    }

    private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, bool extendStart)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
        // 꺾이는 곳이 벌어져 보이지 않게 시작을 반 굵기만큼 늘린다
        if (extendStart) a -= dir * (thickness * 0.5f);
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = a - n; vh.AddVert(v);
        v.position = a + n; vh.AddVert(v);
        v.position = b + n; vh.AddVert(v);
        v.position = b - n; vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    private void AddQuad(VertexHelper vh, Vector2 c, float hw, float hh)
    {
        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = c + new Vector2(-hw, -hh); vh.AddVert(v);
        v.position = c + new Vector2(-hw, hh); vh.AddVert(v);
        v.position = c + new Vector2(hw, hh); vh.AddVert(v);
        v.position = c + new Vector2(hw, -hh); vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }
}
