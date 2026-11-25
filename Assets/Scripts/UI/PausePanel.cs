using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 일시정지 창: "메인화면으로 돌아가시겠습니까?" + [확인] [취소].
// 스토리 챕터·맵 대화(StoryControls의 일시정지 버튼)에서 쓰고, 나중에 리듬 인게임에서도 같은 프리팹을 쓴다.
//  - 열리면 게임을 멈춘다 (Pause Game을 끄면 멈추지 않는다: 리듬처럼 자기 방식으로 멈추는 곳)
//  - 확인: 지직 전환으로 Return Scene(메인화면 = HoloHubScene)으로 간다. Open(onConfirm, ...)으로 다른 동작을 줄 수도 있다
//  - 취소/ESC: 창이 닫히고 멈췄던 곳에서 이어진다
//  - 키보드: Enter = 고른 버튼, Esc = 취소, ←/→ = 버튼 고르기
// 프리팹은 Tools > CapDemo > 설정·일시정지 > 프리팹 만들기로 만든다 (Assets/Resources/UI/PausePanel.prefab).
// Resources 안에 있어서 씬에 연결하지 않아도 PausePanel.SpawnShared()로 어디서든 꺼내 쓸 수 있다 (리듬 인게임 확인 창 등).
public class PausePanel : HoloPopup
{
    [Header("문구")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string defaultMessage = "메인화면으로 돌아가시겠습니까?";
    [SerializeField] private string defaultTitle = "일시정지";

    [Header("버튼")]
    [SerializeField] private HoloDialogButton confirmButton;
    [SerializeField] private HoloDialogButton cancelButton;

    [Header("동작")]
    [Tooltip("확인을 누르면 갈 씬 (메인화면)")]
    [SerializeField] private string returnScene = "HoloHubScene";
    [Tooltip("열려 있는 동안 게임을 멈출지 (Time.timeScale 0 + 소리 멈춤 + 스토리 멈춤)")]
    [SerializeField] private bool pauseGame = true;

    private int focus = 1; // 0 확인, 1 취소 (실수로 나가지 않게 취소가 먼저)
    private System.Action onConfirm, onCancel;
    private bool confirmed;
    private bool pausing; // 이번에 열 때 게임을 멈췄는지
    private string defaultConfirmEn; // 확인 버튼의 작은 영문 (프리팹 값 = MAIN)

    private void SetConfirmEn(string text)
    {
        var en = confirmButton ? confirmButton.transform.Find("En")?.GetComponent<TMP_Text>() : null;
        if (!en) return;
        if (defaultConfirmEn == null) defaultConfirmEn = en.text;
        if (!string.IsNullOrEmpty(text)) en.text = text;
    }

    public const string ResourcePath = "UI/PausePanel";

    // Resources의 공통 프리팹을 씬에 하나 만든다 (닫힌 상태). 없으면 null
    public static PausePanel SpawnShared()
    {
        var prefab = Resources.Load<PausePanel>(ResourcePath);
        if (!prefab)
        {
            Debug.LogWarning("[PausePanel] Resources/" + ResourcePath + " 프리팹이 없어요. Tools > CapDemo > 설정·일시정지 > 1. 프리팹 만들기를 실행하세요.");
            return null;
        }
        return Spawn(prefab);
    }

    public string ReturnScene
    {
        get => returnScene;
        set => returnScene = value;
    }

    protected override void Awake()
    {
        base.Awake();
        if (confirmButton) confirmButton.OnClick = Confirm;
        if (cancelButton) cancelButton.OnClick = Cancel;
    }

    // 기본 동작으로 연다 (확인 = 메인화면으로)
    public void Show() => Show(null, null, null);

    // 확인·취소 때 할 일을 직접 준다. 비우면 기본 동작 (확인 = Return Scene으로, 취소 = 닫고 이어 하기)
    // title을 비우면 "일시정지". pauseGame을 false로 주면 게임 시간을 건드리지 않는다
    // (리듬 인게임처럼 이미 자기 방식으로 멈춰 있는 곳에서 확인 창으로만 쓸 때)
    public void Show(System.Action confirm, System.Action cancel, string message, string title = null, bool? pauseGame = null,
        string confirmEn = null)
    {
        if (IsOpen) return;
        SetConfirmEn(string.IsNullOrEmpty(confirmEn) ? defaultConfirmEn : confirmEn);
        onConfirm = confirm;
        onCancel = cancel;
        pausing = pauseGame ?? this.pauseGame;
        if (messageText) messageText.text = string.IsNullOrEmpty(message) ? defaultMessage : message;
        if (titleText) titleText.text = string.IsNullOrEmpty(title) ? defaultTitle : title;
        Open();
    }

    protected override void OnOpened()
    {
        confirmed = false;
        transform.SetAsLastSibling();
        if (pausing) GamePause.Pause();
        SetFocus(1);
    }

    private void Confirm()
    {
        if (confirmed || !IsOpen) return;
        confirmed = true;

        if (onConfirm != null)
        {
            if (pausing) GamePause.Resume();
            HideNow();
            onConfirm.Invoke();
            return;
        }

        // 메인화면으로: 스토리는 멈춘 채로 두고 소리만 켠 뒤 지직 전환. 새 씬이 열리면 GamePause가 풀린다
        GamePause.ReleaseAudio();
        HideNow();
        if (!Application.CanStreamedLevelBeLoaded(returnScene))
        {
            Debug.LogWarning($"[PausePanel] '{returnScene}' 씬을 열 수 없습니다. 씬 이름과 빌드 프로필 씬 목록을 확인하세요.");
            GamePause.Resume();
            return;
        }
        if (!HoloTransition.LoadScene(returnScene)) SceneManager.LoadScene(returnScene);
    }

    private void Cancel()
    {
        if (!IsOpen) return;
        Close();
    }

    protected override void OnClosed()
    {
        if (confirmed) return;
        if (pausing) GamePause.Resume();
        var cancel = onCancel;
        onCancel = null;
        cancel?.Invoke();
    }

    protected override void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Cancel();
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            if (focus == 0) Confirm();
            else Cancel();
        }
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) SetFocus(0);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) SetFocus(1);
    }

    private void SetFocus(int index)
    {
        focus = index;
        if (confirmButton) confirmButton.SetHighlighted(index == 0);
        if (cancelButton) cancelButton.SetHighlighted(index == 1);
    }

    private void OnDisable()
    {
        // 열린 채로 씬이 바뀌거나 지워져도 게임이 멈춘 채로 남지 않게
        if (IsOpen && pausing && !confirmed) GamePause.Resume();
    }

    // ── 만들기 (에디터 메뉴가 부른다) ───────────────────────────────

    public static PausePanel Build(HoloUIStyle style)
    {
        var root = HoloUI.CreateOverlayCanvas("PausePanel", 900);
        var p = root.AddComponent<PausePanel>();
        p.group = root.GetComponent<CanvasGroup>();
        p.openSfx = style.openSfx;
        p.closeSfx = style.closeSfx;

        // 뒤를 어둡게 + 뒤쪽 클릭 막기
        var dim = HoloUI.Image("Dim", root.transform, null, new Color(0f, 0.02f, 0.02f, 0.72f), raycast: true);
        HoloUI.Stretch(dim.rectTransform);

        var panelImg = HoloUI.Image("Panel", root.transform, style.panel, new Color(10 / 255f, 22 / 255f, 22 / 255f, 0.94f), raycast: true);
        p.panel = panelImg.rectTransform;
        p.panel.anchorMin = p.panel.anchorMax = new Vector2(0.5f, 0.5f);
        p.panel.sizeDelta = new Vector2(600f, 270f);
        var line = HoloUI.Image("Line", p.panel, style.panelLine, HoloPalette.Teal.WithAlpha(0.6f));
        HoloUI.Stretch(line.rectTransform);

        // 위 민트 띠 + 태그
        var bar = HoloUI.Image("Bar", p.panel, null, HoloPalette.Teal);
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = new Vector2(1f, 1f);
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.offsetMin = new Vector2(24f, -6f);
        bar.rectTransform.offsetMax = new Vector2(-24f, -3f);

        var tag = HoloUI.Text("Tag", p.panel, style.monoFont, "▌▌  SYSTEM  ·  PAUSE", 13f, HoloPalette.Teal, spacing: 12f);
        HoloUI.Place(tag.rectTransform, 32f, 22f, 520f, 22f);
        p.titleText = HoloUI.Text("Title", p.panel, style.titleFont, "일시정지", 30f, HoloPalette.Ink);
        HoloUI.Place(p.titleText.rectTransform, 32f, 52f, 520f, 44f);
        p.messageText = HoloUI.Text("Message", p.panel, style.titleFont, p.defaultMessage, 20f, HoloPalette.Mute);
        HoloUI.Place(p.messageText.rectTransform, 33f, 100f, 540f, 30f);

        p.confirmButton = HoloUI.RedButton("Confirm", p.panel, style, "확인", "MAIN", 32f, 152f, 256f, 66f);
        p.cancelButton = HoloUI.TealButton("Cancel", p.panel, style, "취소", "RESUME", 312f, 152f, 256f, 66f);

        var hint = HoloUI.Text("Hint", p.panel, style.monoFont, "ENTER 선택  ·  ESC 계속하기  ·  ←→ 고르기", 12f, HoloPalette.Dim,
            TextAlignmentOptions.Right, 4f);
        HoloUI.Place(hint.rectTransform, 32f, 238f, 536f, 20f);

        root.SetActive(false);
        return p;
    }
}
