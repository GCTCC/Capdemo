using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// NewHubDemoScene 하단 메뉴의 버튼 하나. 마우스를 올리면/치우면 HubMenuBar에 알리고, 클릭하면 실행을 부탁한다.
// 실제 선택 표시(붉은 패널 이동, 밝기)와 씬 이동은 HubMenuBar가 맡는다.
public class HubMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("이 버튼을 눌렀을 때 넘어갈 씬 이름. 비워 두면 '준비 중' 로그만 띄운다. 씬은 Build Settings에 등록되어 있어야 한다.")]
    [SerializeField] private string targetScene = "";

    [Tooltip("선택되면 밝아지는 그래픽 (아이콘, 글자)")]
    [SerializeField] private Graphic[] highlightTargets;

    private HubMenuBar bar;

    public string TargetScene => targetScene;
    public Graphic[] HighlightTargets => highlightTargets;

    private void Awake() => bar = GetComponentInParent<HubMenuBar>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (bar) bar.Select(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (bar) bar.Deselect(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (bar) bar.Submit(this);
    }
}
