using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 스토리 챕터·맵 NPC 대화 중에 오른쪽 위에 뜨는 [AUTO] [일시정지] 버튼.
//  - AUTO: 켜면 대사가 다 나온 뒤 잠깐 기다렸다가 저절로 다음 줄로 넘어간다 (켜짐 상태는 씬이 바뀌어도 유지)
//  - 일시정지(또는 ESC): 게임을 멈추고 "메인화면으로 돌아가시겠습니까?" 창(PausePanel)을 띄운다
// 스토리 캔버스(StoryCanvas·MapStoryCanvas)나 맵 대화 캔버스(DialogueCanvas) 안에 넣어 두면,
// 같은 캔버스의 StoryManager가 재생 중이거나 DemoDialogue가 열려 있을 때만 보인다.
// 맵 대화 캔버스(DialogueCanvas)에 있는 것은 맵을 걸어 다닐 때도 ESC로 일시정지 창을 연다 (Esc On Map).
// 넣기: Tools > CapDemo > 설정·일시정지 > 스토리·맵 대화에 Auto·일시정지 버튼 붙이기
public class StoryControls : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private HoloDialogButton autoButton;
    [SerializeField] private Graphic autoArrows;
    [SerializeField] private HoloDialogButton pauseButton;
    [Tooltip("일시정지 창 프리팹 (Assets/Prefabs/UI/PausePanel.prefab)")]
    [SerializeField] private PausePanel pausePanelPrefab;
    [Tooltip("비우면 같은 캔버스에서 찾는다")]
    [SerializeField] private StoryManager story;
    [Tooltip("비우면 같은 캔버스에서 찾는다")]
    [SerializeField] private DemoDialogue mapDialogue;

    [Tooltip("맵 대화 캔버스에 있을 때: 대화 중이 아니어도(맵을 걸어 다닐 때) ESC로 일시정지 창을 연다")]
    [SerializeField] private bool escOnMap = true;

    [Header("AUTO 기다리는 시간 (대사가 다 나온 뒤)")]
    [Tooltip("기본으로 기다리는 시간(초)")]
    [SerializeField, Min(0f)] private float autoBaseDelay = 1.2f;
    [Tooltip("글자 하나마다 더 기다리는 시간(초). 긴 대사는 조금 더 오래 보여 준다")]
    [SerializeField, Min(0f)] private float autoPerChar = 0.04f;
    [Tooltip("아무리 길어도 이 시간(초)이 지나면 넘어간다")]
    [SerializeField, Min(0f)] private float autoMaxDelay = 4.5f;

    // AUTO 켜짐 (게임 실행 동안 유지)
    public static bool AutoOn { get; set; }

    private static readonly List<StoryControls> active = new List<StoryControls>();
    private PausePanel pausePanel;
    private bool visible;
    private StoryOverlay[] overlays; // 맵 위 스토리. 재생 중에는 그쪽 버튼이 ESC를 받는다

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        AutoOn = false;
        active.Clear();
    }

    // 대사 넘기기 입력을 무시해야 하는지: 일시정지 중, 창이 떠 있음, 또는 마우스가 이 버튼들 위에 있음(버튼 클릭이 대사까지 넘기지 않게)
    public static bool BlocksAdvance
    {
        get
        {
            if (GamePause.IsPaused || HoloPopup.AnyOpen) return true;
            foreach (var c in active)
                if (c && c.PointerOverButtons()) return true;
            return false;
        }
    }

    // 글자 수가 chars인 대사를 다 보여 준 뒤 AUTO가 기다릴 시간(초)
    public static float AutoDelay(int chars)
    {
        var c = active.Count > 0 ? active[0] : null;
        float baseDelay = c ? c.autoBaseDelay : 1.2f, perChar = c ? c.autoPerChar : 0.04f, max = c ? c.autoMaxDelay : 4.5f;
        return Mathf.Min(max, baseDelay + perChar * Mathf.Max(0, chars));
    }

    private void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (!story) story = GetComponentInParent<StoryManager>(true);
        if (!mapDialogue) mapDialogue = GetComponentInParent<DemoDialogue>(true);
        var canvas = GetComponentInParent<Canvas>(true);
        if (!story && canvas) story = canvas.GetComponentInChildren<StoryManager>(true);
        if (!mapDialogue && canvas) mapDialogue = canvas.GetComponentInChildren<DemoDialogue>(true);

        HoloUI.EnsureEventSystem(); // 버튼을 누를 수 있게
        if (autoButton) autoButton.OnClick = () => AutoOn = !AutoOn;
        if (pauseButton) pauseButton.OnClick = OpenPause;
        SetVisible(false, true);
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    public void OpenPause()
    {
        if (!(visible || CanPauseOnMap()) || GamePause.IsPaused || HoloPopup.AnyOpen || HoloTransition.IsBusy || ScreenFader.IsBusy) return;
        if (!pausePanel)
        {
            pausePanel = pausePanelPrefab ? HoloPopup.Spawn(pausePanelPrefab) : PausePanel.SpawnShared();
            if (!pausePanel) return;
        }
        pausePanel.Show();
    }

    // 맵을 걸어 다니는 중인지 (맵 대화 캔버스 + 대화·맵 스토리 재생 중이 아님)
    private bool CanPauseOnMap()
    {
        if (!escOnMap || !mapDialogue || mapDialogue.IsOpen) return false;
        if (overlays == null) overlays = FindObjectsByType<StoryOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var o in overlays)
            if (o && o.IsPlaying) return false;
        return true;
    }

    private void Update()
    {
        bool want = (story && story.IsPlaying) || (mapDialogue && mapDialogue.IsOpen);
        if (want != visible) SetVisible(want, false);

        if (group)
        {
            float target = visible ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / 0.2f);
        }

        if (autoButton) autoButton.Selected = AutoOn;
        // AUTO가 켜져 있으면 ▶▶가 깜빡인다
        if (autoArrows) autoArrows.enabled = !AutoOn || Time.unscaledTime % 0.8f < 0.5f;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && (visible || CanPauseOnMap())) OpenPause();
    }

    private void SetVisible(bool on, bool instant)
    {
        visible = on;
        if (!group) return;
        group.blocksRaycasts = on;
        group.interactable = on;
        if (instant) group.alpha = on ? 1f : 0f;
    }

    private bool PointerOverButtons()
    {
        if (!visible) return false;
        var mouse = Mouse.current;
        if (mouse == null) return false;
        var pos = mouse.position.ReadValue();
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        return Over(autoButton, pos, cam) || Over(pauseButton, pos, cam);
    }

    private static bool Over(Component c, Vector2 pos, Camera cam) =>
        c && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)c.transform, pos, cam);

    // ── 만들기 (에디터 메뉴가 부른다) ───────────────────────────────

    public static StoryControls Build(HoloUIStyle style, PausePanel pausePrefab)
    {
        var go = new GameObject("StoryControls", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-36f, -30f);
        rt.sizeDelta = new Vector2(232f, 54f);

        var c = go.AddComponent<StoryControls>();
        c.group = go.GetComponent<CanvasGroup>();
        c.pausePanelPrefab = pausePrefab;

        // AUTO 버튼: 꺼짐 = 어두운 패널, 켜짐 = 민트
        c.autoButton = HoloUI.TealButton("Auto", rt, style, "AUTO", null, 0f, 0f, 160f, 54f, 20f);
        var label = c.autoButton.transform.Find("Label").GetComponent<TMP_Text>();
        label.font = style.monoFont;
        label.characterSpacing = 10f;
        label.alignment = TextAlignmentOptions.Left;
        HoloUI.Place(label.rectTransform, 22f, 0f, 90f, 54f);
        var arrows = HoloUI.Text("Arrows", c.autoButton.transform, style.monoFont, "▶▶", 14f, HoloPalette.Ink, TextAlignmentOptions.Right);
        HoloUI.Place(arrows.rectTransform, 96f, 0f, 46f, 54f);
        c.autoArrows = arrows;
        c.autoButton.labels = new Graphic[] { label, arrows };
        c.autoButton.hoverScale = 1.04f;

        // 일시정지 버튼: 세로 막대 두 개
        c.pauseButton = HoloUI.TealButton("Pause", rt, style, "", null, 176f, 0f, 56f, 54f);
        var bars = new List<Graphic>();
        for (int i = 0; i < 2; i++)
        {
            var bar = HoloUI.Image("Bar" + i, c.pauseButton.transform, null, HoloPalette.Ink);
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(6f, 20f);
            bar.rectTransform.anchoredPosition = new Vector2(i == 0 ? -6f : 6f, 0f);
            bars.Add(bar);
        }
        c.pauseButton.labels = bars.ToArray();
        c.pauseButton.hoverScale = 1.08f;
        return c;
    }
}
