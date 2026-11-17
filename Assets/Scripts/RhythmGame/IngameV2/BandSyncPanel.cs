using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BAND SYNC 패널 (오른쪽 아래) — 목업.
// 실제 게임 로직과는 연결하지 않았다. 레벨 미터는 곡 소리의 주파수로만 움직인다.
//  - DRUMS = 저음, BASS = 저중음, VOX·GT(플레이어) = 중고음
//  - 헤더 %는 HUD 정확도 값을 그대로 보여 준다
// 나중에 밴드 멤버 시스템이 생기면 이 컴포넌트만 바꾸면 된다 (MemberLevel을 실제 값으로).
[DisallowMultipleComponent]
public class BandSyncPanel : MonoBehaviour
{
    private const int Segments = 16;
    private const int RedTop = 2; // 위쪽 2칸은 빨강

    [System.Serializable]
    public class Member
    {
        public string label = "BASS";
        public string tag = "";          // 오른쪽 끝 글씨 (플레이어는 YOU)
        public bool isPlayer;
    }

    [SerializeField] private Member[] members =
    {
        new Member { label = "VOX · GT", tag = "YOU", isPlayer = true },
        new Member { label = "BASS", tag = "" },
        new Member { label = "DRUMS", tag = "" },
    };

    private RhythmGameUIV2 ui;
    private TextMeshProUGUI percent;
    private Image[][] meters;
    private float[] levels;

    public void Initialize(RhythmGameUIV2 newUi)
    {
        ui = newUi;
        Build();
    }

    private void Build()
    {
        float k = IngameV2Art.K;
        var panel = ui.GlassPanel("Band Sync Panel", ui.Hud, 1244, 728, 300, 100);

        var header = IngameV2Art.Text("BAND SYNC", panel, ui.FontLabelBold, "BAND SYNC", 10 * k, IngameV2Art.TextDim, TextAlignmentOptions.MidlineLeft, 14f);
        IngameV2Art.Place(header.rectTransform, 16 * k, 10 * k, 160 * k, 14 * k);
        percent = IngameV2Art.Text("Percent", panel, ui.FontLabelBold, "--%", 11 * k, IngameV2Art.MintLight, TextAlignmentOptions.MidlineRight, 4f);
        IngameV2Art.Place(percent.rectTransform, 220 * k, 10 * k, 64 * k, 14 * k);

        meters = new Image[members.Length][];
        levels = new float[members.Length];
        Sprite seg = null;
        for (int m = 0; m < members.Length; m++)
        {
            float y = (32 + m * 21) * k;
            var name = IngameV2Art.Text("Name", panel, ui.FontLabelBold, members[m].label, 10.5f * k,
                members[m].isPlayer ? IngameV2Art.MintLight : IngameV2Art.TextDim, TextAlignmentOptions.MidlineLeft, 10f);
            IngameV2Art.Place(name.rectTransform, 16 * k, y, 80 * k, 14 * k);

            meters[m] = new Image[Segments];
            float x0 = 92 * k, cw = 8.6f * k, gap = 1.6f * k, h = 9 * k;
            for (int i = 0; i < Segments; i++)
            {
                var c = IngameV2Art.Img("Seg" + i, panel, seg, IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.14f));
                IngameV2Art.Place(c.rectTransform, x0 + i * (cw + gap), y + 2.5f * k, cw, h);
                meters[m][i] = c;
            }

            string tagText = string.IsNullOrEmpty(members[m].tag) ? "♪" : members[m].tag;
            var tag = IngameV2Art.Text("Tag", panel, members[m].isPlayer ? ui.FontLabelBold : ui.FontKorean, tagText, 10 * k,
                members[m].isPlayer ? IngameV2Art.Mint : IngameV2Art.TextDim, TextAlignmentOptions.MidlineRight, 2f);
            IngameV2Art.Place(tag.rectTransform, 250 * k, y, 34 * k, 14 * k);
        }
    }

    // 멤버별 레벨 0~1. 지금은 소리 주파수 목업 (나중에 실제 밴드 싱크 값으로 바꾸기)
    protected virtual float MemberLevel(int index, IngameSpectrumV2 spectrum)
    {
        switch (index)
        {
            case 0: return spectrum.MidHigh;
            case 1: return spectrum.LowMid;
            default: return spectrum.Low;
        }
    }

    private void Update()
    {
        if (ui == null || meters == null) return;
        percent.text = ui.HasAccuracy ? Mathf.RoundToInt(ui.Accuracy * 100f) + "%" : "--%";

        for (int m = 0; m < meters.Length; m++)
        {
            float target = Mathf.Clamp01(MemberLevel(m, ui.Spectrum));
            levels[m] = Mathf.Lerp(levels[m], target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            int lit = Mathf.RoundToInt(levels[m] * Segments);
            for (int i = 0; i < Segments; i++)
            {
                bool red = i >= Segments - RedTop;
                bool on = i < lit;
                Color c = red ? IngameV2Art.Red : IngameV2Art.Mint;
                meters[m][i].color = IngameV2Art.WithAlpha(c, on ? 0.95f : (red ? 0.18f : 0.14f));
            }
        }
    }
}
