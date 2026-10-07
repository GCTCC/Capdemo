using UnityEditor;
using UnityEngine;

// [SpritePreview]가 붙은 Sprite 칸: 평소처럼 칸을 그리고, 그림이 있으면 아래에 작은 미리보기를 붙인다
[CustomPropertyDrawer(typeof(SpritePreviewAttribute))]
public class SpritePreviewDrawer : PropertyDrawer
{
    private const float PreviewHeight = 72f;
    private const float Gap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        return property.objectReferenceValue is Sprite ? line + Gap + PreviewHeight : line;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.ObjectField(fieldRect, property, typeof(Sprite), label);

        if (!(property.objectReferenceValue is Sprite sprite) || !sprite.texture) return;

        // 라벨 칸 오른쪽(값 칸 아래)에 비율을 지켜 그린다
        var area = new Rect(position.x + EditorGUIUtility.labelWidth, fieldRect.yMax + Gap,
            position.width - EditorGUIUtility.labelWidth, PreviewHeight);
        var tex = sprite.texture;
        var r = sprite.textureRect;
        float aspect = r.width / r.height;
        float w = Mathf.Min(area.width, area.height * aspect);
        float h = w / aspect;
        var draw = new Rect(area.x, area.y + (area.height - h) * 0.5f, w, h);

        EditorGUI.DrawRect(new Rect(draw.x - 1, draw.y - 1, draw.width + 2, draw.height + 2), new Color(0f, 0f, 0f, 0.35f));
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(draw, tex, uv);
    }
}
