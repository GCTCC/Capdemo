using UnityEngine;
using UnityEngine.UI;

// 이미지에 가로 그라데이션을 입힌다 (왼쪽 색 → 오른쪽 색)
[RequireComponent(typeof(Graphic))]
public class HorizontalGradientV2 : BaseMeshEffect
{
    [SerializeField] private Color left = Color.white;
    [SerializeField] private Color right = Color.white;

    public void Set(Color newLeft, Color newRight)
    {
        left = newLeft;
        right = newRight;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var v = new UIVertex();
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            min = Mathf.Min(min, v.position.x);
            max = Mathf.Max(max, v.position.x);
        }
        float w = Mathf.Max(0.001f, max - min);
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color c = Color.Lerp(left, right, (v.position.x - min) / w);
            v.color = c * (Color)v.color;
            vh.SetUIVertex(v, i);
        }
    }
}
