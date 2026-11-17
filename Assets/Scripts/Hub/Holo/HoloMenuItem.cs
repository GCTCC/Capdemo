using UnityEngine;
using UnityEngine.EventSystems;

// 홀로그램 허브 메인 메뉴의 버튼 하나. 마우스를 올리면 HoloMenu에 "나 선택" 을, 클릭하면 "실행" 을 부탁한다.
// 실제 하이라이트(커지고 민트로 바뀜)는 같은 오브젝트의 HoloHover가, 선택 막대·연결선·씬 이동은 HoloMenu가 맡는다.
[RequireComponent(typeof(HoloHover))]
public class HoloMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Tooltip("눌렀을 때 넘어갈 씬 이름. 비워 두면 '준비 중' 으로 지직거리기만 한다. 씬은 빌드 프로필 씬 목록에 있어야 한다.")]
    [SerializeField] private string targetScene = "";

    [Tooltip("싱글 플레이처럼 곡 선택 화면으로 갈 때 켠다 (돌아올 씬으로 이 허브를 알려 준다)")]
    [SerializeField] private bool opensSongSelect = false;

    [Tooltip("선택됐을 때 오른쪽 연결선 끝에 뜨는 글")]
    [SerializeField] private string caption = "";

    [Tooltip("씬을 넘기지 않고 지금 화면 위에 띄울 창 프리팹 (예: 설정 = Assets/Prefabs/UI/SettingsPanel.prefab). 넣어 두면 Target Scene보다 먼저 쓴다")]
    [SerializeField] private HoloPopup panelPrefab;

    private HoloMenu menu;

    public string TargetScene => targetScene;
    public bool OpensSongSelect => opensSongSelect;
    public string Caption => caption;
    public HoloPopup PanelPrefab => panelPrefab;

    // 처음 누를 때 만들어 두고 다음부터는 다시 연다
    private HoloPopup panelInstance;

    public void OpenPanel()
    {
        if (!panelPrefab) return;
        if (!panelInstance) panelInstance = HoloPopup.Spawn(panelPrefab);
        panelInstance.Open();
    }
    public HoloHover Hover { get; private set; }

    private void Awake()
    {
        Hover = GetComponent<HoloHover>();
        menu = GetComponentInParent<HoloMenu>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu) menu.Select(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (menu) menu.Submit(this);
    }
}
