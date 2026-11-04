using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// NewHubDemoScene 하단 메뉴 바에 붙인다.
// 마우스를 올린 버튼 뒤에만 붉은 패널(selectedPanel)을 버튼 칸 크기에 꽉 차게 깔고, 그 버튼의 아이콘·글자를 밝게 한다.
// 아무 버튼에도 올려져 있지 않으면 패널을 숨긴다.
// 키보드 ←/→로도 고를 수 있고, 클릭이나 Enter/Space로 실행한다.
public class HubMenuBar : MonoBehaviour
{
    [Tooltip("선택된 버튼 뒤에 깔리는 붉은 패널. 버튼들보다 먼저(위쪽 자식으로) 두어야 뒤에 그려진다.")]
    [SerializeField] private RectTransform selectedPanel;

    [Tooltip("왼쪽부터 순서대로")]
    [SerializeField] private HubMenuButton[] buttons;

    [Tooltip("선택되지 않은 버튼의 아이콘·글자 색")]
    [SerializeField] private Color normalColor = new Color(0.72f, 0.72f, 0.72f, 1f);

    [Tooltip("선택된 버튼의 아이콘·글자 색")]
    [SerializeField] private Color selectedColor = Color.white;

    [Tooltip("붉은 패널이 따라가는 속도. 클수록 빨리 붙는다. 0이면 바로 이동.")]
    [SerializeField, Min(0f)] private float panelFollowSpeed = 18f;

    // -1이면 아무것도 선택되지 않은 상태
    private int current = -1;

    private void Start()
    {
        if (buttons == null) buttons = new HubMenuButton[0];
        ApplySelection(-1);
    }

    private void Update()
    {
        if (buttons.Length == 0) return;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame)
                ApplySelection(current < 0 ? buttons.Length - 1 : (current - 1 + buttons.Length) % buttons.Length);
            if (keyboard.rightArrowKey.wasPressedThisFrame)
                ApplySelection(current < 0 ? 0 : (current + 1) % buttons.Length);
            if (current >= 0 && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                Submit(buttons[current]);
        }

        // 붉은 패널을 선택된 버튼 칸의 위치·크기로 부드럽게 맞춘다
        if (selectedPanel && current >= 0)
        {
            var target = (RectTransform)buttons[current].transform;
            float t = panelFollowSpeed > 0f ? 1f - Mathf.Exp(-panelFollowSpeed * Time.unscaledDeltaTime) : 1f;
            selectedPanel.position = Vector3.Lerp(selectedPanel.position, target.position, t);
            selectedPanel.sizeDelta = Vector2.Lerp(selectedPanel.sizeDelta, target.rect.size, t);
        }
    }

    // 버튼 사이를 옮기면 "이전 버튼에서 나감 → 새 버튼에 들어옴"이 같은 프레임에 차례로 온다.
    // 그래서 프레임 끝(LateUpdate)까지 기다렸다가 그때도 선택이 없을 때만 패널을 숨긴다.
    private void LateUpdate()
    {
        if (selectedPanel && current < 0 && selectedPanel.gameObject.activeSelf)
            selectedPanel.gameObject.SetActive(false);
    }

    public void Select(HubMenuButton button) => ApplySelection(System.Array.IndexOf(buttons, button));

    // 마우스가 버튼에서 벗어나면 선택을 푼다 (다른 버튼으로 바로 옮겨간 경우는 그대로)
    public void Deselect(HubMenuButton button)
    {
        if (current >= 0 && buttons[current] == button) ApplySelection(-1);
    }

    private void ApplySelection(int index)
    {
        if (index >= buttons.Length) index = -1;
        current = index;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i] || buttons[i].HighlightTargets == null) continue;
            foreach (var graphic in buttons[i].HighlightTargets)
                if (graphic) graphic.color = i == current ? selectedColor : normalColor;
        }

        // 숨어 있다가 나타날 때는 미끄러지지 않고 그 버튼 자리에 바로 놓는다.
        // 숨기는 건 LateUpdate에서 한다 (버튼 사이를 옮길 때 잠깐 꺼졌다 켜지며 순간이동하지 않게)
        if (selectedPanel && current >= 0 && !selectedPanel.gameObject.activeSelf)
        {
            selectedPanel.gameObject.SetActive(true);
            var target = (RectTransform)buttons[current].transform;
            selectedPanel.position = target.position;
            selectedPanel.sizeDelta = target.rect.size;
        }
    }

    public void Submit(HubMenuButton button)
    {
        if (!button) return;
        Select(button);

        string scene = button.TargetScene;
        if (string.IsNullOrEmpty(scene))
        {
            Debug.Log($"[NewHub] '{button.name}' 메뉴는 아직 준비 중입니다.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[NewHub] '{scene}' 씬을 열 수 없습니다. 씬 이름이 맞는지, Build Settings에 등록되어 있는지 확인하세요.");
            return;
        }
        SceneManager.LoadScene(scene);
    }
}
