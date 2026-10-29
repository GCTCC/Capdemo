using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 플레이필드를 Play 전에 Hierarchy에 생성하고 저장할 수 있게 하는 전용 Inspector다.
[CustomEditor(typeof(RhythmPlayField))]
public class RhythmPlayFieldEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(12.0f);
        EditorGUILayout.HelpBox(
            "Generate / Refresh Stage를 누른 뒤 씬을 저장하면 고정 오브젝트가 Play 전에 Hierarchy에 존재합니다.",
            MessageType.Info);

        RhythmPlayField playField = (RhythmPlayField)target;

        if (GUILayout.Button("Generate / Refresh Stage", GUILayout.Height(34.0f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(playField.gameObject, "Generate Rhythm Stage");
            playField.RebuildStage();
            EditorUtility.SetDirty(playField);
            EditorSceneManager.MarkSceneDirty(playField.gameObject.scene);
        }

        if (GUILayout.Button("Clear Generated Stage"))
        {
            Undo.RegisterFullObjectHierarchyUndo(playField.gameObject, "Clear Rhythm Stage");
            playField.ClearGeneratedStage();
            EditorUtility.SetDirty(playField);
            EditorSceneManager.MarkSceneDirty(playField.gameObject.scene);
        }
    }
}
