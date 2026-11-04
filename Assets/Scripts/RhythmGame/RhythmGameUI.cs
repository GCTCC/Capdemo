using UnityEngine;

// 첨부 UI의 먹색·아이보리·적색 톤으로 점수, 콤보, 판정과 키 안내를 그린다.
[DisallowMultipleComponent]
public class RhythmGameUI : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.018f, 0.016f, 0.015f, 0.94f);
    private static readonly Color InkSoft = new Color(0.055f, 0.048f, 0.043f, 0.92f);
    private static readonly Color Cream = new Color(0.94f, 0.88f, 0.75f, 1.0f);
    private static readonly Color CreamDim = new Color(0.72f, 0.67f, 0.57f, 1.0f);
    private static readonly Color DeepRed = new Color(0.55f, 0.075f, 0.08f, 1.0f);
    private static readonly Color Cyan = new Color(0.13f, 0.92f, 0.90f, 1.0f);
    private static readonly Color Magenta = new Color(1.0f, 0.18f, 0.58f, 1.0f);

    private readonly string[] laneLabels = { "A", "S", "D", "J", "K", "L" };

    private RhythmClock rhythmClock;
    private JudgementScoreManager scoreManager;
    private RhythmGameController controller;

    private float judgementTimer;
    private string judgementText = "";
    private bool isVisible;

    public void Initialize(
        RhythmClock newRhythmClock,
        JudgementScoreManager newScoreManager,
        RhythmGameController newController)
    {
        rhythmClock = newRhythmClock;
        scoreManager = newScoreManager;
        controller = newController;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (visible)
        {
            judgementText = "READY";
            judgementTimer = 0.65f;
        }
        else
        {
            judgementText = "";
            judgementTimer = 0.0f;
        }
    }

    public void ShowJudgement(string text)
    {
        judgementText = text;
        judgementTimer = 0.45f;
    }

    private void Update()
    {
        if (judgementTimer <= 0.0f)
        {
            return;
        }

        judgementTimer -= Time.unscaledDeltaTime;
        if (judgementTimer <= 0.0f)
        {
            judgementText = "";
        }
    }

    private void OnGUI()
    {
        if (!isVisible
            || rhythmClock == null
            || scoreManager == null
            || controller == null)
        {
            return;
        }

        GUI.depth = 0;
        DrawProgressRail();
        DrawSongPanel();
        DrawScorePanel();
        DrawCombo();
        DrawJudgement();
        DrawKeyHints();
    }

    private void DrawProgressRail()
    {
        DrawRect(new Rect(0.0f, 0.0f, Screen.width, 5.0f), Ink);
        DrawRect(
            new Rect(0.0f, 0.0f, Screen.width * rhythmClock.NormalizedProgress, 5.0f),
            DeepRed);

        float headX = Screen.width * rhythmClock.NormalizedProgress;
        DrawRect(new Rect(headX - 2.0f, 0.0f, 4.0f, 5.0f), Cream);
    }

    private void DrawSongPanel()
    {
        float width = Mathf.Min(430.0f, Screen.width * 0.36f);
        Rect panel = new Rect(18.0f, 18.0f, width, 82.0f);
        DrawPanel(panel, DeepRed);
        DrawRect(new Rect(panel.x, panel.y, 8.0f, panel.height), DeepRed);

        GUIStyle modeStyle = MakeLabelStyle(
            Mathf.Max(12, Screen.height / 68),
            CreamDim,
            FontStyle.Bold,
            TextAnchor.MiddleLeft);
        GUIStyle titleStyle = MakeLabelStyle(
            Mathf.Max(19, Screen.height / 43),
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleLeft);
        titleStyle.wordWrap = false;

        GUI.Label(
            new Rect(panel.x + 22.0f, panel.y + 8.0f, panel.width - 34.0f, 24.0f),
            controller.CurrentModeLabel + "  //  TRACK",
            modeStyle);
        Rect titleRect = new Rect(
            panel.x + 22.0f,
            panel.y + 30.0f,
            panel.width - 34.0f,
            42.0f);
        GUI.Label(
            titleRect,
            TruncateToWidth(controller.CurrentSongTitle, titleStyle, titleRect.width),
            titleStyle);

        DrawScratchMarks(panel, 5);
    }

    private void DrawScorePanel()
    {
        float width = Mathf.Min(310.0f, Screen.width * 0.28f);
        Rect panel = new Rect(Screen.width - width - 18.0f, 18.0f, width, 82.0f);
        DrawPanel(panel, DeepRed);

        GUIStyle captionStyle = MakeLabelStyle(
            Mathf.Max(12, Screen.height / 68),
            CreamDim,
            FontStyle.Bold,
            TextAnchor.MiddleRight);
        GUIStyle scoreStyle = MakeLabelStyle(
            Mathf.Max(25, Screen.height / 31),
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleRight);

        GUI.Label(
            new Rect(panel.x + 12.0f, panel.y + 7.0f, panel.width - 24.0f, 23.0f),
            "SCORE  //  BEAT " + rhythmClock.CurrentBeat.ToString("0.0"),
            captionStyle);
        GUI.Label(
            new Rect(panel.x + 12.0f, panel.y + 27.0f, panel.width - 24.0f, 45.0f),
            scoreManager.Score.ToString("D8"),
            scoreStyle);

        DrawScratchMarks(panel, 4);
    }

    private void DrawCombo()
    {
        if (scoreManager.Combo <= 0)
        {
            return;
        }

        GUIStyle captionStyle = MakeLabelStyle(
            Mathf.Max(12, Screen.height / 70),
            CreamDim,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        GUIStyle comboStyle = MakeLabelStyle(
            Mathf.Max(35, Screen.height / 22),
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);

        float width = 220.0f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, 16.0f, width, 86.0f);
        DrawRect(new Rect(panel.x + 34.0f, panel.y, panel.width - 68.0f, 2.0f), DeepRed);
        GUI.Label(new Rect(panel.x, panel.y + 2.0f, panel.width, 23.0f), "COMBO", captionStyle);
        GUI.Label(
            new Rect(panel.x, panel.y + 21.0f, panel.width, 59.0f),
            scoreManager.Combo.ToString(),
            comboStyle);
    }

    private void DrawJudgement()
    {
        if (string.IsNullOrEmpty(judgementText))
        {
            return;
        }

        Color judgementColor = GetJudgementColor();
        GUIStyle shadowStyle = MakeLabelStyle(
            Mathf.Max(30, Screen.height / 20),
            new Color(0.0f, 0.0f, 0.0f, 0.72f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        GUIStyle judgementStyle = new GUIStyle(shadowStyle);
        judgementStyle.normal.textColor = judgementColor;

        // 플레이필드 위쪽을 가리지 않도록 판정선 쪽으로 내린다.
        Rect rect = new Rect(0.0f, Screen.height * 0.60f, Screen.width, 74.0f);
        GUI.Label(new Rect(rect.x + 3.0f, rect.y + 4.0f, rect.width, rect.height),
            judgementText, shadowStyle);
        GUI.Label(rect, judgementText, judgementStyle);
        DrawRect(
            new Rect(Screen.width * 0.43f, rect.yMax - 4.0f, Screen.width * 0.14f, 2.0f),
            judgementColor);
    }

    private Color GetJudgementColor()
    {
        switch (judgementText)
        {
            case "MISS":
                return new Color(0.62f, 0.64f, 0.67f, 1.0f);
            case "GOOD":
                return new Color(0.95f, 0.82f, 0.38f, 1.0f);
            case "GREAT":
                return Magenta;
            case "PERFECT":
                return Cyan;
            default:
                return Cream;
        }
    }

    private void DrawKeyHints()
    {
        float totalWidth = Mathf.Min(Screen.width * 0.66f, 690.0f);
        float keyWidth = totalWidth / RhythmGameController.LaneCount;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - 58.0f;

        GUIStyle keyStyle = MakeLabelStyle(
            Mathf.Max(15, Screen.height / 48),
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);

        for (int lane = 0; lane < RhythmGameController.LaneCount; lane++)
        {
            bool held = controller.IsLaneHeldForView(lane);
            Color accent = lane < 3 ? Cyan : Magenta;
            Rect keyRect = new Rect(
                startX + keyWidth * lane + 4.0f,
                y,
                keyWidth - 8.0f,
                38.0f);

            DrawRect(keyRect, held ? accent : InkSoft);
            DrawRect(new Rect(keyRect.x, keyRect.yMax - 3.0f, keyRect.width, 3.0f),
                held ? Cream : new Color(accent.r, accent.g, accent.b, 0.45f));

            GUIStyle laneStyle = new GUIStyle(keyStyle);
            laneStyle.normal.textColor = held ? Ink : Cream;
            GUI.Label(keyRect, laneLabels[lane], laneStyle);
        }
    }

    private static GUIStyle MakeLabelStyle(
        int fontSize,
        Color color,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.fontStyle = fontStyle;
        style.alignment = alignment;
        style.clipping = TextClipping.Clip;
        style.normal.textColor = color;
        return style;
    }

    // IMGUI에는 말줄임표가 없어 실제 렌더링 폭을 재며 한 줄 문자열을 줄인다.
    private static string TruncateToWidth(string text, GUIStyle style, float maxWidth)
    {
        string singleLine = string.IsNullOrEmpty(text)
            ? ""
            : text.Replace('\n', ' ').Replace('\r', ' ');

        if (style.CalcSize(new GUIContent(singleLine)).x <= maxWidth)
        {
            return singleLine;
        }

        const string ellipsis = "...";
        int length = singleLine.Length;
        while (length > 0)
        {
            string candidate = singleLine.Substring(0, length).TrimEnd() + ellipsis;
            if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
            {
                return candidate;
            }

            length--;
        }

        return ellipsis;
    }

    private static void DrawPanel(Rect rect, Color accent)
    {
        DrawRect(new Rect(rect.x + 5.0f, rect.y + 6.0f, rect.width, rect.height),
            new Color(0.0f, 0.0f, 0.0f, 0.42f));
        DrawRect(rect, Ink);
        DrawRect(new Rect(rect.x, rect.y, rect.width, 2.0f), accent);
        DrawRect(new Rect(rect.x, rect.yMax - 1.0f, rect.width, 1.0f),
            new Color(Cream.r, Cream.g, Cream.b, 0.32f));
    }

    private static void DrawScratchMarks(Rect rect, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = rect.x + rect.width * (0.12f + 0.17f * i);
            float width = 9.0f + (i % 3) * 7.0f;
            DrawRect(
                new Rect(x, rect.yMax - 8.0f - (i % 2) * 3.0f, width, 1.0f),
                new Color(Cream.r, Cream.g, Cream.b, 0.16f));
        }
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
