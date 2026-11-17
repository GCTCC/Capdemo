using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 홀로그램 톤 창(설정·일시정지·Auto 버튼)을 만들 때 쓰는 글꼴·그림 묶음.
// 에디터 메뉴(Tools > CapDemo > 설정·일시정지)가 허브와 같은 재료로 채워 넘긴다.
[System.Serializable]
public class HoloUIStyle
{
    public TMP_FontAsset titleFont;  // 큰 한글 제목 (경기천년제목)
    public TMP_FontAsset monoFont;   // 작은 영문·숫자 (neodgm)
    public Sprite panel;             // 창 바탕 (모서리 잘린 패널)
    public Sprite panelLine;         // 창 테두리 선
    public Sprite button;            // 버튼 바탕
    public Sprite buttonLine;        // 버튼 테두리 선
    public AudioClip openSfx;        // 창이 열릴 때
    public AudioClip closeSfx;       // 창이 닫힐 때
}

// 창을 코드로 조립할 때 쓰는 도우미들. 좌표는 부모 왼쪽 위 기준 픽셀(1920 x 1080 화면).
public static class HoloUI
{
    // 화면 위에 따로 뜨는 캔버스(1920 x 1080 기준). 어느 씬에 넣어도 같은 크기로 보인다
    public static GameObject CreateOverlayCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    // 버튼을 누를 수 있게 씬에 EventSystem이 없으면 만든다
    public static void EnsureEventSystem()
    {
        if (EventSystem.current || Object.FindAnyObjectByType<EventSystem>()) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        if (sprite && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    public static RectTransform Node(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Place(rt, x, y, w, h);
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 부모 왼쪽 위 기준
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 가운데 기준으로 커지게 피벗만 가운데로 옮긴다 (위치는 그대로)
    public static void CenterPivot(RectTransform rt)
    {
        var size = rt.sizeDelta;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
    }

    // 확인 창 버튼 (한글 + 작은 영문). 마우스를 올리면 커지고 색이 바뀐다
    public static HoloDialogButton Button(string name, Transform parent, HoloUIStyle style, string label, string en,
        float x, float y, float w, float h, Color bg, Color bgHover, Color text, Color textHover, float labelSize = 24f)
    {
        var img = Image(name, parent, style.button, bg, raycast: true);
        Place(img.rectTransform, x, y, w, h);
        CenterPivot(img.rectTransform);
        var line = Image("Line", img.transform, style.buttonLine, textHover.WithAlpha(0.35f));
        Stretch(line.rectTransform);

        var labels = new System.Collections.Generic.List<Graphic>();
        if (string.IsNullOrEmpty(en))
        {
            var t = Text("Label", img.transform, style.titleFont, label, labelSize, text, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            labels.Add(t);
        }
        else
        {
            var t = Text("Label", img.transform, style.titleFont, label, labelSize, text);
            Place(t.rectTransform, 22f, 0f, w * 0.6f, h);
            var e = Text("En", img.transform, style.monoFont, en, 12f, text, TextAlignmentOptions.Right, 12f);
            Place(e.rectTransform, w * 0.45f, 0f, w * 0.55f - 20f, h);
            labels.Add(t);
            labels.Add(e);
        }

        var b = img.gameObject.AddComponent<HoloDialogButton>();
        b.background = img;
        b.labels = labels.ToArray();
        b.normalBg = bg;
        b.hoverBg = bgHover;
        b.normalText = text;
        b.hoverText = textHover;
        return b;
    }

    // 민트(기본) 버튼 / 빨간(위험) 버튼 색
    public static HoloDialogButton TealButton(string name, Transform parent, HoloUIStyle style, string label, string en,
        float x, float y, float w, float h, float labelSize = 24f) =>
        Button(name, parent, style, label, en, x, y, w, h,
            HoloPalette.Panel2, HoloPalette.Teal.WithAlpha(0.93f), HoloPalette.Ink, HoloPalette.DarkInk, labelSize);

    public static HoloDialogButton RedButton(string name, Transform parent, HoloUIStyle style, string label, string en,
        float x, float y, float w, float h, float labelSize = 24f) =>
        Button(name, parent, style, label, en, x, y, w, h,
            new Color(40 / 255f, 10 / 255f, 14 / 255f, 0.85f), HoloPalette.Red, new Color(1f, 0.84f, 0.85f), Color.white, labelSize);
}
