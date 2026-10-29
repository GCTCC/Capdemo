using UnityEngine;
using UnityEngine.InputSystem;

// ESC 일시정지, 일시정지 메뉴와 3초 재개 카운트다운을 담당한다.
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    private enum PauseState
    {
        Playing,
        Paused,
        Countdown
    }

    [Header("Resume Countdown")]
    [SerializeField] private float countdownSeconds = 3.0f;

    private RhythmGameController controller;
    private PauseState state = PauseState.Playing;
    private float countdownRemaining;
    private float timeScaleBeforePause = 1.0f;

    public bool IsPaused
    {
        get { return state != PauseState.Playing; }
    }

    public void Initialize(RhythmGameController newController)
    {
        controller = newController;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool escapePressed = keyboard != null
            && keyboard.escapeKey.wasPressedThisFrame;

        if (state == PauseState.Playing)
        {
            if (escapePressed
                && controller != null
                && controller.IsGameplayActive)
            {
                OpenPauseMenu();
            }

            return;
        }

        if (state == PauseState.Paused)
        {
            if (escapePressed)
            {
                BeginResumeCountdown();
            }

            return;
        }

        // Time.timeScale이 0이어도 카운트다운이 흐르도록 실시간 델타를 사용한다.
        countdownRemaining -= Time.unscaledDeltaTime;
        if (countdownRemaining <= 0.0f)
        {
            ResumeGame();
        }
    }

    public void OpenPauseMenu()
    {
        if (state != PauseState.Playing
            || controller == null
            || !controller.IsGameplayActive)
        {
            return;
        }

        timeScaleBeforePause = Time.timeScale > 0.0f ? Time.timeScale : 1.0f;
        state = PauseState.Paused;

        if (controller != null)
        {
            controller.PauseGame();
        }

        Time.timeScale = 0.0f;
    }

    public void BeginResumeCountdown()
    {
        if (state != PauseState.Paused)
        {
            return;
        }

        countdownRemaining = Mathf.Max(0.1f, countdownSeconds);
        state = PauseState.Countdown;
    }

    private void ResumeGame()
    {
        Time.timeScale = timeScaleBeforePause;

        if (controller != null)
        {
            controller.ResumeGame();
        }

        state = PauseState.Playing;
    }

    private void OnGUI()
    {
        if (state == PauseState.Playing)
        {
            return;
        }

        // 다른 IMGUI HUD보다 앞쪽에 일시정지 화면을 그린다.
        GUI.depth = -100;
        DrawScreenRect(
            new Rect(0.0f, 0.0f, Screen.width, Screen.height),
            new Color(0.005f, 0.008f, 0.025f, 0.88f));

        if (state == PauseState.Paused)
        {
            DrawPausePanel();
        }
        else
        {
            DrawCountdown();
        }
    }

    private void DrawPausePanel()
    {
        float panelWidth = Mathf.Min(520.0f, Screen.width * 0.82f);
        float panelHeight = Mathf.Min(410.0f, Screen.height * 0.72f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        DrawScreenRect(panel, new Color(0.025f, 0.035f, 0.09f, 0.98f));
        DrawScreenRect(
            new Rect(panel.x, panel.y, panel.width, 5.0f),
            new Color(0.10f, 0.95f, 1.0f));
        DrawScreenRect(
            new Rect(panel.x, panel.yMax - 5.0f, panel.width, 5.0f),
            new Color(1.0f, 0.08f, 0.72f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = Mathf.Max(34, Screen.height / 18);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.35f, 1.0f, 0.95f);

        GUIStyle guideStyle = new GUIStyle(GUI.skin.label);
        guideStyle.alignment = TextAnchor.MiddleCenter;
        guideStyle.fontSize = Mathf.Max(16, Screen.height / 46);
        guideStyle.normal.textColor = new Color(0.75f, 0.82f, 1.0f);

        GUI.Label(
            new Rect(panel.x, panel.y + 40.0f, panel.width, 70.0f),
            "PAUSE",
            titleStyle);
        GUI.Label(
            new Rect(panel.x, panel.y + 112.0f, panel.width, 34.0f),
            "ESC 키를 다시 눌러도 돌아갈 수 있습니다.",
            guideStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = Mathf.Max(20, Screen.height / 36);
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = new Color(0.25f, 1.0f, 0.95f);

        Rect resumeButton = new Rect(
            panel.x + panel.width * 0.18f,
            panel.yMax - 175.0f,
            panel.width * 0.64f,
            58.0f);

        Rect exitButton = new Rect(
            panel.x + panel.width * 0.18f,
            panel.yMax - 100.0f,
            panel.width * 0.64f,
            52.0f);

        Color oldBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.18f, 0.12f, 0.42f);
        if (GUI.Button(resumeButton, "게임으로 돌아가기", buttonStyle))
        {
            BeginResumeCountdown();
        }

        GUI.backgroundColor = new Color(0.48f, 0.08f, 0.28f);
        if (GUI.Button(exitButton, "나가기 / 곡 선택으로", buttonStyle))
        {
            ExitToLobby();
        }

        GUI.backgroundColor = oldBackgroundColor;
    }

    private void ExitToLobby()
    {
        Time.timeScale = timeScaleBeforePause;
        state = PauseState.Playing;

        if (controller != null)
        {
            controller.ReturnToLobby();
        }
    }

    private void DrawCountdown()
    {
        GUIStyle captionStyle = new GUIStyle(GUI.skin.label);
        captionStyle.alignment = TextAnchor.MiddleCenter;
        captionStyle.fontSize = Mathf.Max(26, Screen.height / 25);
        captionStyle.fontStyle = FontStyle.Bold;
        captionStyle.normal.textColor = new Color(0.70f, 0.95f, 1.0f);

        GUIStyle numberStyle = new GUIStyle(captionStyle);
        numberStyle.fontSize = Mathf.Max(96, Screen.height / 6);
        numberStyle.normal.textColor = new Color(1.0f, 0.20f, 0.75f);

        int displayedNumber = Mathf.Clamp(
            Mathf.CeilToInt(countdownRemaining),
            1,
            Mathf.CeilToInt(Mathf.Max(1.0f, countdownSeconds)));

        GUI.Label(
            new Rect(0.0f, Screen.height * 0.32f, Screen.width, 60.0f),
            "다시 시작까지",
            captionStyle);
        GUI.Label(
            new Rect(0.0f, Screen.height * 0.40f, Screen.width, 170.0f),
            displayedNumber.ToString(),
            numberStyle);
    }

    private static void DrawScreenRect(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void OnDisable()
    {
        // Play 종료나 씬 전환 때 0으로 고정된 전역 시간 배율이 남지 않게 한다.
        if (state != PauseState.Playing)
        {
            Time.timeScale = timeScaleBeforePause;
            state = PauseState.Playing;
        }
    }
}
