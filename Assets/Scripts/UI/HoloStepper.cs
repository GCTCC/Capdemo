using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 창의 "◀  값  ▶" 고르기. 화살표를 누르면 OnStep(-1 또는 +1)을 부르고, 돌려받은 글을 가운데에 쓴다.
// (배속·싱크·화면 모드·해상도·프레임 제한처럼 정해진 값 중에서 고르는 항목)
public class HoloStepper : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private HoloDialogButton prevButton;
    [SerializeField] private HoloDialogButton nextButton;

    // 방향(-1/+1)을 받아 값을 바꾸고, 새로 보여 줄 글을 돌려준다
    public System.Func<int, string> OnStep;

    private float flash;

    private void Awake()
    {
        if (prevButton) prevButton.OnClick = () => Step(-1);
        if (nextButton) nextButton.OnClick = () => Step(1);
    }

    public void Step(int dir)
    {
        if (OnStep == null) return;
        SetText(OnStep(dir));
        flash = 0.15f;
    }

    public void SetText(string text)
    {
        if (valueText) valueText.text = text;
    }

    private void Update()
    {
        if (!valueText) return;
        // 바뀐 순간 잠깐 치직
        if (flash > 0f)
        {
            flash -= Time.unscaledDeltaTime;
            valueText.alpha = flash > 0f && Random.value < 0.5f ? 0.35f : 1f;
        }
    }

    // ── 만들기 ──
    public static HoloStepper Build(Transform parent, HoloUIStyle style, float x, float y, float w)
    {
        const float h = 44f;
        var root = HoloUI.Node("Stepper", parent, x, y, w, h);
        var bg = HoloUI.Image("Bg", root, style.button, HoloPalette.Panel2);
        HoloUI.Stretch(bg.rectTransform);
        var line = HoloUI.Image("Line", root, style.buttonLine, HoloPalette.Teal.WithAlpha(0.25f));
        HoloUI.Stretch(line.rectTransform);

        var s = root.gameObject.AddComponent<HoloStepper>();
        s.prevButton = Arrow(root, style, "Prev", "◀", 0f, h);
        s.nextButton = Arrow(root, style, "Next", "▶", w - h, h);
        s.valueText = HoloUI.Text("Value", root, style.titleFont, "-", 20f, HoloPalette.Ink, TextAlignmentOptions.Center);
        HoloUI.Place(s.valueText.rectTransform, h, 0f, w - h * 2f, h);
        return s;
    }

    private static HoloDialogButton Arrow(RectTransform parent, HoloUIStyle style, string name, string glyph, float x, float h)
    {
        var img = HoloUI.Image(name, parent, null, new Color(0f, 0f, 0f, 0f), raycast: true);
        HoloUI.Place(img.rectTransform, x, 0f, h, h);
        HoloUI.CenterPivot(img.rectTransform);
        var t = HoloUI.Text("Glyph", img.transform, style.monoFont, glyph, 16f, HoloPalette.Teal, TextAlignmentOptions.Center);
        HoloUI.Stretch(t.rectTransform);
        var b = img.gameObject.AddComponent<HoloDialogButton>();
        b.background = img;
        b.labels = new Graphic[] { t };
        b.normalBg = new Color(0f, 0f, 0f, 0f);
        b.hoverBg = HoloPalette.Teal.WithAlpha(0.9f);
        b.normalText = HoloPalette.Teal;
        b.hoverText = HoloPalette.DarkInk;
        b.hoverScale = 1f;
        return b;
    }
}
