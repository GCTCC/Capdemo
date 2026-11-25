using UnityEditor;
using UnityEngine;

// 공통 조건(Condition) 한 줄을 Inspector에 그린다. 고른 종류에 필요한 칸만 보인다.
[CustomPropertyDrawer(typeof(Condition))]
public class ConditionDrawer : PropertyDrawer
{
    private const float Gap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight + Gap;
        return line * (1 + ExtraLines(property));
    }

    private static int ExtraLines(SerializedProperty property)
    {
        switch ((Condition.Kind)property.FindPropertyRelative("kind").enumValueIndex)
        {
            case Condition.Kind.TutorialStep: return 1;
            case Condition.Kind.QuestState:
                var state = (Condition.QuestStateKind)property.FindPropertyRelative("questState").enumValueIndex;
                return state == Condition.QuestStateKind.Active ? 2 : 1;
            default: return 1;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float h = EditorGUIUtility.singleLineHeight;
        var row = new Rect(position.x, position.y, position.width, h);

        // 첫 줄: 종류 + 아님
        var kind = property.FindPropertyRelative("kind");
        var not = property.FindPropertyRelative("not");
        const float notWidth = 52f;
        EditorGUI.PropertyField(new Rect(row.x, row.y, row.width - notWidth - 4f, h), kind, GUIContent.none);
        not.boolValue = EditorGUI.ToggleLeft(new Rect(row.xMax - notWidth, row.y, notWidth, h), "아님", not.boolValue);

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel++;
        row.y += h + Gap;
        switch ((Condition.Kind)kind.enumValueIndex)
        {
            case Condition.Kind.TutorialStep:
                HalfPair(row, property.FindPropertyRelative("step"), property.FindPropertyRelative("compare"));
                break;
            case Condition.Kind.QuestState:
                HalfPair(row, property.FindPropertyRelative("quest"), property.FindPropertyRelative("questState"));
                if ((Condition.QuestStateKind)property.FindPropertyRelative("questState").enumValueIndex == Condition.QuestStateKind.Active)
                {
                    row.y += h + Gap;
                    EditorGUI.PropertyField(row, property.FindPropertyRelative("questStep"), new GUIContent("단계 (0 = 상관없음)"));
                }
                break;
            case Condition.Kind.NpcMet:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("npcId"), new GUIContent("NPC ID"));
                break;
            case Condition.Kind.DialogueSeen:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("dialogue"), new GUIContent("대화·스토리"));
                break;
            case Condition.Kind.GuideSeen:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("guide"), new GUIContent("안내"));
                break;
            case Condition.Kind.AreaVisited:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("area"), new GUIContent("구역"));
                break;
            case Condition.Kind.ChaptersCleared:
                EditorGUI.PropertyField(row, property.FindPropertyRelative("chapters"), new GUIContent("챕터 수 이상"));
                break;
        }
        EditorGUI.indentLevel = oldIndent;
        EditorGUI.EndProperty();
    }

    private static void HalfPair(Rect row, SerializedProperty a, SerializedProperty b)
    {
        float half = (row.width - 4f) * 0.6f;
        EditorGUI.PropertyField(new Rect(row.x, row.y, half, row.height), a, GUIContent.none);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUI.PropertyField(new Rect(row.x + half + 4f, row.y, row.width - half - 4f, row.height), b, GUIContent.none);
        EditorGUI.indentLevel = indent;
    }
}
