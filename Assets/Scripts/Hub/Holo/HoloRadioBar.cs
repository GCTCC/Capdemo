using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 아래 "RADIO 7" 홀로그램 바.
//   - 바닥의 발광 줄(Emitter)에서 빛(Beam)이 위로 쏘아 올라가고, 그 위에 반투명 패널이 떠 있다
//   - 이퀄라이저 막대가 출렁이고, 뉴스 문구가 오른쪽에서 왼쪽으로 흘러간다 (끊김 없이 반복)
//   - 가끔 패널이 지직거리며 옆으로 튀고 흐려진다 (홀로폰 문자와 같은 방식)
//   - 마우스를 올리면 문구가 천천히 흐른다 (읽기 쉽게)
public class HoloRadioBar : MonoBehaviour
{
    [Header("빛")]
    [SerializeField] private Graphic beam;
    [SerializeField] private Graphic emitter;
    [SerializeField] private Graphic emitterGlow;

    [Header("패널")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup panelGroup;
    [Tooltip("지직거릴 때 패널 위를 지나가는 밝은 가로 띠")]
    [SerializeField] private RectTransform glitchSlice;

    [Header("이퀄라이저")]
    [SerializeField] private RectTransform[] bars = new RectTransform[0];
    [SerializeField] private Vector2 barHeight = new Vector2(4f, 16f);

    [Header("흐르는 문구")]
    [Tooltip("문구 두 벌. 같은 글이 들어 있고, 하나가 끝나면 다른 하나가 이어 붙는다.")]
    [SerializeField] private TMP_Text tickerA;
    [SerializeField] private TMP_Text tickerB;
    [SerializeField] private float tickerSpeed = 70f;
    [SerializeField] private float tickerGap = 120f;
    [Tooltip("마우스를 올렸을 때 흐르는 속도 배율")]
    [SerializeField, Range(0f, 1f)] private float hoverSlowdown = 0.3f;

    [Header("지직")]
    [SerializeField] private Vector2 glitchInterval = new Vector2(2.5f, 6f);
    [SerializeField] private Vector2 glitchDuration = new Vector2(0.06f, 0.18f);
    [SerializeField] private float glitchShift = 6f;

    private HoloHover hover;
    private float tickerX;
    private float tickerWidth;
    private float nextGlitch;
    private float glitchLeft;
    private Vector2 panelBase;
    private float beamAlpha, emitterAlpha, glowAlpha;
    private float speedNow;

    private void Start()
    {
        hover = GetComponent<HoloHover>();
        if (panel) panelBase = panel.anchoredPosition;
        if (beam) beamAlpha = beam.color.a;
        if (emitter) emitterAlpha = emitter.color.a;
        if (emitterGlow) glowAlpha = emitterGlow.color.a;
        if (glitchSlice) glitchSlice.gameObject.SetActive(false);

        if (tickerA)
        {
            tickerA.ForceMeshUpdate();
            tickerWidth = tickerA.preferredWidth + tickerGap;
            if (tickerB)
            {
                tickerB.text = tickerA.text;
                tickerB.rectTransform.sizeDelta = tickerA.rectTransform.sizeDelta;
            }
        }
        speedNow = tickerSpeed;
        ScheduleGlitch();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        // 이퀄라이저
        for (int i = 0; i < bars.Length; i++)
        {
            if (!bars[i]) continue;
            float n = Mathf.PerlinNoise(time * 4.5f + i * 2.3f, i * 0.71f);
            bars[i].sizeDelta = new Vector2(bars[i].sizeDelta.x, Mathf.Lerp(barHeight.x, barHeight.y, n));
        }

        // 빛이 숨 쉬듯 일렁인다
        float breathe = 0.85f + 0.15f * Mathf.Sin(time * 2.1f);
        float flick = Random.value < 0.04f ? Random.Range(0.6f, 0.9f) : 1f;
        bool lit = hover && hover.Highlighted;
        SetAlpha(beam, beamAlpha * breathe * flick * (lit ? 1.35f : 1f));
        SetAlpha(emitter, emitterAlpha * flick);
        SetAlpha(emitterGlow, glowAlpha * (0.8f + 0.2f * Mathf.Sin(time * 3.7f)) * flick * (lit ? 1.3f : 1f));

        // 흐르는 문구
        if (tickerA && tickerWidth > 1f)
        {
            float goal = tickerSpeed * (lit ? hoverSlowdown : 1f);
            speedNow = Mathf.Lerp(speedNow, goal, 1f - Mathf.Exp(-6f * dt));
            tickerX -= speedNow * dt;
            if (tickerX <= -tickerWidth) tickerX += tickerWidth;
            Place(tickerA, tickerX);
            if (tickerB) Place(tickerB, tickerX + tickerWidth);
        }

        UpdateGlitch(dt);
    }

    private void UpdateGlitch(float dt)
    {
        if (!panel) return;
        if (glitchLeft > 0f)
        {
            glitchLeft -= dt;
            panel.anchoredPosition = panelBase + new Vector2(Random.value < 0.6f ? Random.Range(-glitchShift, glitchShift) : 0f, 0f);
            if (panelGroup) panelGroup.alpha = Random.Range(0.45f, 1f);
            if (glitchSlice)
            {
                glitchSlice.gameObject.SetActive(Random.value < 0.7f);
                var parent = (RectTransform)glitchSlice.parent;
                glitchSlice.anchoredPosition = new Vector2(Random.Range(-20f, 20f), Random.Range(-parent.rect.height * 0.4f, parent.rect.height * 0.4f));
            }
            if (glitchLeft <= 0f) EndGlitch();
        }
        else if (Time.unscaledTime >= nextGlitch)
        {
            glitchLeft = Random.Range(glitchDuration.x, Mathf.Max(glitchDuration.x, glitchDuration.y));
        }
    }

    private void EndGlitch()
    {
        panel.anchoredPosition = panelBase;
        if (panelGroup) panelGroup.alpha = 1f;
        if (glitchSlice) glitchSlice.gameObject.SetActive(false);
        ScheduleGlitch();
    }

    private void ScheduleGlitch()
    {
        glitchLeft = 0f;
        nextGlitch = Time.unscaledTime + Random.Range(glitchInterval.x, Mathf.Max(glitchInterval.x, glitchInterval.y));
    }

    private static void Place(TMP_Text text, float x)
    {
        var rt = text.rectTransform;
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }

    private static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color;
        c.a = Mathf.Clamp01(a);
        g.color = c;
    }
}
