using TMPro;
using UnityEngine;
using UnityEngine.UI;

// LIFE 게이지 (왼쪽 아래). 원본에 체력 시스템이 없어서 새로 만들었다.
//  - 시작 100%, 판정마다 아래 값만큼 깎이거나 회복 (최대 100%). 값은 인스펙터에서 바꾼다
//  - 30% 이하면 게이지가 빨갛게 깜빡인다
//  - failAtZero를 켜면 0%가 될 때 곡을 끝낸다 (기본 꺼짐: 지금은 표시만)
[DisallowMultipleComponent]
public class LifeGaugeV2 : MonoBehaviour
{
    [Header("판정별 변화 (%)")]
    [SerializeField] private float perfectGain = 0.5f;
    [SerializeField] private float greatGain = 0.3f;
    [SerializeField] private float goodLoss = 1f;
    [SerializeField] private float missLoss = 6f;

    [Header("옵션")]
    [SerializeField] private float startLife = 100f;
    [SerializeField] private float dangerBelow = 30f;
    [Tooltip("켜면 LIFE 0%에서 곡을 끝내고 결과 화면으로 간다")]
    [SerializeField] private bool failAtZero = false;

    private const int Cells = 20;

    private RhythmGameUIV2 ui;
    private float life;
    private bool failed;
    private TextMeshProUGUI percent;
    private Image[] cells;
    private Image[] cellGlows;
    private float[] lostTimer;
    private int lastFilled = Cells;

    public float Life { get { return life; } }

    public void Initialize(RhythmGameUIV2 newUi)
    {
        ui = newUi;
        Build();
        ui.Controller.JudgedSigned += OnJudged;
        ResetLife();
    }

    public void ResetLife()
    {
        life = Mathf.Clamp(startLife, 0f, 100f);
        failed = false;
        lastFilled = Mathf.CeilToInt(life / 100f * Cells - 0.001f);
        if (lostTimer != null) for (int i = 0; i < lostTimer.Length; i++) lostTimer[i] = 0f;
    }

    private void Build()
    {
        float k = IngameV2Art.K;
        var panel = ui.GlassPanel("Life Panel", ui.Hud, 56, 748, 330, 64);

        var label = IngameV2Art.Text("LIFE", panel, ui.FontLabelBold, "LIFE", 13 * k, IngameV2Art.MintLight, TextAlignmentOptions.MidlineLeft, 14f);
        IngameV2Art.Place(label.rectTransform, 18 * k, 13 * k, 100 * k, 18 * k);
        percent = IngameV2Art.Text("Percent", panel, ui.FontNumber, "100%", 22 * k, IngameV2Art.MintLight, TextAlignmentOptions.MidlineRight, 0f, true);
        IngameV2Art.Place(percent.rectTransform, 200 * k, 8 * k, 112 * k, 26 * k);
        IngameV2Art.Glow(percent, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.4f), 0.8f);

        cells = new Image[Cells];
        cellGlows = new Image[Cells];
        lostTimer = new float[Cells];
        float x0 = 18 * k, y = 40 * k, cw = 13.4f * k, ch = 9 * k, gap = 1.9f * k;
        Sprite cell = IngameV2Art.Parallelogram(Mathf.RoundToInt(cw), Mathf.RoundToInt(ch), 4);
        for (int i = 0; i < Cells; i++)
        {
            float x = x0 + i * (cw + gap);
            var glow = IngameV2Art.Img("Glow" + i, panel, IngameV2Art.SoftDot(), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.25f));
            IngameV2Art.PlaceCenter(glow.rectTransform, x + cw * 0.5f, y + ch * 0.5f, cw * 2.2f, ch * 3f);
            cellGlows[i] = glow;
            var c = IngameV2Art.Img("Cell" + i, panel, cell, IngameV2Art.Mint);
            IngameV2Art.Place(c.rectTransform, x, y, cw, ch);
            cells[i] = c;
        }
    }

    private void OnJudged(JudgementResult result, double signed)
    {
        switch (result.judgement)
        {
            case JudgementType.Perfect: life += perfectGain; break;
            case JudgementType.Great: life += greatGain; break;
            case JudgementType.Good: life -= goodLoss; break;
            default: life -= missLoss; break;
        }
        life = Mathf.Clamp(life, 0f, 100f);

        int filled = Mathf.CeilToInt(life / 100f * Cells - 0.001f);
        for (int i = filled; i < lastFilled && i < Cells; i++) lostTimer[i] = 0.6f; // 방금 깎인 칸은 잠깐 빨갛게
        lastFilled = filled;

        if (failAtZero && !failed && life <= 0f)
        {
            failed = true;
            ui.Controller.FailSong();
        }
    }

    private void Update()
    {
        if (cells == null) return;
        float dt = Time.unscaledDeltaTime;
        bool danger = life <= dangerBelow;
        float blink = danger ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        Color on = danger ? IngameV2Art.Red : IngameV2Art.Mint;
        int filled = Mathf.CeilToInt(life / 100f * Cells - 0.001f);
        for (int i = 0; i < Cells; i++)
        {
            if (lostTimer[i] > 0f) lostTimer[i] -= dt;
            Color c;
            float glow;
            if (i < filled) { c = IngameV2Art.WithAlpha(on, blink); glow = 0.28f * blink; }
            else if (lostTimer[i] > 0f) { c = IngameV2Art.WithAlpha(IngameV2Art.Red, 0.9f); glow = 0.2f; }
            else { c = IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.14f); glow = 0f; }
            cells[i].color = c;
            cellGlows[i].color = IngameV2Art.WithAlpha(i < filled ? on : IngameV2Art.Red, glow);
        }
        percent.text = Mathf.RoundToInt(life) + "%";
        percent.color = danger ? IngameV2Art.WithAlpha(IngameV2Art.Red, blink) : IngameV2Art.MintLight;
    }

    private void OnDestroy()
    {
        if (ui != null && ui.Controller != null) ui.Controller.JudgedSigned -= OnJudged;
    }
}
