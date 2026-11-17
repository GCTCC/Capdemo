using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 홀로그램 허브의 왼쪽 메인 메뉴 묶음에 붙인다.
//   - 항상 버튼 하나가 선택돼 있다 (처음엔 Start Index). 마우스를 올리거나 ↑/↓(W/S)로 바꾼다.
//   - 선택된 버튼 옆으로 왼쪽 막대(Cursor)와 오른쪽 연결선(Connector)이 따라간다.
//   - 연결선 끝 글(Connector Text)은 버튼마다 다른 문구가 타자 치듯 나온다.
//   - 클릭/Enter/Space로 실행: 씬이 정해져 있으면 지직 전환(HoloTransition)으로 넘어가고, 없으면 "준비 중" 으로 지직거린다.
public class HoloMenu : MonoBehaviour
{
    [Tooltip("위에서부터 순서대로")]
    [SerializeField] private HoloMenuItem[] items = new HoloMenuItem[0];
    [SerializeField] private int startIndex = 0;

    [Header("따라다니는 것들")]
    [Tooltip("선택된 버튼 왼쪽의 세로 막대")]
    [SerializeField] private RectTransform cursor;
    [Tooltip("선택된 버튼 오른쪽에서 배경 쪽으로 뻗는 연결선 묶음")]
    [SerializeField] private RectTransform connector;
    [SerializeField] private CanvasGroup connectorGroup;
    [SerializeField] private TMP_Text connectorText;
    [Tooltip("따라가는 속도. 클수록 빨리 붙는다.")]
    [SerializeField, Min(0f)] private float followSpeed = 16f;
    [Tooltip("연결선 글이 한 글자 나오는 시간(초)")]
    [SerializeField, Min(0f)] private float typeInterval = 0.018f;

    [Header("준비 중 메뉴")]
    [SerializeField] private string deniedText = "ACCESS DENIED  ·  준비 중인 메뉴입니다";
    [SerializeField] private Color deniedColor = new Color(1f, 0.30f, 0.37f, 1f);

    private int current = -1;
    private float typeTime;
    private float deniedLeft;
    private float connectorFlash;
    private Color captionColor;

    private void Start()
    {
        if (connectorText) captionColor = connectorText.color;
        Select(Mathf.Clamp(startIndex, 0, items.Length - 1), instant: true);
    }

    public void Select(HoloMenuItem item) => Select(System.Array.IndexOf(items, item), false);

    private void Select(int index, bool instant)
    {
        if (index < 0 || index >= items.Length || index == current) return;
        current = index;
        for (int i = 0; i < items.Length; i++)
            if (items[i]) items[i].Hover.SetHighlighted(i == current, instant);

        if (instant) SnapFollowers();
        ShowCaption(items[current].Caption, captionColor);
        if (!instant) connectorFlash = 0.18f;
    }

    public void Submit(HoloMenuItem item)
    {
        if (!item || HoloTransition.IsBusy || HoloPopup.AnyOpen || HoloQuitDialog.IsOpen) return;
        Select(item);

        // 설정처럼 화면 위에 창을 띄우는 메뉴
        if (item.PanelPrefab)
        {
            item.OpenPanel();
            return;
        }

        string scene = item.TargetScene;
        if (string.IsNullOrEmpty(scene))
        {
            Deny(item);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[HoloHub] '{scene}' 씬을 열 수 없습니다. 씬 이름이 맞는지, 빌드 프로필 씬 목록에 있는지 확인하세요.");
            Deny(item);
            return;
        }

        // 곡 선택 화면에 "끝나면 이 허브로 돌아와" 라고 알려 준다
        if (item.OpensSongSelect)
            RhythmLaunchContext.RequestSingle(string.Empty, SceneManager.GetActiveScene().name);

        HoloTransition.LoadScene(scene);
    }

    private void Deny(HoloMenuItem item)
    {
        item.Hover.Glitch(0.35f);
        ShowCaption(deniedText, deniedColor);
        deniedLeft = 1.4f;
        connectorFlash = 0.3f;
    }

    private void ShowCaption(string text, Color color)
    {
        if (!connectorText) return;
        connectorText.text = text;
        connectorText.color = color;
        connectorText.maxVisibleCharacters = 0;
        typeTime = 0f;
    }

    private void Update()
    {
        if (items.Length == 0 || current < 0) return;
        float dt = Time.unscaledDeltaTime;

        var keyboard = Keyboard.current;
        // 종료 확인 창·설정 창이 떠 있으면 메뉴는 키를 받지 않는다
        if (keyboard != null && !HoloTransition.IsBusy && !HoloQuitDialog.IsOpen && !HoloPopup.AnyOpen)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                Select((current - 1 + items.Length) % items.Length, false);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                Select((current + 1) % items.Length, false);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                Submit(items[current]);
        }

        // 막대와 연결선이 선택된 버튼 높이로 부드럽게 따라간다
        float t = followSpeed > 0f ? 1f - Mathf.Exp(-followSpeed * dt) : 1f;
        float y = ItemCenterY(current);
        if (cursor) cursor.position = new Vector3(cursor.position.x, Mathf.Lerp(cursor.position.y, y, t), cursor.position.z);
        if (connector) connector.position = new Vector3(connector.position.x, Mathf.Lerp(connector.position.y, y, t), connector.position.z);

        // 연결선: 선택이 바뀐 직후 잠깐 치직
        // (처음 나타날 때는 HoloEntrance가 투명도를 맡으므로, 치직거릴 때만 건드린다)
        if (connectorGroup && connectorFlash > 0f)
        {
            connectorFlash -= dt;
            connectorGroup.alpha = connectorFlash > 0f ? (Random.value < 0.5f ? 0.25f : 1f) : 1f;
        }

        // 글자가 하나씩 찍힌다
        if (connectorText)
        {
            typeTime += dt;
            connectorText.maxVisibleCharacters = typeInterval > 0f ? Mathf.FloorToInt(typeTime / typeInterval) : 99999;
        }

        // "준비 중" 문구는 잠깐 보였다가 원래 문구로 돌아간다
        if (deniedLeft > 0f)
        {
            deniedLeft -= dt;
            if (deniedLeft <= 0f) ShowCaption(items[current].Caption, captionColor);
        }
    }

    private void SnapFollowers()
    {
        float y = ItemCenterY(current);
        if (cursor) cursor.position = new Vector3(cursor.position.x, y, cursor.position.z);
        if (connector) connector.position = new Vector3(connector.position.x, y, connector.position.z);
    }

    // 버튼의 세로 가운데 (월드 좌표)
    private float ItemCenterY(int index)
    {
        var rt = (RectTransform)items[index].transform;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0].y + corners[1].y) * 0.5f;
    }
}
