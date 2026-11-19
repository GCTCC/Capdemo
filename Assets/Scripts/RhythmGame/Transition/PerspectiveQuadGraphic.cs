using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전환 연출에서 쓰는 UI 그래픽 2가지. 좌표는 모두 "시안 좌표"(1600 x 900, 왼쪽 위가 0,0, 아래로 +y)다.
// 이 그래픽들은 1600 x 900 크기, 왼쪽 위 기준(pivot 0,1)인 Stage 아래에 둔다.

// 원근이 들어간 사각판 (재킷, 레코드판, 라벨, 광택 …).
// CSS의 transform: translate(x,y) perspective(1400px) rotateX rotateY rotateZ scale 과 같은 계산으로
// 400x400 판을 기울여 그린다. 원근 때문에 그림이 휘지 않게 격자로 잘게 나눠 그린다.
[RequireComponent(typeof(CanvasRenderer))]
public class PerspectiveQuadGraphic : MaskableGraphic
{
    public Texture texture;

    // 판 위치와 회전 (시안 좌표, 도)
    public Vector2 center = new Vector2(800, 450);
    public float size = 400f;      // 판 한 변 길이 (px)
    public float rotX, rotY, rotZ;
    public float perspective = 1400f;
    public Vector2 offset;         // 판 평면 안에서 밀기 (RGB 분리용, px)

    // 판의 일부만 그리기 (0~1, 왼쪽 위 기준). 라벨, 스캔 띠 등
    public Rect localRect = new Rect(0, 0, 1, 1);

    private const int Grid = 10;

    public override Texture mainTexture
    {
        get { return texture != null ? texture : s_WhiteTexture; }
    }

    public void Refresh()
    {
        SetVerticesDirty();
    }

    // 판 평면의 (u, v) [0~1, 왼쪽 위 기준] → 시안 좌표
    public Vector2 Project(float u, float v)
    {
        float half = size * 0.5f;
        Vector3 p = new Vector3((u - 0.5f) * size + offset.x, (v - 0.5f) * size + offset.y, 0f);
        // CSS 순서: 점에 scale → rotateZ → rotateY → rotateX 를 적용 (y는 아래, z는 화면 밖으로)
        p = RotZ(p, rotZ);
        p = RotY(p, rotY);
        p = RotX(p, rotX);
        float k = perspective / Mathf.Max(1f, perspective - p.z);
        return new Vector2(center.x + p.x * k, center.y + p.y * k);
    }

    private static Vector3 RotZ(Vector3 p, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector3(p.x * c - p.y * s, p.x * s + p.y * c, p.z);
    }

    private static Vector3 RotY(Vector3 p, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c);
    }

    private static Vector3 RotX(Vector3 p, float deg)
    {
        // CSS rotateX(+): 위쪽이 화면 안쪽으로 눕는다 (y 아래 좌표계)
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector3(p.x, p.y * c - p.z * s, p.y * s + p.z * c);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color32 col = color;
        for (int j = 0; j <= Grid; j++)
        {
            for (int i = 0; i <= Grid; i++)
            {
                float a = i / (float)Grid, b = j / (float)Grid;
                float u = localRect.x + a * localRect.width;
                float v = localRect.y + b * localRect.height;
                Vector2 s = Project(u, v);
                // 셰이더 UV: 이 조각 기준 0~1 (아래가 0)
                vh.AddVert(new Vector3(s.x, -s.y, 0f), col, new Vector2(a, 1f - b));
            }
        }
        int row = Grid + 1;
        for (int j = 0; j < Grid; j++)
        {
            for (int i = 0; i < Grid; i++)
            {
                int k = j * row + i;
                vh.AddTriangle(k, k + 1, k + row);
                vh.AddTriangle(k + 1, k + row + 1, k + row);
            }
        }
    }
}
