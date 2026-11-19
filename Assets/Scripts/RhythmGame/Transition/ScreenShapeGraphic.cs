using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 화면에 바로 그리는 선과 면 (시안 좌표). 로딩 테두리, 충격파 타원, 판정선 번쩍임, 하이웨이 바닥 빛 등.
// 매 프레임 Begin → Line/Polygon/Ellipse 추가 → End 로 다시 만든다.
[RequireComponent(typeof(CanvasRenderer))]
public class ScreenShapeGraphic : MaskableGraphic
{
    public Texture texture; // 선 단면용 (가운데 밝은 가로 그라데이션). 없으면 단색

    private readonly List<UIVertex> verts = new List<UIVertex>();
    private readonly List<int> tris = new List<int>();

    public override Texture mainTexture
    {
        get { return texture != null ? texture : s_WhiteTexture; }
    }

    public void Begin()
    {
        verts.Clear();
        tris.Clear();
    }

    public void End()
    {
        SetVerticesDirty();
    }

    private void V(Vector2 p, Color c, Vector2 uv)
    {
        var v = UIVertex.simpleVert;
        v.position = new Vector3(p.x, -p.y, 0f);
        v.color = c;
        v.uv0 = uv;
        verts.Add(v);
    }

    // 두께 있는 선 (글로우 텍스처를 쓰면 가운데만 밝고 양옆이 흐리다)
    public void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
        int k = verts.Count;
        V(a - n, c, new Vector2(0, 0));
        V(a + n, c, new Vector2(1, 0));
        V(b - n, c, new Vector2(0, 1));
        V(b + n, c, new Vector2(1, 1));
        tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
        tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
    }

    public void Polyline(IList<Vector2> pts, float width, Color c, bool closed = false)
    {
        int n = pts.Count;
        for (int i = 0; i + 1 < n; i++) Line(pts[i], pts[i + 1], width, c);
        if (closed && n > 2) Line(pts[n - 1], pts[0], width, c);
    }

    public void Ellipse(Vector2 center, float rx, float ry, float width, Color c, int segments = 64)
    {
        Vector2 prev = center + new Vector2(rx, 0);
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector2 p = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            Line(prev, p, width, c);
            prev = p;
        }
    }

    // 볼록한 면 (사각형 등). 단색
    public void Polygon(IList<Vector2> pts, Color c)
    {
        int k = verts.Count;
        for (int i = 0; i < pts.Count; i++) V(pts[i], c, new Vector2(0.5f, 0.5f));
        for (int i = 1; i + 1 < pts.Count; i++)
        {
            tris.Add(k); tris.Add(k + i + 1); tris.Add(k + i);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for (int i = 0; i < verts.Count; i++) vh.AddVert(verts[i]);
        for (int i = 0; i + 2 < tris.Count; i += 3) vh.AddTriangle(tris[i], tris[i + 1], tris[i + 2]);
    }
}
