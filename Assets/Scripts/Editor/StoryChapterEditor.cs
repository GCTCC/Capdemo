using UnityEditor;
using UnityEngine;

// StoryChapter 인스펙터 위쪽에 미리보기 패널을 붙인다.
// 컷·대사를 골라 열린 씬에 띄워 보고, 값을 바꾸면 바로 다시 그린다. 그 컷부터 Play도 할 수 있다.
// 소개 카드가 있는 줄을 미리보면 씬 뷰에 노란 네모(꼬리 끝 자리)가 생기고, 끌면 그 줄의 Intro Card > Position이 바뀐다.
// 홀로그램 구간의 줄을 미리보면 청록 네모(홀로그램 가운데)가 생기고, 끌면 그 구간 Holo Start 줄의 Holo Position이 바뀐다.
[CustomEditor(typeof(StoryChapter))]
public class StoryChapterEditor : Editor
{
    private const string CutKey = "CapDemo.StoryPreview.Cut";
    private const string LineKey = "CapDemo.StoryPreview.Line";
    private const string LiveKey = "CapDemo.StoryPreview.Live";

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    // 되돌리기로 값이 바뀌어도 미리보기가 따라오게
    private void OnUndoRedo()
    {
        if (target && StoryPreview.IsActive) ShowSelected((StoryChapter)target, StoryPreview.ActiveManager);
    }

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

        if (StoryPreview.IsActive && lines.Count > 0 && lines[line].HasIntroCard)
            EditorGUILayout.HelpBox("이 줄에 소개 카드가 있어요. 씬 뷰에서 노란 네모(꼬리 끝)를 끌면 카드 위치(Intro Card > Position)가 바뀌어요. Ctrl+Z로 되돌릴 수 있어요.", MessageType.Info);

        if (lines.Count > 0 && chapter.TryGetHoloStart(cut, line, out int holoCut, out int holoLine))
        {
            if (!manager.Holo)
                EditorGUILayout.HelpBox("이 줄은 홀로그램 구간인데, 씬의 StoryCanvas에 홀로그램 폰이 없어요. Tools > CapDemo > 스토리 > 홀로그램 폰 붙이기를 먼저 눌러 주세요.", MessageType.Warning);
            else if (StoryPreview.IsActive)
            {
                EditorGUILayout.HelpBox($"이 줄은 홀로그램 구간이에요 (시작: 컷 {holoCut} / 대사 {holoLine}). 씬 뷰에서 청록 네모를 끌면 그 시작 줄의 Holo Position이 바뀌어요. 크기는 Holo Scale. Ctrl+Z로 되돌릴 수 있어요.\n" +
                                        "화면(캔버스)은 씬 뷰에서 아주 크게 그려져서, 맵 씬에서는 안 보일 수 있어요. 아래 버튼으로 홀로그램 쪽으로 이동하세요.", MessageType.Info);
                if (GUILayout.Button("씬 뷰를 홀로그램 쪽으로 이동")) FrameHolo(manager.Holo);
            }
        }

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

    // 소개 카드 위치잡기: 미리보기 중인 줄에 카드가 있으면 꼬리 끝에 끌 수 있는 네모를 그린다
    private void OnSceneGUI(SceneView view)
    {
        var chapter = target as StoryChapter;
        if (!chapter || !StoryPreview.IsActive) return;
        var manager = StoryPreview.ActiveManager;
        DrawHoloHandle(chapter, manager);
        var card = manager && manager.Dialogue ? manager.Dialogue.IntroCard : null;
        if (!card || !card.gameObject.activeInHierarchy) return;

        int cut = SessionState.GetInt(CutKey, 0);
        int line = SessionState.GetInt(LineKey, 0);
        if (cut < 0 || cut >= chapter.Cuts.Count) return;
        var lines = chapter.Cuts[cut].Lines;
        if (line < 0 || line >= lines.Count || !lines[line].HasIntroCard) return;

        Vector3 tip = card.ReferenceToWorld(lines[line].IntroCard.position);
        float size = HandleUtility.GetHandleSize(tip) * 0.12f;
        Handles.color = Color.yellow;
        Handles.Label(tip + Vector3.down * size * 2.5f, "소개 카드 (끌어서 옮기기)", EditorStyles.whiteBoldLabel);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(tip, size, Vector3.zero, Handles.RectangleHandleCap);
        if (!EditorGUI.EndChangeCheck()) return;

        Vector2 refPoint = card.WorldToReference(moved);
        refPoint = new Vector2(Mathf.Round(Mathf.Clamp(refPoint.x, 0f, IntroCard.RefWidth)),
                               Mathf.Round(Mathf.Clamp(refPoint.y, 0f, IntroCard.RefHeight)));
        serializedObject.Update();
        var prop = serializedObject.FindProperty($"cuts.Array.data[{cut}].lines.Array.data[{line}].introCard.position");
        if (prop == null) return;
        prop.vector2Value = refPoint;
        serializedObject.ApplyModifiedProperties(); // Undo 기록 포함
        ShowSelected(chapter, manager);
        Repaint();
    }

    // 씬 뷰 카메라를 홀로그램(과 화면 전체)이 보이게 옮긴다. 2D 모드로 바꿔 정면에서 보이게 한다
    private static void FrameHolo(HoloPhone holo)
    {
        var view = SceneView.lastActiveSceneView;
        if (!view || !holo) return;
        var bounds = WorldBounds((RectTransform)holo.transform);
        var canvas = holo.GetComponentInParent<Canvas>(true);
        if (canvas) bounds.Encapsulate(WorldBounds((RectTransform)canvas.rootCanvas.transform));
        view.in2DMode = true;
        view.Frame(bounds, false);
        view.Repaint();
    }

    private static Bounds WorldBounds(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (var corner in corners) bounds.Encapsulate(corner);
        return bounds;
    }

    // 홀로그램 위치잡기: 미리보기 중인 줄이 홀로그램 구간이면 가운데에 끌 수 있는 청록 네모를 그린다
    private void DrawHoloHandle(StoryChapter chapter, StoryManager manager)
    {
        var holo = manager ? manager.Holo : null;
        if (!holo) return;
        int cut = SessionState.GetInt(CutKey, 0);
        int line = SessionState.GetInt(LineKey, 0);
        if (cut < 0 || cut >= chapter.Cuts.Count) return;
        if (line < 0 || line >= chapter.Cuts[cut].Lines.Count) return;
        if (!chapter.TryGetHoloStart(cut, line, out int startCut, out int startLine)) return;

        var start = chapter.Cuts[startCut].Lines[startLine];
        // 홀로그램 테두리(청록 선)를 그려서, 그림이 안 보여도 자리를 알 수 있게 한다
        var outline = new Vector3[4];
        ((RectTransform)holo.transform).GetWorldCorners(outline);
        Handles.DrawSolidRectangleWithOutline(outline, new Color(0.3f, 1f, 1f, 0.06f), new Color(0.3f, 1f, 1f, 0.9f));

        Vector3 center = holo.ReferenceToWorld(start.HoloPosition);
        float size = HandleUtility.GetHandleSize(center) * 0.15f;
        Handles.color = new Color(0.3f, 1f, 1f, 1f);
        Handles.Label(center + Vector3.down * size * 2.5f, "홀로그램 (끌어서 옮기기)", EditorStyles.whiteBoldLabel);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(center, size, Vector3.zero, Handles.RectangleHandleCap);
        if (!EditorGUI.EndChangeCheck()) return;

        Vector2 refPoint = holo.WorldToReference(moved);
        refPoint = new Vector2(Mathf.Round(Mathf.Clamp(refPoint.x, 0f, HoloPhone.RefWidth)),
                               Mathf.Round(Mathf.Clamp(refPoint.y, 0f, HoloPhone.RefHeight)));
        serializedObject.Update();
        var prop = serializedObject.FindProperty($"cuts.Array.data[{startCut}].lines.Array.data[{startLine}].holoPosition");
        if (prop == null) return;
        prop.vector2Value = refPoint;
        serializedObject.ApplyModifiedProperties(); // Undo 기록 포함
        ShowSelected(chapter, manager);
        Repaint();
    }

    private static void ShowSelected(StoryChapter chapter, StoryManager manager)
    {
        StoryPreview.Show(manager, chapter, SessionState.GetInt(CutKey, 0), SessionState.GetInt(LineKey, 0));
    }
}
