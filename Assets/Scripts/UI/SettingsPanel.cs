using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 설정 창. 허브의 '설정' 메뉴(HoloMenuItem의 Panel Prefab)가 지금 화면 위에 띄운다. 씬은 바뀌지 않는다.
// 왼쪽 탭 4개 + 오른쪽 내용:
//   사운드  전체/배경음/효과음 음량, 미리듣기          (실제 동작)
//   리듬    배속, 싱크(±ms), 키 설정                    (배속·싱크는 저장만, 키 설정은 보기용 목업)
//   화면    전체화면/창, 해상도, 프레임 제한            (실제 동작. 에디터 Game 창에서는 전체화면·해상도가 안 바뀜)
//   데이터  진행 초기화 + "정말 초기화할까요?" 확인
// 값은 전부 GameSettings가 PlayerPrefs에 저장한다. ESC 또는 [닫기]로 닫는다. Q/E로 탭을 넘긴다.
// 프리팹은 Tools > CapDemo > 설정·일시정지 > 프리팹 만들기로 만든다 (Assets/Prefabs/UI/SettingsPanel.prefab).
public class SettingsPanel : HoloPopup
{
    [System.Serializable]
    private class Tab
    {
        public HoloDialogButton button;
        public GameObject page;
    }

    [SerializeField] private Tab[] tabs = new Tab[0];
    [SerializeField] private HoloDialogButton closeButton;

    [Header("사운드")]
    [SerializeField] private HoloSlider masterSlider;
    [SerializeField] private HoloSlider bgmSlider;
    [SerializeField] private HoloSlider sfxSlider;
    [SerializeField] private HoloDialogButton bgmPreviewButton;
    [SerializeField] private TMP_Text bgmPreviewLabel;
    [SerializeField] private HoloDialogButton sfxPreviewButton;
    [Tooltip("배경음 미리듣기 곡")]
    [SerializeField] private AudioClip bgmPreviewClip;
    [Tooltip("효과음 미리듣기 소리 (효과음 막대를 놓을 때도 난다)")]
    [SerializeField] private AudioClip sfxPreviewClip;

    [Header("리듬")]
    [SerializeField] private HoloStepper speedStepper;
    [SerializeField] private HoloStepper syncStepper;
    [SerializeField] private HoloDialogButton[] keyButtons = new HoloDialogButton[0];

    [Header("화면")]
    [SerializeField] private HoloStepper screenModeStepper;
    [SerializeField] private HoloStepper resolutionStepper;
    [SerializeField] private HoloStepper frameStepper;

    [Header("데이터")]
    [SerializeField] private HoloDialogButton resetButton;
    [SerializeField] private TMP_Text resetStatus;
    [SerializeField] private CanvasGroup resetConfirm;
    [SerializeField] private HoloDialogButton resetYesButton;
    [SerializeField] private HoloDialogButton resetNoButton;

    private static readonly string[] LaneKeys = { "A", "S", "D", "J", "K", "L" }; // RhythmGameController의 6레인 키

    private int currentTab;
    private AudioSource bgmPreview, sfxPreview;
    private bool resetConfirmOpen;
    private int resetFocus = 1;
    private float keyDenyTime;
    private int keyDenyIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        bgmPreview = gameObject.AddComponent<AudioSource>();
        bgmPreview.playOnAwake = false;
        bgmPreview.loop = true;
        bgmPreview.ignoreListenerPause = true;
        sfxPreview = gameObject.AddComponent<AudioSource>();
        sfxPreview.playOnAwake = false;
        sfxPreview.ignoreListenerPause = true;

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button) tabs[i].button.OnClick = () => ShowTab(index);
        }
        if (closeButton) closeButton.OnClick = Close;

        // 사운드
        if (masterSlider) masterSlider.OnChanged = v => GameSettings.MasterVolume = v;
        if (bgmSlider) bgmSlider.OnChanged = v => GameSettings.BgmVolume = v;
        if (sfxSlider)
        {
            sfxSlider.OnChanged = v => GameSettings.SfxVolume = v;
            sfxSlider.OnReleased = PlaySfxPreview;
        }
        if (bgmPreviewButton) bgmPreviewButton.OnClick = ToggleBgmPreview;
        if (sfxPreviewButton) sfxPreviewButton.OnClick = PlaySfxPreview;

        // 리듬
        if (speedStepper) speedStepper.OnStep = dir =>
        {
            GameSettings.NoteSpeed += dir * GameSettings.NoteSpeedStep;
            return SpeedText();
        };
        if (syncStepper) syncStepper.OnStep = dir =>
        {
            GameSettings.SyncMs += dir * GameSettings.SyncStep;
            return SyncText();
        };
        for (int i = 0; i < keyButtons.Length; i++)
        {
            int index = i;
            if (keyButtons[i]) keyButtons[i].OnClick = () => DenyKey(index);
        }

        // 화면
        if (screenModeStepper) screenModeStepper.OnStep = dir =>
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            return ScreenModeText();
        };
        if (resolutionStepper) resolutionStepper.OnStep = dir =>
        {
            var list = GameSettings.Resolutions;
            int i = list.IndexOf(GameSettings.Resolution);
            i = i < 0 ? list.Count - 1 : Mathf.Clamp(i + dir, 0, list.Count - 1);
            GameSettings.Resolution = list[i];
            return ResolutionText();
        };
        if (frameStepper) frameStepper.OnStep = dir =>
        {
            var limits = GameSettings.FrameLimits;
            int i = System.Array.IndexOf(limits, GameSettings.FrameLimit);
            i = i < 0 ? 1 : (i + dir + limits.Length) % limits.Length;
            GameSettings.FrameLimit = limits[i];
            return FrameText();
        };

        // 데이터
        if (resetButton) resetButton.OnClick = () => ShowResetConfirm(true);
        if (resetYesButton) resetYesButton.OnClick = DoReset;
        if (resetNoButton) resetNoButton.OnClick = () => ShowResetConfirm(false);
    }

    protected override void OnOpened()
    {
        transform.SetAsLastSibling();
        Refresh();
        ShowResetConfirm(false);
        if (resetStatus) resetStatus.text = "";
        ShowTab(currentTab);
    }

    protected override void OnClosing()
    {
        StopBgmPreview();
        GameSettings.Save();
    }

    // 저장된 값으로 화면을 다시 채운다
    private void Refresh()
    {
        if (masterSlider) masterSlider.SetValue(GameSettings.MasterVolume);
        if (bgmSlider) bgmSlider.SetValue(GameSettings.BgmVolume);
        if (sfxSlider) sfxSlider.SetValue(GameSettings.SfxVolume);
        if (speedStepper) speedStepper.SetText(SpeedText());
        if (syncStepper) syncStepper.SetText(SyncText());
        if (screenModeStepper) screenModeStepper.SetText(ScreenModeText());
        if (resolutionStepper) resolutionStepper.SetText(ResolutionText());
        if (frameStepper) frameStepper.SetText(FrameText());
        for (int i = 0; i < keyButtons.Length && i < LaneKeys.Length; i++)
            SetKeyLabel(i, LaneKeys[i]);
        StopBgmPreview();
    }

    private static string SpeedText() => "x " + GameSettings.NoteSpeed.ToString("0.0");
    private static string SyncText() { int ms = GameSettings.SyncMs; return (ms >= 0 ? "+" : "") + ms + " ms"; }
    private static string ScreenModeText() => GameSettings.Fullscreen ? "전체화면" : "창 모드";
    private static string ResolutionText() { var r = GameSettings.Resolution; return r.x + " × " + r.y; }
    private static string FrameText() { int f = GameSettings.FrameLimit; return f > 0 ? f + " FPS" : "제한 없음"; }

    private void ShowTab(int index)
    {
        if (tabs.Length == 0) return;
        currentTab = Mathf.Clamp(index, 0, tabs.Length - 1);
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].page) tabs[i].page.SetActive(i == currentTab);
            if (tabs[i].button) tabs[i].button.Selected = i == currentTab;
        }
        if (currentTab != 0) StopBgmPreview();
        ShowResetConfirm(false);
    }

    // ── 사운드 미리듣기 ──

    private void ToggleBgmPreview()
    {
        if (bgmPreview.isPlaying) StopBgmPreview();
        else if (bgmPreviewClip)
        {
            bgmPreview.clip = bgmPreviewClip;
            bgmPreview.Play();
            if (bgmPreviewLabel) bgmPreviewLabel.text = "배경음 ■";
        }
    }

    private void StopBgmPreview()
    {
        if (bgmPreview) bgmPreview.Stop();
        if (bgmPreviewLabel) bgmPreviewLabel.text = "배경음 ▶";
    }

    private void PlaySfxPreview()
    {
        if (sfxPreviewClip && sfxPreview) sfxPreview.PlayOneShot(sfxPreviewClip, GameSettings.SfxVolume);
    }

    // ── 키 설정 (목업) ──

    private void DenyKey(int index)
    {
        if (keyDenyIndex >= 0 && keyDenyIndex != index) SetKeyLabel(keyDenyIndex, LaneKeys[keyDenyIndex]);
        keyDenyIndex = index;
        keyDenyTime = 1.2f;
        SetKeyLabel(index, "준비 중");
    }

    private void SetKeyLabel(int index, string text)
    {
        if (index < 0 || index >= keyButtons.Length || !keyButtons[index]) return;
        var label = keyButtons[index].transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label) label.text = text;
    }

    // ── 데이터 ──

    private void ShowResetConfirm(bool on)
    {
        resetConfirmOpen = on;
        if (!resetConfirm) return;
        resetConfirm.gameObject.SetActive(on);
        resetConfirm.alpha = on ? 1f : 0f;
        resetFocus = 1;
        if (resetYesButton) resetYesButton.SetHighlighted(false);
        if (resetNoButton) resetNoButton.SetHighlighted(on);
    }

    private void DoReset()
    {
        SaveData.ResetProgress();
        ShowResetConfirm(false);
        if (resetStatus) resetStatus.text = "진행 데이터를 초기화했어요.";
        PlaySfx(closeSfx);
    }

    protected override void Update()
    {
        base.Update();
        if (bgmPreview && bgmPreview.isPlaying) bgmPreview.volume = GameSettings.BgmVolume;
        if (keyDenyIndex >= 0)
        {
            keyDenyTime -= Time.unscaledDeltaTime;
            if (keyDenyTime <= 0f)
            {
                SetKeyLabel(keyDenyIndex, LaneKeys[keyDenyIndex]);
                keyDenyIndex = -1;
            }
        }
    }

    protected override void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (resetConfirmOpen)
        {
            if (kb.escapeKey.wasPressedThisFrame) ShowResetConfirm(false);
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                resetFocus = 1 - resetFocus;
                if (resetYesButton) resetYesButton.SetHighlighted(resetFocus == 0);
                if (resetNoButton) resetNoButton.SetHighlighted(resetFocus == 1);
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (resetFocus == 0) DoReset();
                else ShowResetConfirm(false);
            }
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (kb.qKey.wasPressedThisFrame) ShowTab((currentTab - 1 + tabs.Length) % tabs.Length);
        else if (kb.eKey.wasPressedThisFrame) ShowTab((currentTab + 1) % tabs.Length);
    }

    // ── 만들기 (에디터 메뉴가 부른다) ───────────────────────────────

    private const float PanelW = 1120f, PanelH = 660f;
    private const float LabelX = 40f, ControlX = 330f, ControlW = 370f, RowH = 78f, FirstRowY = 96f;

    public static SettingsPanel Build(HoloUIStyle style, AudioClip bgmClip, AudioClip sfxClip)
    {
        var root = HoloUI.CreateOverlayCanvas("SettingsPanel", 910);
        var p = root.AddComponent<SettingsPanel>();
        p.group = root.GetComponent<CanvasGroup>();
        p.openSfx = style.openSfx;
        p.closeSfx = style.closeSfx;
        p.bgmPreviewClip = bgmClip;
        p.sfxPreviewClip = sfxClip;

        var dim = HoloUI.Image("Dim", root.transform, null, new Color(0f, 0.02f, 0.02f, 0.72f), raycast: true);
        HoloUI.Stretch(dim.rectTransform);

        var panelImg = HoloUI.Image("Panel", root.transform, style.panel, new Color(10 / 255f, 22 / 255f, 22 / 255f, 0.95f), raycast: true);
        p.panel = panelImg.rectTransform;
        p.panel.anchorMin = p.panel.anchorMax = new Vector2(0.5f, 0.5f);
        p.panel.sizeDelta = new Vector2(PanelW, PanelH);
        var line = HoloUI.Image("Line", p.panel, style.panelLine, HoloPalette.Teal.WithAlpha(0.6f));
        HoloUI.Stretch(line.rectTransform);

        // 머리: 민트 띠 + 태그 + 제목 + 닫기
        var bar = HoloUI.Image("Bar", p.panel, null, HoloPalette.Teal);
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = new Vector2(1f, 1f);
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.offsetMin = new Vector2(24f, -6f);
        bar.rectTransform.offsetMax = new Vector2(-24f, -3f);
        var tag = HoloUI.Text("Tag", p.panel, style.monoFont, "●  SYSTEM  ·  SETTINGS", 13f, HoloPalette.Teal, spacing: 12f);
        HoloUI.Place(tag.rectTransform, 36f, 24f, 500f, 22f);
        var title = HoloUI.Text("Title", p.panel, style.titleFont, "설정", 34f, HoloPalette.Ink);
        HoloUI.Place(title.rectTransform, 36f, 50f, 300f, 48f);
        p.closeButton = HoloUI.TealButton("Close", p.panel, style, "닫기", "ESC", PanelW - 216f, 34f, 180f, 52f, 20f);

        var divider = HoloUI.Image("Divider", p.panel, null, HoloPalette.Line);
        HoloUI.Place(divider.rectTransform, 36f, 110f, PanelW - 72f, 1.5f);

        // 왼쪽 탭
        var tabDefs = new[] { ("사운드", "AUDIO"), ("리듬", "RHYTHM"), ("화면", "DISPLAY"), ("데이터", "DATA") };
        var tabs = new List<Tab>();
        var pagesRoot = HoloUI.Node("Pages", p.panel, 290f, 130f, PanelW - 326f, PanelH - 190f);
        for (int i = 0; i < tabDefs.Length; i++)
        {
            var (label, en) = tabDefs[i];
            var b = HoloUI.TealButton("Tab_" + en, p.panel, style, label, en, 36f, 134f + i * 80f, 230f, 64f, 22f);
            b.hoverScale = 1.03f;
            var page = HoloUI.Node("Page_" + en, pagesRoot, 0f, 0f, PanelW - 326f, PanelH - 190f).gameObject;
            tabs.Add(new Tab { button = b, page = page });
        }
        p.tabs = tabs.ToArray();
        var vline = HoloUI.Image("TabLine", p.panel, null, HoloPalette.Line);
        HoloUI.Place(vline.rectTransform, 284f, 134f, 1.5f, PanelH - 190f);

        BuildSoundPage(p, style, (RectTransform)p.tabs[0].page.transform);
        BuildRhythmPage(p, style, (RectTransform)p.tabs[1].page.transform);
        BuildScreenPage(p, style, (RectTransform)p.tabs[2].page.transform);
        BuildDataPage(p, style, (RectTransform)p.tabs[3].page.transform);

        var hint = HoloUI.Text("Hint", p.panel, style.monoFont, "Q / E 탭 이동  ·  ESC 닫기  ·  설정은 자동 저장돼요", 12f, HoloPalette.Dim,
            TextAlignmentOptions.Right, 4f);
        HoloUI.Place(hint.rectTransform, 36f, PanelH - 40f, PanelW - 72f, 20f);

        root.SetActive(false);
        return p;
    }

    private static TMP_Text RowLabel(RectTransform page, HoloUIStyle style, int row, string label, string en)
    {
        float y = row * RowH;
        var t = HoloUI.Text("Label_" + en, page, style.titleFont, label, 22f, HoloPalette.Ink);
        HoloUI.Place(t.rectTransform, LabelX, y, 260f, 30f);
        var e = HoloUI.Text("En_" + en, page, style.monoFont, en, 11f, HoloPalette.Dim, spacing: 10f);
        HoloUI.Place(e.rectTransform, LabelX, y + 30f, 260f, 18f);
        return t;
    }

    private static TMP_Text Note(RectTransform page, HoloUIStyle style, float y, string text, Color color)
    {
        var t = HoloUI.Text("Note", page, style.monoFont, text, 13f, color);
        HoloUI.Place(t.rectTransform, LabelX, y, 760f, 22f);
        return t;
    }

    private static void BuildSoundPage(SettingsPanel p, HoloUIStyle style, RectTransform page)
    {
        RowLabel(page, style, 0, "전체 음량", "MASTER");
        p.masterSlider = HoloSlider.Build(page, style, ControlX, 6f, ControlW);
        RowLabel(page, style, 1, "배경음", "BGM");
        p.bgmSlider = HoloSlider.Build(page, style, ControlX, RowH + 6f, ControlW);
        RowLabel(page, style, 2, "효과음", "SFX");
        p.sfxSlider = HoloSlider.Build(page, style, ControlX, RowH * 2 + 6f, ControlW);

        RowLabel(page, style, 3, "미리듣기", "PREVIEW");
        p.bgmPreviewButton = HoloUI.TealButton("PreviewBgm", page, style, "배경음 ▶", null, ControlX, RowH * 3, 200f, 48f, 18f);
        p.bgmPreviewLabel = p.bgmPreviewButton.transform.Find("Label").GetComponent<TMP_Text>();
        p.sfxPreviewButton = HoloUI.TealButton("PreviewSfx", page, style, "효과음 ▶", null, ControlX + 220f, RowH * 3, 200f, 48f, 18f);

        Note(page, style, RowH * 4 + 10f, "막대를 누르거나 끌어서 바꿔요. 마우스 휠로도 조절할 수 있어요.", HoloPalette.Mute);
    }

    private static void BuildRhythmPage(SettingsPanel p, HoloUIStyle style, RectTransform page)
    {
        RowLabel(page, style, 0, "노트 배속", "NOTE SPEED");
        p.speedStepper = HoloStepper.Build(page, style, ControlX, 4f, 260f);
        RowLabel(page, style, 1, "싱크 조절", "SYNC OFFSET");
        p.syncStepper = HoloStepper.Build(page, style, ControlX, RowH + 4f, 260f);
        Note(page, style, RowH * 2 - 4f, "곡 선택 화면의 배속·싱크와 같은 값이에요.  ·  인게임 반영은 준비 중", HoloPalette.Amber);

        RowLabel(page, style, 3, "키 설정", "KEY CONFIG");
        var keys = new List<HoloDialogButton>();
        for (int i = 0; i < LaneKeys.Length; i++)
        {
            var b = HoloUI.TealButton("Key" + (i + 1), page, style, LaneKeys[i], null, ControlX + i * 74f, RowH * 3, 64f, 56f, 20f);
            keys.Add(b);
            var lane = HoloUI.Text("Lane", b.transform, style.monoFont, "L" + (i + 1), 10f, HoloPalette.Dim, TextAlignmentOptions.Center);
            HoloUI.Place(lane.rectTransform, 0f, 60f, 64f, 16f);
        }
        p.keyButtons = keys.ToArray();
        Note(page, style, RowH * 4 + 24f, "키 바꾸기는 준비 중이에요 (지금은 보기만).", HoloPalette.Dim);
    }

    private static void BuildScreenPage(SettingsPanel p, HoloUIStyle style, RectTransform page)
    {
        RowLabel(page, style, 0, "화면 모드", "WINDOW MODE");
        p.screenModeStepper = HoloStepper.Build(page, style, ControlX, 4f, 300f);
        RowLabel(page, style, 1, "해상도", "RESOLUTION");
        p.resolutionStepper = HoloStepper.Build(page, style, ControlX, RowH + 4f, 300f);
        RowLabel(page, style, 2, "프레임 제한", "FRAME LIMIT");
        p.frameStepper = HoloStepper.Build(page, style, ControlX, RowH * 2 + 4f, 300f);
        Note(page, style, RowH * 3 + 10f, "에디터 Game 창에서는 화면 모드·해상도가 바뀌지 않아요 (빌드한 게임에서 적용).", HoloPalette.Mute);
    }

    private static void BuildDataPage(SettingsPanel p, HoloUIStyle style, RectTransform page)
    {
        RowLabel(page, style, 0, "진행 초기화", "RESET PROGRESS");
        p.resetButton = HoloUI.RedButton("Reset", page, style, "초기화", "RESET", ControlX, 0f, 240f, 56f, 20f);
        Note(page, style, RowH, "플레이어 이름, 클리어한 챕터, 만난 NPC 기록을 지워요. 설정값과 곡 기록은 남아요.", HoloPalette.Mute);
        p.resetStatus = Note(page, style, RowH + 30f, "", HoloPalette.Teal);

        // "정말 초기화할까요?" 작은 확인 창 (설정 창 안에서 뜬다)
        var confirm = HoloUI.Node("ResetConfirm", page, 0f, 0f, PanelW - 326f, PanelH - 190f);
        var cg = confirm.gameObject.AddComponent<CanvasGroup>();
        var dim = HoloUI.Image("Dim", confirm, null, new Color(0f, 0.02f, 0.02f, 0.85f), raycast: true);
        HoloUI.Stretch(dim.rectTransform);
        var box = HoloUI.Image("Box", confirm, style.panel, new Color(20 / 255f, 8 / 255f, 10 / 255f, 0.96f), raycast: true);
        HoloUI.Place(box.rectTransform, 110f, 90f, 580f, 230f);
        var boxLine = HoloUI.Image("Line", box.transform, style.panelLine, HoloPalette.Red.WithAlpha(0.7f));
        HoloUI.Stretch(boxLine.rectTransform);
        var tag = HoloUI.Text("Tag", box.transform, style.monoFont, "●  WARNING  ·  DATA RESET", 13f, HoloPalette.Red, spacing: 12f);
        HoloUI.Place(tag.rectTransform, 30f, 20f, 500f, 22f);
        var q = HoloUI.Text("Title", box.transform, style.titleFont, "정말 초기화할까요?", 28f, HoloPalette.Ink);
        HoloUI.Place(q.rectTransform, 30f, 48f, 520f, 40f);
        var sub = HoloUI.Text("Sub", box.transform, style.monoFont, "되돌릴 수 없어요. 튜토리얼부터 다시 시작해요.", 14f, HoloPalette.Mute);
        HoloUI.Place(sub.rectTransform, 31f, 92f, 520f, 22f);
        p.resetYesButton = HoloUI.RedButton("Yes", box.transform, style, "초기화", "RESET", 30f, 136f, 250f, 62f);
        p.resetNoButton = HoloUI.TealButton("No", box.transform, style, "취소", "CANCEL", 300f, 136f, 250f, 62f);
        p.resetConfirm = cg;
        confirm.gameObject.SetActive(false);
    }
}
