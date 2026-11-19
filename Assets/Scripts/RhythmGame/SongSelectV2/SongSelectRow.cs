using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 곡 목록의 한 줄 (번호 · 작은 커버 · 곡 이름 · BPM/NEW · 랭크 · 클리어 램프).
// 씬에는 꺼둔 견본 줄(Row Template) 하나만 있고, 곡 수만큼 복사해서 쓴다.
// 선택되면: 줄이 커지고 왼쪽으로 튀어나오며(0.15초), 테두리가 빛나고, 왼쪽 민트 막대·곡 이름 색번짐 글리치·
//          파형(재생된 부분이 밝음)·재생 배지(00:14 / 00:30)가 나타난다.
// 마우스만 올리면 줄이 조금 밝아질 뿐 재생은 하지 않는다 (재생은 선택했을 때만).
public class SongSelectRow : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum Lamp { None, Clear, FullCombo }

    [Header("연결")]
    [SerializeField] private Image background;
    [Tooltip("선택될 때 왼쪽으로 튀어나오는 내용물 묶음")]
    [SerializeField] private RectTransform body;
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private RawImage cover;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("곡 이름 뒤 빨강·하늘 그림자 (선택됐을 때 색번짐)")]
    [SerializeField] private TMP_Text titleRed;
    [SerializeField] private TMP_Text titleCyan;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Image lamp;
    [SerializeField] private GameObject selectedMark;
    [SerializeField] private Image glow;
    [SerializeField] private RectTransform waveRoot;
    [SerializeField] private GameObject badge;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private LayoutElement layout;

    [Header("평소")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Color normalColor = new Color(0.64f, 0.92f, 0.84f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.86f, 0.93f, 0.91f, 1f);

    [Header("마우스 올렸을 때")]
    [SerializeField] private Color hoverColor = new Color(0.80f, 1f, 0.95f, 1f);

    [Header("선택됐을 때")]
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Color selectedColor = new Color(0.75f, 1f, 0.93f, 1f);
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private float normalHeight = 83f;
    [SerializeField] private float selectedHeight = 115f;
    [Tooltip("선택되면 왼쪽으로 튀어나오는 거리")]
    [SerializeField] private float selectedShift = 22f;
    [SerializeField, Min(0.01f)] private float popTime = 0.15f;

    [Header("램프 색")]
    [SerializeField] private Color lampNone = new Color(0.45f, 0.50f, 0.50f, 0.6f);
    [SerializeField] private Color lampClear = new Color(0.64f, 0.92f, 0.84f, 1f);
    [SerializeField] private Color lampFullCombo = new Color(1f, 0.71f, 0.36f, 1f);

    [Header("파형")]
    [SerializeField, Min(8)] private int waveBars = 80;
    [SerializeField] private Color wavePlayed = new Color(0.64f, 0.92f, 0.84f, 1f);
    [SerializeField] private Color waveRest = new Color(0.64f, 0.92f, 0.84f, 0.3f);

    [Header("곡 줄 왼쪽에서 연결선이 닿는 위치 (줄 높이 기준 0~1)")]
    [SerializeField, Range(0f, 1f)] private float linkHeight = 0.5f;

    public int Index { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    private UnityAction<SongSelectRow> clicked;
    private bool selected;
    private bool hovered;
    private float pop;          // 0 = 평소, 1 = 선택 모양
    private Image[] waveImages;
    private float[] waveValues;
    private float glitchLeft;
    private float nextGlitch;
    private Vector2 bodyBase;
    private bool bodyBaseSet;

    // 예전 방식 호출 (번호·이름·커버만)
    public void Setup(int index, string title, Texture coverTexture, UnityAction<SongSelectRow> onClick) =>
        Setup(index, title, coverTexture, "", "", Lamp.None, false, onClick);

    public void Setup(int index, string title, Texture coverTexture, string bpmLabel, string rank, Lamp lampState, bool isNew,
        UnityAction<SongSelectRow> onClick)
    {
        Index = index;
        clicked = onClick;
        if (numberText) numberText.text = (index + 1).ToString("00");
        if (titleText) titleText.text = title;
        if (titleRed) titleRed.text = title;
        if (titleCyan) titleCyan.text = title;
        if (cover)
        {
            cover.texture = coverTexture;
            cover.uvRect = SquareCrop(coverTexture);
        }
        SetInfo(bpmLabel, isNew);
        if (rankText) rankText.text = string.IsNullOrEmpty(rank) ? "—" : rank;
        if (lamp) lamp.color = lampState == Lamp.FullCombo ? lampFullCombo : lampState == Lamp.Clear ? lampClear : lampNone;
        if (body && !bodyBaseSet)
        {
            bodyBase = body.anchoredPosition;
            bodyBaseSet = true;
        }
        SetSelected(false);
        pop = 0f;
        ApplyPop();
    }

    // BPM 줄. 새 곡이면 빨간 ● NEW
    public void SetInfo(string bpmLabel, bool isNew)
    {
        if (!subText) return;
        subText.text = isNew ? "<color=#FF4D5E>● NEW</color>" : bpmLabel;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        Refresh();
    }

    // 미리듣기 파형 (0~1 값들). 선택된 줄에서만 보인다
    public void SetWaveform(float[] values)
    {
        waveValues = values;
        BuildWave();
    }

    // 미리듣기 진행: 파형의 재생된 부분을 밝히고 배지 시간을 바꾼다
    public void SetPlayback(float progress, string label)
    {
        if (badgeText) badgeText.text = label;
        if (waveImages == null) return;
        int lit = Mathf.RoundToInt(Mathf.Clamp01(progress) * waveImages.Length);
        for (int i = 0; i < waveImages.Length; i++)
            if (waveImages[i]) waveImages[i].color = i < lit ? wavePlayed : waveRest;
    }

    // 연결선 끝점 (줄 왼쪽 가장자리, 튀어나온 만큼 포함)
    public Vector3 LinkPointWorld()
    {
        var target = body ? body : Rect;
        var r = target.rect;
        return target.TransformPoint(new Vector3(r.xMin, Mathf.Lerp(r.yMin, r.yMax, linkHeight), 0f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(); }

    private void Update()
    {
        // 튀어나오기 (ease-out)
        float goal = selected ? 1f : 0f;
        if (!Mathf.Approximately(pop, goal))
        {
            pop = Mathf.MoveTowards(pop, goal, Time.unscaledDeltaTime / popTime);
            ApplyPop();
        }

        // 선택된 곡 이름: 빨강·하늘 그림자가 좌우로 벌어지고, 가끔 크게 튄다
        if (!selected || !titleRed || !titleCyan) return;
        if (glitchLeft > 0f) glitchLeft -= Time.unscaledDeltaTime;
        else if (Time.unscaledTime > nextGlitch)
        {
            glitchLeft = Random.Range(0.06f, 0.16f);
            nextGlitch = Time.unscaledTime + Random.Range(1.2f, 3.2f);
        }
        float split = glitchLeft > 0f ? Random.Range(3f, 7f) : 2f;
        float jump = glitchLeft > 0f && Random.value < 0.5f ? Random.Range(-4f, 4f) : 0f;
        var basePos = titleText.rectTransform.anchoredPosition;
        titleRed.rectTransform.anchoredPosition = basePos + new Vector2(-split + jump, 0f);
        titleCyan.rectTransform.anchoredPosition = basePos + new Vector2(split + jump, 0f);
    }

    private void ApplyPop()
    {
        float e = 1f - (1f - pop) * (1f - pop) * (1f - pop);
        if (layout)
        {
            float h = Mathf.Lerp(normalHeight, selectedHeight, e);
            layout.preferredHeight = h;
            layout.minHeight = h;
        }
        if (body && bodyBaseSet) body.anchoredPosition = bodyBase + new Vector2(-selectedShift * e, 0f);
        if (glow)
        {
            var c = glow.color;
            c.a = e;
            glow.color = c;
        }
    }

    private void Refresh()
    {
        if (background)
        {
            if (selected && selectedSprite) background.sprite = selectedSprite;
            else if (normalSprite) background.sprite = normalSprite;
            background.color = selected ? selectedColor : hovered ? hoverColor : normalColor;
        }
        Color text = selected ? selectedTextColor : normalTextColor;
        if (titleText) titleText.color = text;
        if (numberText) numberText.color = new Color(text.r, text.g, text.b, 0.75f);
        if (selectedMark) selectedMark.SetActive(selected);
        if (titleRed) titleRed.gameObject.SetActive(selected);
        if (titleCyan) titleCyan.gameObject.SetActive(selected);
        if (waveRoot) waveRoot.gameObject.SetActive(selected);
        if (badge) badge.SetActive(selected);
    }

    private void BuildWave()
    {
        if (!waveRoot) return;
        if (waveImages == null || waveImages.Length != waveBars)
        {
            for (int i = waveRoot.childCount - 1; i >= 0; i--) Destroy(waveRoot.GetChild(i).gameObject);
            waveImages = new Image[waveBars];
            for (int i = 0; i < waveBars; i++)
            {
                var go = new GameObject("W", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(waveRoot, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                waveImages[i] = img;
            }
        }
        float w = waveRoot.rect.width;
        float h = waveRoot.rect.height;
        float step = w / waveBars;
        for (int i = 0; i < waveBars; i++)
        {
            float v = waveValues != null && waveValues.Length > 0 ? waveValues[i * waveValues.Length / waveBars] : 0.3f;
            var rt = waveImages[i].rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(1.5f, step * 0.5f), Mathf.Lerp(h * 0.12f, h, Mathf.Clamp01(v)));
            rt.anchoredPosition = new Vector2(i * step, 0f);
            waveImages[i].color = waveRest;
        }
    }

    // 정사각형이 아닌 커버는 가운데를 정사각형으로 잘라 보여준다
    public static Rect SquareCrop(Texture texture)
    {
        if (texture == null || texture.width == texture.height) return new Rect(0f, 0f, 1f, 1f);
        if (texture.width > texture.height)
        {
            float w = (float)texture.height / texture.width;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        float h = (float)texture.width / texture.height;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }
}
