using UnityEngine;

// 점수, 콤보, 판정, 박자와 키 안내 등 리듬게임 HUD만 담당한다.
[DisallowMultipleComponent]
public class RhythmGameUI : MonoBehaviour
{
    private readonly string[] laneLabels = { "A", "S", "D", "J", "K", "L" };

    private RhythmClock rhythmClock;
    private JudgementScoreManager scoreManager;
    private RhythmGameController controller;

    private float judgementTimer;
    private string judgementText = "READY";

    public void Initialize(
        RhythmClock newRhythmClock,
        JudgementScoreManager newScoreManager,
        RhythmGameController newController)
    {
        rhythmClock = newRhythmClock;
        scoreManager = newScoreManager;
        controller = newController;
    }

    public void ShowJudgement(string text)
    {
        judgementText = text;
        judgementTimer = 0.45f;
    }

    private void Update()
    {
        if (judgementTimer > 0.0f)
        {
            judgementTimer -= Time.unscaledDeltaTime;
        }
    }

    private void OnGUI()
    {
        if (rhythmClock == null || scoreManager == null || controller == null)
        {
            return;
        }

        DrawScreenRect(
            new Rect(14, 12, 365, 112),
            new Color(0.01f, 0.02f, 0.06f, 0.78f));
        DrawScreenRect(
            new Rect(14, 12, 5, 112),
            new Color(0.05f, 0.95f, 1.0f, 0.95f));
        DrawScreenRect(
            new Rect(Screen.width - 230, 18, 210, 34),
            new Color(0.05f, 0.01f, 0.08f, 0.72f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = Mathf.Max(20, Screen.height / 32);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.30f, 1.0f, 0.95f);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = Mathf.Max(15, Screen.height / 48);
        infoStyle.normal.textColor = new Color(0.60f, 0.95f, 1.0f);

        GUI.Label(new Rect(28, 18, 500, 45), "CYBER//RHYTHM", titleStyle);
        GUI.Label(
            new Rect(24, 58, 450, 28),
            "SCORE  " + scoreManager.Score,
            infoStyle);
        GUI.Label(
            new Rect(24, 84, 450, 28),
            "COMBO  " + scoreManager.Combo
                + "    BEST  " + scoreManager.MaxCombo,
            infoStyle);

        GUIStyle statusStyle = new GUIStyle(infoStyle);
        statusStyle.alignment = TextAnchor.MiddleCenter;
        statusStyle.normal.textColor = new Color(1.0f, 0.25f, 0.80f);
        GUI.Label(
            new Rect(Screen.width - 230, 20, 210, 28),
            "BEAT // " + rhythmClock.CurrentBeat.ToString("0.0"),
            statusStyle);

        if (judgementTimer > 0.0f || judgementText == "READY")
        {
            GUIStyle judgeStyle = new GUIStyle(titleStyle);
            judgeStyle.alignment = TextAnchor.MiddleCenter;
            judgeStyle.fontSize = Mathf.Max(28, Screen.height / 20);
            judgeStyle.normal.textColor = judgementText == "MISS"
                ? new Color(1.0f, 0.35f, 0.45f)
                : new Color(0.40f, 1.0f, 0.90f);
            GUI.Label(
                new Rect(0, Screen.height * 0.45f, Screen.width, 70),
                judgementText,
                judgeStyle);
        }

        DrawKeyHints();
    }

    private static void DrawScreenRect(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawKeyHints()
    {
        GUIStyle keyStyle = new GUIStyle(GUI.skin.box);
        keyStyle.fontSize = Mathf.Max(16, Screen.height / 42);
        keyStyle.fontStyle = FontStyle.Bold;
        keyStyle.alignment = TextAnchor.MiddleCenter;
        keyStyle.normal.textColor = Color.white;

        float totalWidth = Mathf.Min(Screen.width * 0.72f, 720.0f);
        float keyWidth = totalWidth / RhythmGameController.LaneCount;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - 62.0f;

        for (int lane = 0; lane < RhythmGameController.LaneCount; lane++)
        {
            GUI.backgroundColor = controller.IsLaneHeldForView(lane)
                ? (lane < 3
                    ? new Color(0.05f, 1.0f, 1.0f)
                    : new Color(1.0f, 0.08f, 0.78f))
                : new Color(0.16f, 0.12f, 0.28f);

            GUI.Box(
                new Rect(startX + keyWidth * lane + 3.0f, y, keyWidth - 6.0f, 42.0f),
                laneLabels[lane],
                keyStyle);
        }

        GUI.backgroundColor = Color.white;
    }
}
