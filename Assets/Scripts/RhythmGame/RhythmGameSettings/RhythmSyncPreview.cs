using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 일정한 박자 소리 + 실제 노트 이동 공식을 사용하는 독립 프리뷰.
// 임시 배속/싱크는 확인 전까지 PlayerPrefs나 본 게임에 쓰지 않는다.
public class RhythmSyncPreview : HoloPopup
{
    public event Action Closed;
    private TMP_FontAsset font;
    private TMP_Text speedText, syncText, feedback, statistics, cue;
    private Button suggestButton;
    private RhythmPreviewHighway highway;
    private readonly RhythmCalibrationSession session = new RhythmCalibrationSession();
    private readonly List<AudioSource> pausedLoops = new List<AudioSource>();
    private AudioSource reference;
    private AudioClip referenceClip;
    private float draftSpeed, baseSpeed = 15;
    private int draftSync;
    private double deviceCalibrationMs = 145.83333;
    private double referenceTime;
    private readonly RhythmPreviewSampleClock sampleClock = new RhythmPreviewSampleClock();
    private bool referenceStarted, completed, suspended, finishing;
    private const int LoopBeats = 16;

    public bool IsPreviewOpen { get { return IsOpen; } }

    public static RhythmSyncPreview Create(Transform owner, TMP_FontAsset fallbackFont)
    {
        var root = HoloUI.CreateOverlayCanvas("Rhythm Sync Preview", 1000);
        root.transform.SetParent(owner, false);
        root.SetActive(false);
        var view = root.AddComponent<RhythmSyncPreview>();
        view.group = root.GetComponent<CanvasGroup>();
        view.group.ignoreParentGroups = true; // 부모 설정 창의 입력 잠금과 독립적으로 조작한다.
        view.font = IngameV2Art.MakeFont(SongTransitionSettings.Load().notoSansKr, fallbackFont);
        var dim = HoloUI.Image("Dim", root.transform, null, new Color(0, 0.015f, 0.025f, 0.94f), true);
        HoloUI.Stretch(dim.rectTransform);
        var bg = HoloUI.Image("Panel", root.transform, null, new Color(0.025f, 0.07f, 0.085f, 1), true);
        view.panel = bg.rectTransform;
        view.panel.anchorMin = view.panel.anchorMax = view.panel.pivot = new Vector2(0.5f, 0.5f);
        view.panel.sizeDelta = new Vector2(1640, 920);
        view.Build();
        view.HideNow();
        return view;
    }

    private void Build()
    {
        Label("Title", "프리뷰에서 상세 설정", 36, 40, 25, 1120, 60, Color.white);
        Label("Subtitle", "키를 눌러 판정 싱크와 노트 속도를 직접 확인하세요  /  80 BPM", 18, 40, 85, 1110, 35, Muted);
        MakeButton("Close", "닫기", 1515, 30, 90, 45, Close);
        Label("Speed Label", "노트 속도", 27, 40, 145, 1080, 40, Color.white);
        speedText = Label("Speed", "", 34, 435, 200, 190, 65, Mint);
        speedText.alignment = TextAlignmentOptions.Center;
        float[] speedSteps = { -1, -0.1f, -0.01f, 0.01f, 0.1f, 1 };
        string[] speedLabels = { "-1", "-0.1", "-0.01", "+0.01", "+0.1", "+1" };
        for (int i = 0; i < speedSteps.Length; i++)
        {
            float step = speedSteps[i];
            float x = i < 3 ? 75 + i * 115 : 665 + (i - 3) * 115;
            MakeButton("Speed " + i, speedLabels[i], x, 200, 95, 65, () => {
                draftSpeed = NoteScrollSetting.Normalize(draftSpeed + step); RefreshValues();
            });
        }
        Label("Speed Help", "배속이 높을수록 노트가 빨라집니다. 음악 재생 속도는 유지됩니다.", 18, 40, 280, 1100, 35, Muted);
        Label("Sync Label", "판정 싱크", 27, 40, 350, 1080, 40, Color.white);
        syncText = Label("Sync", "", 34, 435, 405, 190, 65, Mint);
        syncText.alignment = TextAlignmentOptions.Center;
        int[] syncSteps = { -10, -2, -1, 1, 2, 10 };
        for (int i = 0; i < syncSteps.Length; i++)
        {
            int step = syncSteps[i];
            float x = i < 3 ? 75 + i * 115 : 665 + (i - 3) * 115;
            MakeButton("Sync " + i, (step > 0 ? "+" : "") + step, x, 405, 95, 65, () => {
                draftSync = JudgementTimingSetting.Normalize(draftSync + step); RefreshValues();
            });
        }
        Label("Sync Help", "+는 노트와 판정을 늦추고, -는 앞당깁니다. 단위: ms\nFAST가 반복되면 -, SLOW가 반복되면 +로 조절하세요.", 18, 40, 485, 1100, 55, Muted);
        feedback = Label("Feedback", "박자 소리에 맞춰 키를 눌러보세요", 29, 40, 548, 1090, 45, Mint);
        statistics = Label("Statistics", "", 19, 40, 605, 1090, 65, Muted);
        MakeButton("Reset Measure", "다시 측정", 40, 690, 225, 55, ResetMeasurements);
        suggestButton = MakeButton("Suggest", "추천 싱크 적용", 650, 690, 430, 55, () => {
            if (!session.CanSuggest) return;
            draftSync = session.SuggestedSyncMs; RefreshValues();
        });
        Label("Save Help", "확인 전까지 임시 설정입니다. 소리가 안 들리면 전체/효과음 볼륨을 확인하세요.", 16, 40, 765, 1100, 30, Muted);
        MakeButton("Cancel", "취소", 170, 820, 360, 60, Close);
        MakeButton("Apply", "확인", 600, 820, 480, 60, () => {
            RhythmSettings.SetValues(draftSpeed, draftSync); Close();
        });
        Label("Preview Label", "노트 프리뷰", 23, 1190, 50, 400, 40, Mint).alignment = TextAlignmentOptions.Center;
        var viewport = HoloUI.Node("Preview", panel, 1190, 110, 400, 705);
        viewport.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Highway", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(viewport, false);
        highway = go.AddComponent<RhythmPreviewHighway>();
        highway.raycastTarget = true;
        HoloUI.Stretch((RectTransform)go.transform);
        var trigger = go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener(_ => Tap());
        trigger.triggers.Add(entry);
        cue = Label("Cue", "박자 소리 준비 중", 21, 1190, 825, 400, 35, Mint);
        cue.alignment = TextAlignmentOptions.Center;
        Label("Keys", "A / S / D / J / K / L\nSPACE 또는 레인 클릭", 18, 1190, 865, 400, 50, Muted).alignment = TextAlignmentOptions.Center;
    }

    private static Color Mint { get { return new Color(0.64f, 0.96f, 0.86f); } }
    private static Color Muted { get { return new Color(0.62f, 0.75f, 0.76f); } }
    private TMP_Text Label(string name, string value, float size, float x, float y, float width, float height, Color color)
    {
        var text = HoloUI.Text(name, panel, font, value, size, color, TextAlignmentOptions.TopLeft);
        HoloUI.Place(text.rectTransform, x, y, width, height);
        return text;
    }
    private Button MakeButton(string name, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
    {
        var image = HoloUI.Image(name, panel, null, new Color(0.10f, 0.25f, 0.27f, 1), true);
        HoloUI.Place(image.rectTransform, x, y, width, height);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        var label = HoloUI.Text("Text", image.transform, font, value, 23, Color.white, TextAlignmentOptions.Center);
        HoloUI.Stretch(label.rectTransform);
        button.onClick.AddListener(() => {
            if (finishing) return;
            action();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });
        return button;
    }

    protected override void OnOpened()
    {
        completed = false; suspended = false; finishing = false;
        group.interactable = true;
        draftSpeed = RhythmSettings.NoteSpeed; draftSync = RhythmSettings.SyncMs;
        baseSpeed = 15; deviceCalibrationMs = RhythmClock.DefaultNoteTimingCalibrationMs;
        var controller = FindAnyObjectByType<RhythmGameControllerV2>();
        if (controller != null)
        {
            baseSpeed = controller.BaseNoteSpeed;
            deviceCalibrationMs = controller.Clock.NoteTimingCalibrationMilliseconds;
            var field = controller.GetComponent<RhythmPlayFieldV2>();
            highway.Configure(field.NearZ, field.NoteSpawnZ, field.JudgeZ, field.NearHalfWidth, field.NoteSpawnHalfWidth);
        }
        ResetMeasurements();
        PauseBackgroundLoops();
        StartReference();
        RefreshValues();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void StartReference()
    {
        if (reference == null)
        {
            reference = gameObject.AddComponent<AudioSource>();
            reference.playOnAwake = false; reference.loop = true; reference.spatialBlend = 0;
            reference.ignoreListenerPause = true;
        }
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        int count = (int)Math.Round(LoopBeats * RhythmCalibrationSession.BeatInterval * rate);
        var samples = new float[count];
        for (int beat = 0; beat < LoopBeats; beat++)
        {
            int start = (int)Math.Round(beat * RhythmCalibrationSession.BeatInterval * rate);
            for (int i = 0; i < rate * 0.035 && start + i < count; i++)
            {
                double t = i / (double)rate;
                samples[start + i] = (float)(0.55 * Math.Sin(2 * Math.PI * (beat % 4 == 0 ? 1300 : 950) * t)
                    * Math.Exp(-t * 115) * Math.Min(1, t / 0.001));
            }
        }
        referenceClip = AudioClip.Create("Rhythm calibration 80 BPM", count, 1, rate, false);
        referenceClip.SetData(samples, 0);
        reference.clip = referenceClip; reference.pitch = 1; reference.volume = GameSettings.SfxVolume;
        referenceTime = 0; sampleClock.Reset(); referenceStarted = false;
        if (AudioListener.pause) reference.Play();
        else reference.PlayScheduled(AudioSettings.dspTime + 0.25);
    }

    private void StopReference()
    {
        if (reference != null) { reference.Stop(); reference.clip = null; }
        if (referenceClip != null) { Destroy(referenceClip); referenceClip = null; }
        referenceStarted = false;
    }
    private void PauseBackgroundLoops()
    {
        pausedLoops.Clear();
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (source != reference && source.loop && source.isPlaying)
            { pausedLoops.Add(source); source.Pause(); }
    }
    private void RestoreBackgroundLoops()
    {
        foreach (var source in pausedLoops) if (source != null && source.isActiveAndEnabled) source.UnPause();
        pausedLoops.Clear();
    }

    protected override void Update()
    {
        ReadReferenceTime();
        base.Update();
        if (!IsOpen || highway == null) return;
        group.blocksRaycasts = true; // 닫히는 애니메이션 중에도 뒤 창으로 클릭이 새지 않게 한다.
        highway.SetFrame(referenceTime, NoteScrollSetting.ToWorldSpeed(baseSpeed, draftSpeed), deviceCalibrationMs, draftSync);
        double phaseBeat = referenceTime % RhythmCalibrationSession.BeatInterval;
        cue.text = referenceStarted ? (phaseBeat < 0.12 ? "BEAT" : "박자 소리에 맞춰 입력") : "박자 소리 준비 중";
    }

    private void ReadReferenceTime()
    {
        if (IsOpen && !suspended && reference != null && reference.clip != null)
        {
            double phase = reference.timeSamples / (double)reference.clip.frequency;
            if (reference.isPlaying && phase > 0) referenceStarted = true;
            if (referenceStarted)
            {
                referenceTime = sampleClock.Read(reference.timeSamples, reference.clip.frequency, reference.clip.length);
                reference.volume = GameSettings.SfxVolume;
            }
        }
    }

    protected override void HandleKeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (keyboard.aKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame
            || keyboard.jKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame) Tap();
    }
    private void Tap()
    {
        ReadReferenceTime();
        if (!IsOpen || finishing || suspended || !referenceStarted) return;
        double error, target;
        if (!session.TryTap(referenceTime, deviceCalibrationMs, draftSync, out error, out target)) return;
        double chartTime = JudgementTimingSetting.ChartTime(referenceTime, deviceCalibrationMs, draftSync);
        highway.ShowInput(target, chartTime);
        feedback.text = (Math.Abs(error) <= 20 ? "거의 일치" : error < 0 ? "FAST · 빠름" : "SLOW · 늦음")
            + "   " + (error >= 0 ? "+" : "") + error.ToString("0") + " ms";
        feedback.color = Math.Abs(error) <= 20 ? Mint : new Color(1, 0.5f, 0.65f);
        RefreshValues();
    }
    private void ResetMeasurements()
    {
        session.Reset();
        feedback.text = "박자 소리에 맞춰 키를 눌러보세요";
        feedback.color = Mint;
        highway.ResetFeedback();
        RefreshValues();
    }

    private void RefreshValues()
    {
        if (speedText == null) return;
        speedText.text = draftSpeed.ToString("0.00");
        syncText.text = (draftSync >= 0 ? "+" : "") + draftSync + " ms";
        suggestButton.interactable = session.CanSuggest;
        if (session.Count == 0) statistics.text = "같은 박자에 한 번씩 입력하세요. 8회 이상 안정적으로 입력하면 추천값이 나옵니다.";
        else statistics.text = "측정 " + session.Count + "회 / 현재 중앙 오차 "
            + (session.MedianDesiredMs - draftSync).ToString("+0;-0;0") + " ms / 흔들림 " + session.VariationMs.ToString("0") + " ms\n"
            + (session.CanSuggest ? "추천 싱크: " + session.SuggestedSyncMs + " ms · 버튼으로 임시 적용 후 다시 확인하세요."
                : session.Count < 8 ? "추천값까지 " + (8 - session.Count) + "회 더 입력하세요." : "박자 소리에 맞춰 일정하게 입력해 보세요.");
    }

    protected override void OnClosing()
    {
        finishing = true;
        group.interactable = false;
        StopReference(); RestoreBackgroundLoops();
    }
    protected override void OnClosed() { Complete(); }
    private void OnDisable()
    {
        StopReference(); RestoreBackgroundLoops();
        if (IsOpen) HideNow();
        Complete();
    }
    private void Complete()
    {
        if (completed) return;
        completed = true;
        Closed?.Invoke();
    }
    private void OnApplicationFocus(bool focused)
    {
        if (!IsOpen) return;
        if (!focused) { suspended = true; StopReference(); }
        else if (suspended)
        {
            suspended = false; StartReference(); ResetMeasurements();
        }
    }
}
