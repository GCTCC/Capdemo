using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 설정 창의 음량 막대. 막대를 누르거나 끌면 0~1 값이 바뀌고 OnChanged를 부른다.
// ←/→ 대신 마우스 휠로도 5%씩 바꿀 수 있다.
public class HoloSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform track;  // 눌러서 값을 고르는 영역
    [SerializeField] private RectTransform fill;   // 채워지는 막대 (anchorMax.x로 길이를 바꾼다)
    [SerializeField] private RectTransform handle; // 막대 끝 손잡이
    [SerializeField] private TMP_Text valueText;   // 오른쪽 숫자 (0~100)
    [SerializeField] private Graphic glow;         // 마우스를 올리면 밝아지는 테두리

    public System.Action<float> OnChanged;
    public System.Action OnReleased;

    private float value = 1f;
    private bool hovered, dragging;

    public float Value => value;

    public void SetValue(float v, bool notify = false)
    {
        value = Mathf.Clamp01(v);
        if (fill) fill.anchorMax = new Vector2(value, 1f);
        if (handle) handle.anchorMin = handle.anchorMax = new Vector2(value, 0.5f);
        if (valueText) valueText.text = Mathf.RoundToInt(value * 100f).ToString();
        if (notify) OnChanged?.Invoke(value);
    }

    public void OnPointerDown(PointerEventData e) { dragging = true; Pick(e); }
    public void OnDrag(PointerEventData e) => Pick(e);

    public void OnPointerUp(PointerEventData e)
    {
        dragging = false;
        OnReleased?.Invoke();
    }

    public void OnScroll(PointerEventData e)
    {
        SetValue(value + Mathf.Sign(e.scrollDelta.y) * 0.05f, true);
        OnReleased?.Invoke();
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;

    private void Pick(PointerEventData e)
    {
        if (!track) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local)) return;
        var r = track.rect;
        SetValue(Mathf.InverseLerp(r.xMin, r.xMax, local.x), true);
    }

    private void Update()
    {
        if (glow)
        {
            var c = glow.color;
            c.a = Mathf.MoveTowards(c.a, hovered || dragging ? 0.9f : 0.3f, Time.unscaledDeltaTime * 6f);
            glow.color = c;
        }
    }

    // ── 만들기 ──
    public static HoloSlider Build(Transform parent, HoloUIStyle style, float x, float y, float w)
    {
        const float h = 36f;
        var root = HoloUI.Node("Slider", parent, x, y, w + 80f, h);
        var hit = HoloUI.Image("Hit", root, null, new Color(0, 0, 0, 0), raycast: true); // 누르는 영역 (투명)
        HoloUI.Place(hit.rectTransform, 0f, 0f, w, h);

        var trackImg = HoloUI.Image("Track", root, null, HoloPalette.TealDeep);
        HoloUI.Place(trackImg.rectTransform, 0f, h * 0.5f - 4f, w, 8f);
        var glow = HoloUI.Image("Glow", trackImg.transform, style.buttonLine, HoloPalette.Teal.WithAlpha(0.3f));
        HoloUI.Stretch(glow.rectTransform);
        glow.rectTransform.offsetMin = new Vector2(-3f, -3f);
        glow.rectTransform.offsetMax = new Vector2(3f, 3f);

        var fillImg = HoloUI.Image("Fill", trackImg.transform, null, HoloPalette.Teal);
        fillImg.rectTransform.anchorMin = Vector2.zero;
        fillImg.rectTransform.anchorMax = Vector2.one;
        fillImg.rectTransform.offsetMin = fillImg.rectTransform.offsetMax = Vector2.zero;

        // 눈금 (10%마다)
        for (int i = 1; i < 10; i++)
        {
            var tick = HoloUI.Image("Tick" + i, trackImg.transform, null, new Color(0f, 0f, 0f, 0.35f));
            tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(i / 10f, 0.5f);
            tick.rectTransform.sizeDelta = new Vector2(2f, 8f);
        }

        var handleImg = HoloUI.Image("Handle", trackImg.transform, null, HoloPalette.Ink);
        handleImg.rectTransform.sizeDelta = new Vector2(6f, 26f);

        var num = HoloUI.Text("Value", root, style.monoFont, "100", 20f, HoloPalette.Teal, TextAlignmentOptions.Right);
        HoloUI.Place(num.rectTransform, w + 10f, 0f, 64f, h);

        var s = root.gameObject.AddComponent<HoloSlider>();
        s.track = hit.rectTransform;
        s.fill = fillImg.rectTransform;
        s.handle = handleImg.rectTransform;
        s.valueText = num;
        s.glow = glow;
        s.SetValue(1f);
        return s;
    }
}
