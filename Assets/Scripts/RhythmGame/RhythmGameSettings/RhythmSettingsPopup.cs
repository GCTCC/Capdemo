using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 곡 선택의 톱니 버튼과 인게임 일시정지에서 여는 리듬 전용 설정 창.
public class RhythmSettingsPopup : HoloPopup
{
    private TMP_Text speedValue, syncValue;
    private Button speedDown, speedUp, syncDown, syncUp;
    private TMP_FontAsset font;
    private RhythmSyncPreview syncPreview;

    public static RhythmSettingsPopup Create(Transform owner, TMP_FontAsset textFont)
    {
        var root = HoloUI.CreateOverlayCanvas("Rhythm Settings", 950);
        root.transform.SetParent(owner, false);
        root.SetActive(false);
        var popup = root.AddComponent<RhythmSettingsPopup>();
        popup.font = textFont;
        popup.group = root.GetComponent<CanvasGroup>();
        var dim = HoloUI.Image("Dim", root.transform, null, new Color(0f, 0.02f, 0.025f, 0.87f), true);
        HoloUI.Stretch(dim.rectTransform);
        var bg = HoloUI.Image("Panel", root.transform, null, new Color(0.025f, 0.085f, 0.09f, 1f), true);
        popup.panel = bg.rectTransform;
        popup.panel.anchorMin = popup.panel.anchorMax = new Vector2(0.5f, 0.5f);
        popup.panel.pivot = new Vector2(0.5f, 0.5f);
        popup.panel.sizeDelta = new Vector2(920f, 510f);
        var title = popup.Text("Title", "리듬 설정", 30, Color.white);
        HoloUI.Place(title.rectTransform, 40, 25, 650, 55);
        popup.MakeButton("Close", "닫기", 780, 30, 100, 45, popup.Close);

        var label = popup.Text("Speed Label", "노트 속도", 24, Color.white);
        HoloUI.Place(label.rectTransform, 40, 120, 280, 45);
        popup.speedValue = popup.Text("Speed Value", "", 28, new Color(0.64f, 0.92f, 0.84f));
        HoloUI.Place(popup.speedValue.rectTransform, 550, 118, 170, 50);
        popup.speedValue.alignment = TextAlignmentOptions.Center;
        popup.speedDown = popup.MakeButton("Speed Down", "−", 465, 120, 65, 46,
            () => RhythmSettings.NoteSpeed -= NoteScrollSetting.Step);
        popup.speedUp = popup.MakeButton("Speed Up", "+", 750, 120, 65, 46,
            () => RhythmSettings.NoteSpeed += NoteScrollSetting.Step);
        var speedHelp = popup.Text("Speed Help", "큰 값일수록 빠르게 내려옵니다. 음악 재생 속도는 유지됩니다.", 17, new Color(0.65f, 0.76f, 0.75f));
        HoloUI.Place(speedHelp.rectTransform, 40, 179, 840, 30);

        label = popup.Text("Sync Label", "판정 싱크", 24, Color.white);
        HoloUI.Place(label.rectTransform, 40, 250, 280, 45);
        popup.syncValue = popup.Text("Sync Value", "", 28, new Color(0.64f, 0.92f, 0.84f));
        HoloUI.Place(popup.syncValue.rectTransform, 550, 248, 170, 50);
        popup.syncValue.alignment = TextAlignmentOptions.Center;
        popup.syncDown = popup.MakeButton("Sync Down", "−", 465, 250, 65, 46,
            () => RhythmSettings.SyncMs -= JudgementTimingSetting.Step);
        popup.syncUp = popup.MakeButton("Sync Up", "+", 750, 250, 65, 46,
            () => RhythmSettings.SyncMs += JudgementTimingSetting.Step);
        var syncHelp = popup.Text("Sync Help", "+ 값: 노트와 판정을 늦춤  /  − 값: 앞당김 (2ms 단위)", 17, new Color(0.65f, 0.76f, 0.75f));
        HoloUI.Place(syncHelp.rectTransform, 40, 310, 840, 30);
        popup.MakeButton("Detailed Preview", "프리뷰에서 상세 설정", 500, 348, 380, 40, popup.OpenSyncPreview);
        popup.MakeButton("Reset", "리듬 설정 초기화", 40, 400, 300, 50, () => {
            RhythmSettings.SetValues(NoteScrollSetting.Default, JudgementTimingSetting.Default);
        });
        var saved = popup.Text("Saved", "변경 즉시 적용·저장됩니다.  ESC: 닫기", 17, new Color(0.65f, 0.76f, 0.75f));
        HoloUI.Place(saved.rectTransform, 380, 410, 500, 35);
        popup.HideNow();
        return popup;
    }

    private TMP_Text Text(string name, string value, float size, Color color)
    {
        return HoloUI.Text(name, panel, font, value, size, color);
    }

    private Button MakeButton(string name, string label, float x, float y, float width, float height,
        UnityEngine.Events.UnityAction action)
    {
        var image = HoloUI.Image(name, panel, null, new Color(0.12f, 0.30f, 0.30f, 1f), true);
        HoloUI.Place(image.rectTransform, x, y, width, height);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        var text = HoloUI.Text("Label", image.transform, font, label, 22, Color.white, TextAlignmentOptions.Center);
        HoloUI.Stretch(text.rectTransform);
        button.onClick.AddListener(action);
        return button;
    }

    private void OnEnable() { RhythmSettings.Changed += Refresh; }
    private void OnDisable() { RhythmSettings.Changed -= Refresh; }
    protected override void OnOpened() { Refresh(); }
    protected override void OnClosing() { RhythmSettings.Save(); }
    protected override void HandleKeys()
    {
        if (syncPreview != null && syncPreview.IsPreviewOpen) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    private void OpenSyncPreview()
    {
        if (syncPreview == null)
        {
            syncPreview = RhythmSyncPreview.Create(transform, font);
            syncPreview.Closed += () => {
                if (group != null) group.interactable = true;
                Refresh();
            };
        }
        if (group != null) group.interactable = false;
        syncPreview.Open();
        if (!syncPreview.IsOpen && group != null) group.interactable = true;
    }

    private void Refresh()
    {
        if (speedValue != null) speedValue.text = "x " + RhythmSettings.NoteSpeed.ToString("0.00");
        int sync = RhythmSettings.SyncMs;
        if (syncValue != null) syncValue.text = (sync >= 0 ? "+" : "") + sync + " ms";
        if (speedDown != null) speedDown.interactable = RhythmSettings.NoteSpeed > NoteScrollSetting.Min;
        if (speedUp != null) speedUp.interactable = RhythmSettings.NoteSpeed < NoteScrollSetting.Max;
        if (syncDown != null) syncDown.interactable = sync > JudgementTimingSetting.Min;
        if (syncUp != null) syncUp.interactable = sync < JudgementTimingSetting.Max;
    }
}
