using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 확인 창 안의 버튼 하나. 마우스를 올리면 살짝 커지고 배경·글자 색이 바뀌며, 클릭하면 OnClick을 부른다.
// (HoloQuitDialog가 실행 중에 만들어 붙인다)
public class HoloDialogButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public System.Action OnClick;
    public Graphic background;
    public Graphic[] labels = new Graphic[0];
    public Color normalBg, hoverBg, normalText, hoverText;
    public float hoverScale = 1.06f;

    private bool hovered;
    private float blend;

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) OnClick?.Invoke();
    }

    // 키보드로 고를 때도 같은 모양으로 보이게
    public void SetHighlighted(bool on) => hovered = on;

    // 탭처럼 "골라진 상태"를 마우스가 떠나도 유지한다 (설정 창 탭, Auto 켜짐)
    public bool Selected { get; set; }

    private void Update()
    {
        blend = Mathf.MoveTowards(blend, hovered || Selected ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, 1f - (1f - blend) * (1f - blend));
        if (background) background.color = Color.Lerp(normalBg, hoverBg, blend);
        foreach (var g in labels) if (g) g.color = Color.Lerp(normalText, hoverText, blend);
    }
}
