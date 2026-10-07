using UnityEditor;
using UnityEngine;

// StoryChapter 인스펙터 위쪽에 미리보기 패널을 붙인다.
// 컷·대사를 골라 열린 씬에 띄워 보고, 값을 바꾸면 바로 다시 그린다. 그 컷부터 Play도 할 수 있다.
[CustomEditor(typeof(StoryChapter))]
public class StoryChapterEditor : Editor
{
    private const string CutKey = "CapDemo.StoryPreview.Cut";
    private const string LineKey = "CapDemo.StoryPreview.Line";
    private const string LiveKey = "CapDemo.StoryPreview.Live";

    public override void OnInspectorGUI()
    {
        var chapter = (StoryChapter)target;
        DrawPreviewPanel(chapter);
        EditorGUILayout.Space(6);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();

        // 값을 고치는 중에도 씬 미리보기가 따라오게 한다
        if (changed && SessionState.GetBool(LiveKey, true) && StoryPreview.IsActive)
            ShowSelected(chapter, StoryPreview.ActiveManager);
    }

    private void DrawPreviewPanel(StoryChapter chapter)
    {
        using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        EditorGUILayout.LabelField("씬 미리보기", EditorStyles.boldLabel);

        var manager = Object.FindAnyObjectByType<StoryManager>(FindObjectsInactive.Include);
        if (!manager)
        {
            EditorGUILayout.HelpBox("열린 씬에 StoryCanvas(StoryManager)가 없어요. Tools > CapDemo > 스토리 > 스토리 UI 만들기로 먼저 만들어 주세요.", MessageType.Info);
            return;
        }
        if (chapter.Cuts.Count == 0)
        {
            EditorGUILayout.HelpBox("Cuts에 컷을 하나 이상 추가하면 미리볼 수 있어요.", MessageType.Info);
            return;
        }

        // 컷·대사 고르기 (Memo와 대사 앞부분으로 목록을 만든다)
        int cut = Mathf.Clamp(SessionState.GetInt(CutKey, 0), 0, chapter.Cuts.Count - 1);
        var cutLabels = new string[chapter.Cuts.Count];
        for (int i = 0; i < cutLabels.Length; i++)
        {
            var c = chapter.Cuts[i];
            string memo = string.IsNullOrEmpty(c.Memo) ? (c.BlackScreen ? "검은 화면" : c.Illustration ? c.Illustration.name : "") : c.Memo;
            cutLabels[i] = $"{i}: {memo}";
        }
        cut = EditorGUILayout.Popup("컷", cut, cutLabels);

        var lines = chapter.Cuts[cut].Lines;
        int line = 0;
        if (lines.Count > 0)
        {
            line = Mathf.Clamp(SessionState.GetInt(LineKey, 0), 0, lines.Count - 1);
            var lineLabels = new string[lines.Count];
            for (int i = 0; i < lineLabels.Length; i++)
            {
                string text = (lines[i].Text ?? "").Replace("\n", " ");
                if (text.Length > 24) text = text.Substring(0, 24) + "…";
                string speaker = string.IsNullOrEmpty(lines[i].Speaker) ? "" : lines[i].Speaker + ": ";
                lineLabels[i] = $"{i}: {speaker}{text}".Replace("/", "∕"); // Popup은 /를 하위 메뉴로 읽는다
            }
            line = EditorGUILayout.Popup("대사", line, lineLabels);
        }
        else
        {
            EditorGUILayout.LabelField("대사", "이 컷에는 대사가 없어요");
        }

        bool selectionChanged = cut != SessionState.GetInt(CutKey, 0) || line != SessionState.GetInt(LineKey, 0);
        SessionState.SetInt(CutKey, cut);
        SessionState.SetInt(LineKey, line);

        bool live = EditorGUILayout.ToggleLeft("값을 바꾸면 미리보기 자동 갱신", SessionState.GetBool(LiveKey, true));
        SessionState.SetBool(LiveKey, live);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(StoryPreview.IsActive ? "다시 그리기" : "씬에서 미리보기")) ShowSelected(chapter, manager);
            using (new EditorGUI.DisabledScope(!StoryPreview.IsActive))
                if (GUILayout.Button("미리보기 끄기")) StoryPreview.End();
        }
        if (selectionChanged && StoryPreview.IsActive) ShowSelected(chapter, manager);

        if (StoryPreview.IsActive)
            EditorGUILayout.HelpBox("미리보기 중이에요. 이 상태에서 씬 오브젝트 위치를 옮기면 미리보기를 끌 때 원래대로 돌아가요. 레이아웃은 미리보기를 끈 뒤 고쳐 주세요.", MessageType.Warning);

        EditorGUILayout.Space(4);
        DrawPlayFromCut(chapter, manager, cut);
    }

    private static void DrawPlayFromCut(StoryChapter chapter, StoryManager manager, int cut)
    {
        if (manager.Chapter != chapter)
        {
            EditorGUILayout.HelpBox("씬의 StoryCanvas에 연결된 Chapter가 이 파일이 아니에요.", MessageType.None);
            if (GUILayout.Button("StoryCanvas에 이 Chapter 연결"))
            {
                var so = new SerializedObject(manager);
                so.FindProperty("chapter").objectReferenceValue = chapter;
                so.ApplyModifiedProperties();
            }
            return;
        }

        var managerSo = new SerializedObject(manager);
        var startCut = managerSo.FindProperty("startCut");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"컷 {cut}부터 Play"))
            {
                StoryPreview.End();
                startCut.intValue = cut;
                managerSo.ApplyModifiedProperties();
                EditorApplication.EnterPlaymode();
            }
            using (new EditorGUI.DisabledScope(startCut.intValue == 0))
                if (GUILayout.Button("처음부터 Play로 되돌리기"))
                {
                    startCut.intValue = 0;
                    managerSo.ApplyModifiedProperties();
                }
        }
        if (startCut.intValue != 0)
            EditorGUILayout.LabelField($"지금 Play하면 컷 {startCut.intValue}부터 시작해요 (StoryManager의 Start Cut).", EditorStyles.miniLabel);
    }

    private static void ShowSelected(StoryChapter chapter, StoryManager manager)
    {
        StoryPreview.Show(manager, chapter, SessionState.GetInt(CutKey, 0), SessionState.GetInt(LineKey, 0));
    }
}
