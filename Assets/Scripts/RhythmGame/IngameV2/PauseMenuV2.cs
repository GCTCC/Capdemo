using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인게임 v2 일시정지. 원본 PauseMenu를 복사해 동작(ESC, 3초 재개 카운트다운, 나가기)은 그대로 두고
// 겉모습만 홀로그램 톤 Canvas로 바꿨다. (원본 PauseMenu는 원본 컨트롤러 타입에 묶여 있어서 그대로 쓸 수 없다)
[DisallowMultipleComponent]
public class PauseMenuV2 : MonoBehaviour
{
    private enum PauseState
    {
        Playing,
        Paused,
        Countdown
    }

    [Header("Resume Countdown")]
    [SerializeField] private float countdownSeconds = 3.0f;

    private RhythmGameControllerV2 controller;
    private PauseState state = PauseState.Playing;
    private float countdownRemaining;
    private float timeScaleBeforePause = 1.0f;

    private GameObject canvasObject;
    private GameObject panel;
    private GameObject countdown;
    private TextMeshProUGUI countdownNumber, countdownRed, countdownCyan;
    private TextMeshProUGUI exitLabel;
    private RhythmSettingsPopup rhythmSettingsPopup;

    public bool IsPaused { get { return state != PauseState.Playing; } }

    public void Initialize(RhythmGameControllerV2 newController)
    {
        controller = newController;
    }

    private void Update()
    {
        if (rhythmSettingsPopup != null && rhythmSettingsPopup.IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;

        if (state == PauseState.Playing)
        {
            if (escapePressed && controller != null && controller.IsGameplayActive)
            {
                OpenPauseMenu();
            }
            return;
        }

        if (state == PauseState.Paused)
        {
            if (escapePressed) BeginResumeCountdown();
            return;
        }

        // Time.timeScale이 0이어도 카운트다운이 흐르도록 실시간 델타를 사용한다.
        countdownRemaining -= Time.unscaledDeltaTime;
        int shown = Mathf.Clamp(Mathf.CeilToInt(countdownRemaining), 1, Mathf.CeilToInt(Mathf.Max(1f, countdownSeconds)));
        string s = shown.ToString();
        if (countdownNumber && countdownNumber.text != s)
        {
            countdownNumber.text = countdownRed.text = countdownCyan.text = s;
        }
        if (countdownRemaining <= 0.0f) ResumeGame();
    }

    public void OpenPauseMenu()
    {
        if (state != PauseState.Playing || controller == null || !controller.IsGameplayActive) return;

        timeScaleBeforePause = Time.timeScale > 0.0f ? Time.timeScale : 1.0f;
        state = PauseState.Paused;
        controller.PauseGame();
        Time.timeScale = 0.0f;
        ShowView();
    }

    public void BeginResumeCountdown()
    {
        if (state != PauseState.Paused || (rhythmSettingsPopup != null && rhythmSettingsPopup.IsOpen)) return;
        countdownRemaining = Mathf.Max(0.1f, countdownSeconds);
        state = PauseState.Countdown;
        ShowView();
    }

    private void ResumeGame()
    {
        Time.timeScale = timeScaleBeforePause;
        if (controller != null) controller.ResumeGame();
        state = PauseState.Playing;
        ShowView();
    }

    private void ExitToLobby()
    {
        Time.timeScale = timeScaleBeforePause;
        state = PauseState.Playing;
        ShowView();
        if (controller != null) controller.ReturnToLobby();
    }

    private void OpenRhythmSettings()
    {
        if (state != PauseState.Paused) return;
        var ui = GetComponent<RhythmGameUIV2>();
        if (rhythmSettingsPopup == null)
            rhythmSettingsPopup = RhythmSettingsPopup.Create(transform, ui != null ? ui.FontKorean : null);
        rhythmSettingsPopup.Open();
    }

    private void ShowView()
    {
        if (state != PauseState.Playing && canvasObject == null) Build();
        if (canvasObject == null) return;
        canvasObject.SetActive(state != PauseState.Playing);
        panel.SetActive(state == PauseState.Paused);
        countdown.SetActive(state == PauseState.Countdown);
        if (exitLabel != null && controller != null)
        {
            exitLabel.text = controller.IsStoryMode ? "나가기 · 스토리로" : "나가기 · 곡 선택으로";
        }
    }

    private void Build()
    {
        var ui = GetComponent<RhythmGameUIV2>();
        TMP_FontAsset label = ui ? ui.FontLabelBold : null;
        TMP_FontAsset number = ui ? ui.FontNumber : null;
        TMP_FontAsset korean = ui ? ui.FontKorean : null;
        float k = IngameV2Art.K;

        canvasObject = new GameObject("Pause V2 Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasObject.transform;

        var dim = IngameV2Art.Img("Dim", root, null, new Color(0.0f, 0.02f, 0.02f, 0.82f));
        dim.raycastTarget = true; // 뒤의 일시정지 버튼이 눌리지 않게 막는다
        IngameV2Art.Stretch(dim.rectTransform);
        var scan = IngameV2Art.NewRect("Scan", root).gameObject.AddComponent<RawImage>();
        scan.texture = IngameV2Art.ScanlineTile(0.25f);
        scan.uvRect = new Rect(0, 0, 1, 360);
        scan.raycastTarget = false;
        IngameV2Art.Stretch(scan.rectTransform);

        // 일시정지 패널
        int pw = Mathf.RoundToInt(440 * k), ph = Mathf.RoundToInt(310 * k);
        var panelImg = IngameV2Art.Img("Panel", root, IngameV2Art.GlassPanel(pw, ph, 18), Color.white);
        IngameV2Art.PlaceCenter(panelImg.rectTransform, 960, 540, pw, ph);
        panel = panelImg.gameObject;
        var pr = panelImg.rectTransform;

        TextMeshProUGUI red, cyan;
        var title = IngameV2Art.GlitchText("Title", pr, number, "PAUSE", 54 * k, Color.white, TextAlignmentOptions.Center, 2f, true, 4f, out red, out cyan);
        IngameV2Art.Place((RectTransform)title.transform.parent, 0, 22 * k, pw, 60 * k);
        var sub = IngameV2Art.Text("Sub", pr, korean, "일시정지  ·  ESC를 다시 누르면 돌아가요", 14 * k, IngameV2Art.TextDim, TextAlignmentOptions.Center);
        IngameV2Art.Place(sub.rectTransform, 0, 88 * k, pw, 20 * k);

        MakeButton(pr, korean, "게임으로 돌아가기", 62 * k, 128 * k, pw - 124 * k, 40 * k, IngameV2Art.Mint, BeginResumeCountdown);
        MakeButton(pr, korean, "리듬 설정", 62 * k, 180 * k, pw - 124 * k, 40 * k, IngameV2Art.Mint, OpenRhythmSettings);
        exitLabel = MakeButton(pr, korean, "나가기 · 곡 선택으로", 62 * k, 232 * k, pw - 124 * k, 40 * k, IngameV2Art.Red, ExitToLobby);

        // 재개 카운트다운
        var cd = IngameV2Art.NewRect("Countdown", root);
        IngameV2Art.PlaceCenter(cd, 960, 540, 600, 400);
        countdown = cd.gameObject;
        var cap = IngameV2Art.Text("Caption", cd, label, "RESUME IN", 22 * k, IngameV2Art.Mint, TextAlignmentOptions.Center, 30f);
        IngameV2Art.Place(cap.rectTransform, 0, 40, 600, 40);
        countdownNumber = IngameV2Art.GlitchText("Number", cd, number, "3", 200 * k, Color.white, TextAlignmentOptions.Center, 4f, true, 0f,
            out countdownRed, out countdownCyan);
        IngameV2Art.Place((RectTransform)countdownNumber.transform.parent, 0, 80, 600, 300);

        canvasObject.SetActive(false);
    }

    private TextMeshProUGUI MakeButton(RectTransform parent, TMP_FontAsset font, string text, float x, float y, float w, float h,
        Color accent, UnityEngine.Events.UnityAction onClick)
    {
        int iw = Mathf.RoundToInt(w), ih = Mathf.RoundToInt(h);
        var bg = IngameV2Art.Img(text, parent, IngameV2Art.Parallelogram(iw, ih, 12), accent);
        bg.raycastTarget = true;
        IngameV2Art.Place(bg.rectTransform, x, y, w, h);
        var line = IngameV2Art.Img("Line", bg.rectTransform, IngameV2Art.Parallelogram(iw, ih, 12, true), IngameV2Art.WithAlpha(accent, 0.75f));
        IngameV2Art.Stretch(line.rectTransform);
        var label = IngameV2Art.Text("Label", bg.rectTransform, font, text, 17 * IngameV2Art.K, Color.white, TextAlignmentOptions.Center);
        IngameV2Art.Stretch(label.rectTransform);
        var button = bg.gameObject.AddComponent<Button>();
        var colors = button.colors;
        // 바탕 알파: 평소 0.16, 마우스를 올리면 0.4, 누르면 0.55
        colors.normalColor = new Color(1f, 1f, 1f, 0.16f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.4f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.16f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        return label;
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
