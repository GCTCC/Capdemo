using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Play 없이 열린 씬의 StoryCanvas에 특정 컷·대사를 띄워 보는 에디터 미리보기.
// 시작할 때 화면 상태를 기억해 두고, 끌 때(또는 Play·씬 저장·스크립트 재컴파일 직전) 그대로 되돌린다.
[InitializeOnLoad]
public static class StoryPreview
{
    private static StoryManager activeManager;
    private static readonly List<GraphicState> graphics = new List<GraphicState>();
    private static readonly List<TextState> texts = new List<TextState>();
    private static GameObject namePlate;
    private static bool namePlateActive;
    private static CanvasGroup overlay;
    private static float overlayAlpha;

    public static bool IsActive => activeManager;
    public static StoryManager ActiveManager => activeManager;

    static StoryPreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) End();
        };
        EditorSceneManager.sceneSaving += (scene, path) => End();
        EditorSceneManager.sceneClosing += (scene, removing) => End();
        AssemblyReloadEvents.beforeAssemblyReload += End;
    }

    // chapter의 cutIndex번 컷, lineIndex번 대사를 manager의 화면에 띄운다
    public static void Show(StoryManager manager, StoryChapter chapter, int cutIndex, int lineIndex)
    {
        if (!manager || !chapter || chapter.Cuts.Count == 0) return;
        if (activeManager != manager)
        {
            End();
            Begin(manager);
        }
        else
        {
            Restore(); // 매번 원래 상태에서 다시 그려야 Keep(이전 유지) 값이 정확하다
        }

        var illustration = manager.Illustration;
        var dialogue = manager.Dialogue;
        cutIndex = Mathf.Clamp(cutIndex, 0, chapter.Cuts.Count - 1);

        if (illustration)
        {
            var (sprite, framing) = chapter.ResolveIllustration(cutIndex);
            illustration.ShowImmediate(sprite, framing);
        }

        if (dialogue)
        {
            dialogue.ForgetStandingBase();
            dialogue.ResetStanding();
            dialogue.Clear();

            // 앞 컷·앞 줄의 스탠딩 변화를 순서대로 따라간 뒤 대상 줄을 보여 준다
            for (int c = 0; c < cutIndex; c++)
                foreach (var line in chapter.Cuts[c].Lines)
                    dialogue.ApplyStanding(line);

            var lines = chapter.Cuts[cutIndex].Lines;
            if (lines.Count > 0)
            {
                lineIndex = Mathf.Clamp(lineIndex, 0, lines.Count - 1);
                for (int l = 0; l < lineIndex; l++) dialogue.ApplyStanding(lines[l]);
                dialogue.ShowInstant(lines[lineIndex]);
            }
        }

        if (overlay) overlay.alpha = 0f;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    public static void End()
    {
        if (!activeManager) return;
        Restore();
        if (activeManager.Dialogue) activeManager.Dialogue.ForgetStandingBase();
        if (activeManager.Illustration) activeManager.Illustration.RemoveBackdrop();
        activeManager = null;
        graphics.Clear();
        texts.Clear();
        namePlate = null;
        overlay = null;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void Begin(StoryManager manager)
    {
        activeManager = manager;
        graphics.Clear();
        texts.Clear();

        if (manager.Illustration)
        {
            var view = new SerializedObject(manager.Illustration);
            Capture(view.FindProperty("front").objectReferenceValue as Graphic);
            Capture(view.FindProperty("back").objectReferenceValue as Graphic);
        }

        if (manager.Dialogue)
        {
            var box = new SerializedObject(manager.Dialogue);
            Capture(box.FindProperty("standing").objectReferenceValue as Graphic);
            Capture(box.FindProperty("nextIndicator").objectReferenceValue as Graphic);
            Capture(box.FindProperty("nameText").objectReferenceValue as TMP_Text);
            Capture(box.FindProperty("bodyText").objectReferenceValue as TMP_Text);
            namePlate = box.FindProperty("namePlate").objectReferenceValue as GameObject;
            if (namePlate) namePlateActive = namePlate.activeSelf;
        }

        overlay = manager.FadeOverlay;
        if (overlay) overlayAlpha = overlay.alpha;
    }

    private static void Restore()
    {
        foreach (var g in graphics) g.Restore();
        foreach (var t in texts) t.Restore();
        if (namePlate) namePlate.SetActive(namePlateActive);
        if (overlay) overlay.alpha = overlayAlpha;
    }

    private static void Capture(Graphic graphic)
    {
        if (!graphic) return;
        if (graphic is TMP_Text text) Capture(text);
        else graphics.Add(new GraphicState(graphic));
    }

    private static void Capture(TMP_Text text)
    {
        if (text) texts.Add(new TextState(text));
    }

    private class GraphicState
    {
        private readonly Graphic graphic;
        private readonly Sprite sprite;
        private readonly bool preserveAspect;
        private readonly Color color;
        private readonly bool enabled;
        private readonly int sibling;
        private readonly Vector2 anchorMin, anchorMax, pivot, position, size;
        private readonly Vector3 scale;

        public GraphicState(Graphic graphic)
        {
            this.graphic = graphic;
            if (graphic is Image image)
            {
                sprite = image.sprite;
                preserveAspect = image.preserveAspect;
            }
            color = graphic.color;
            enabled = graphic.enabled;
            sibling = graphic.transform.GetSiblingIndex();
            var rect = graphic.rectTransform;
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            position = rect.anchoredPosition;
            size = rect.sizeDelta;
            scale = rect.localScale;
        }

        public void Restore()
        {
            if (!graphic) return;
            if (graphic is Image image)
            {
                image.sprite = sprite;
                image.preserveAspect = preserveAspect;
            }
            graphic.color = color;
            graphic.enabled = enabled;
            graphic.transform.SetSiblingIndex(sibling);
            var rect = graphic.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = scale;
        }
    }

    private class TextState
    {
        private readonly TMP_Text text;
        private readonly string value;
        private readonly int maxVisible;
        private readonly bool enabled;

        public TextState(TMP_Text text)
        {
            this.text = text;
            value = text.text;
            maxVisible = text.maxVisibleCharacters;
            enabled = text.enabled;
        }

        public void Restore()
        {
            if (!text) return;
            text.text = value;
            text.maxVisibleCharacters = maxVisible;
            text.enabled = enabled;
        }
    }
}
