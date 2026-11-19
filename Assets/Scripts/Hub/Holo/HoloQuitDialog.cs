using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// "정말로 종료하시겠습니까?" 확인 창. 게임 종료 버튼(HoloQuitButton)이 처음 눌릴 때 화면 위에 만들어진다.
//  - 종료: 화면이 지직거리며 꺼진 뒤 게임을 끈다 (에디터에서는 Play가 멈춘다)
//  - 취소: 창이 지직거리며 닫히고 허브로 돌아온다
//  - 키보드: Enter = 종료, Esc = 취소, ←/→ = 버튼 고르기
// 창이 열려 있는 동안은 뒤의 메뉴를 누를 수 없다 (HoloMenu도 IsOpen을 보고 키 입력을 멈춘다).
public class HoloQuitDialog : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private static readonly Color PanelColor = new Color(10 / 255f, 22 / 255f, 22 / 255f, 0.94f);

    private CanvasGroup group;
    private RectTransform panel;
    private HoloDialogButton quitButton, cancelButton;
    private int focus = 1; // 0 종료, 1 취소 (실수로 꺼지지 않게 취소가 먼저)
    private float anim;    // 0 닫힘 ~ 1 열림
    private bool opening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => IsOpen = false;

    // canvas 위에 창을 만든다. 글꼴·패널 그림은 종료 버튼 쪽에서 넘겨받는다.
    public static HoloQuitDialog Create(Canvas canvas, TMP_FontAsset titleFont, TMP_FontAsset monoFont, Sprite panelSprite, Sprite lineSprite)
    {
        var root = new GameObject("QuitDialog", typeof(RectTransform), typeof(CanvasGroup), typeof(HoloQuitDialog));
        root.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var dialog = root.GetComponent<HoloQuitDialog>();
        dialog.group = root.GetComponent<CanvasGroup>();
        dialog.Build(titleFont, monoFont, panelSprite, lineSprite);
        dialog.group.alpha = 0f;
        dialog.gameObject.SetActive(false);
        return dialog;
    }

    public void Open()
    {
        if (HoloTransition.IsBusy) return;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        IsOpen = true;
        opening = true;
        anim = 0f;
        SetFocus(1);
    }

    private void Close()
    {
        opening = false;
    }

    private void Quit()
    {
        IsOpen = false;
        gameObject.SetActive(false);
        HoloTransition.QuitGame();
    }

    private void Update()
    {
        // 지직거리며 열리고 닫힌다
        float goal = opening ? 1f : 0f;
        anim = Mathf.MoveTowards(anim, goal, Time.unscaledDeltaTime / 0.18f);
        float flicker = anim < 0.999f && Random.value < 0.35f ? Random.Range(0.3f, 0.8f) : 1f;
        group.alpha = anim * flicker;
        float sy = Mathf.Lerp(0.02f, 1f, 1f - (1f - anim) * (1f - anim));
        panel.localScale = new Vector3(Mathf.Lerp(0.9f, 1f, anim), sy, 1f);
        panel.anchoredPosition = anim < 0.999f && Random.value < 0.3f ? new Vector2(Random.Range(-8f, 8f), 0f) : Vector2.zero;
        if (!opening && anim <= 0f)
        {
            IsOpen = false;
            gameObject.SetActive(false);
            return;
        }
        if (!opening) return;

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            if (focus == 0) Quit();
            else Close();
        }
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) SetFocus(0);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) SetFocus(1);
    }

    private void SetFocus(int index)
    {
        focus = index;
        if (quitButton) quitButton.SetHighlighted(index == 0);
        if (cancelButton) cancelButton.SetHighlighted(index == 1);
    }

    // ── 만들기 ────────────────────────────────────────────────

    private void Build(TMP_FontAsset titleFont, TMP_FontAsset monoFont, Sprite panelSprite, Sprite lineSprite)
    {
        // 뒤를 어둡게 + 클릭 막기 (빈 곳을 누르면 취소)
        var dim = MakeImage("Dim", transform, null, new Color(0f, 0.02f, 0.02f, 0.72f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<HoloDialogButton>();
        dimButton.hoverScale = 1f;
        dimButton.normalBg = dimButton.hoverBg = dim.color;
        dimButton.background = dim;
        dimButton.OnClick = Close;

        var panelImg = MakeImage("Panel", transform, panelSprite, PanelColor);
        panel = panelImg.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(560f, 250f);
        panelImg.raycastTarget = true;
        var line = MakeImage("Line", panel, lineSprite, HoloPalette.Red.WithAlpha(0.7f));
        Stretch(line.rectTransform);

        // 위 빨간 띠 + 경고 문구
        var bar = MakeImage("Bar", panel, null, HoloPalette.Red);
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = new Vector2(1f, 1f);
        bar.rectTransform.pivot = new Vector2(0.5f, 1f);
        bar.rectTransform.offsetMin = new Vector2(24f, -6f);
        bar.rectTransform.offsetMax = new Vector2(-24f, -3f);

        var tag = MakeText("Tag", panel, monoFont, "●  SYSTEM  ·  SHUTDOWN", 13f, HoloPalette.Red, TextAlignmentOptions.Left);
        tag.characterSpacing = 12f;
        Place(tag.rectTransform, 32f, 22f, 480f, 22f);
        var title = MakeText("Title", panel, titleFont, "정말로 종료하시겠습니까?", 30f, HoloPalette.Ink, TextAlignmentOptions.Left);
        Place(title.rectTransform, 32f, 54f, 500f, 44f);
        var sub = MakeText("Sub", panel, monoFont, "저장되지 않은 진행은 사라질 수 있어요.", 14f, HoloPalette.Mute, TextAlignmentOptions.Left);
        Place(sub.rectTransform, 33f, 100f, 500f, 24f);

        quitButton = MakeButton("Quit", panel, panelSprite, titleFont, monoFont, "종료", "EXIT", 32f,
            new Color(40 / 255f, 10 / 255f, 14 / 255f, 0.85f), HoloPalette.Red, new Color(1f, 0.84f, 0.85f), Color.white);
        quitButton.OnClick = Quit;
        cancelButton = MakeButton("Cancel", panel, panelSprite, titleFont, monoFont, "취소", "CANCEL", 288f,
            HoloPalette.Panel, HoloPalette.Teal.WithAlpha(0.93f), HoloPalette.Ink, HoloPalette.DarkInk);
        cancelButton.OnClick = Close;

        var hint = MakeText("Hint", panel, monoFont, "ENTER 선택  ·  ESC 취소  ·  ←→ 고르기", 12f, HoloPalette.Dim, TextAlignmentOptions.Right);
        hint.characterSpacing = 4f;
        Place(hint.rectTransform, 32f, 222f, 496f, 20f);
    }

    private HoloDialogButton MakeButton(string name, Transform parent, Sprite sprite, TMP_FontAsset titleFont, TMP_FontAsset monoFont,
        string label, string en, float x, Color bg, Color bgHover, Color text, Color textHover)
    {
        var img = MakeImage(name, parent, sprite, bg);
        img.raycastTarget = true;
        Place(img.rectTransform, x, 140f, 240f, 66f);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition += new Vector2(120f, -33f);

        var t = MakeText("Label", img.transform, titleFont, label, 24f, text, TextAlignmentOptions.Left);
        Place(t.rectTransform, 24f, 8f, 120f, 50f);
        var e = MakeText("En", img.transform, monoFont, en, 12f, text, TextAlignmentOptions.Right);
        e.characterSpacing = 12f;
        Place(e.rectTransform, 110f, 8f, 110f, 50f);

        var b = img.gameObject.AddComponent<HoloDialogButton>();
        b.background = img;
        b.labels = new Graphic[] { t, e };
        b.normalBg = bg;
        b.hoverBg = bgHover;
        b.normalText = text;
        b.hoverText = textHover;
        return b;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 부모 왼쪽 위 기준
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
