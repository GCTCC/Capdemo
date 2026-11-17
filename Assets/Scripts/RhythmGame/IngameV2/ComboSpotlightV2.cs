using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연속 콤보 연출 (시안 E4 두 번째 그림). 콤보가 기준값(기본 100)에 닿으면 켜지고, 끊기면 0.5초에 걸쳐 꺼진다.
//  - 스포트라이트: 화면 위에서 떨어지는 빔 4개 (바깥 2개 민트, 안쪽 2개 흰색). 천천히 흔들리고 박자에 맞춰 밝기가 뛴다
//  - 스펙트럼 월: 하이웨이 양쪽 레일 바깥에 원근을 따라 세운 막대. 곡 소리 주파수로 높이가 바뀐다
//  - 콤보 태그: 콤보 숫자 아래 "★ SPOTLIGHT · 100+ COMBO"
//  - 켜지는 순간: 0.3초 페이드인 + 화면 전체 민트 플래시와 글리치 한 번
[DisallowMultipleComponent]
public class ComboSpotlightV2 : MonoBehaviour
{
    [SerializeField] private int comboThreshold = 100;
    [SerializeField] private float fadeInTime = 0.3f;
    [SerializeField] private float fadeOutTime = 0.5f;

    [Header("스포트라이트")]
    [SerializeField, Range(0f, 1f)] private float outerBeamAlpha = 0.42f;
    [SerializeField, Range(0f, 1f)] private float innerBeamAlpha = 0.2f;
    [SerializeField] private float swayDegrees = 4f;

    [Header("스펙트럼 월")]
    [SerializeField] private float barSpacing = 0.5f;
    [SerializeField] private float barMaxHeight = 1.3f;
    [SerializeField] private float barWidth = 0.13f;
    [SerializeField] private float railGap = 0.35f;

    private RhythmGameUIV2 ui;
    private RhythmPlayFieldV2 playField;
    private JudgementScoreManager scores;
    private RhythmClock clock;

    private RectTransform[] beams;
    private Image[] beamImages;
    private float[] beamBaseAngle;
    private GameObject tagObject;
    private CanvasGroup tagGroup;
    private Mesh wallMesh;
    private readonly IngameV2MeshBatch batch = new IngameV2MeshBatch();

    private bool active;
    private float intensity;
    private float glitchT;
    private Vector2 hudBase;

    public bool IsActive { get { return active; } }

    public void Initialize(RhythmGameUIV2 newUi, RhythmPlayFieldV2 newPlayField)
    {
        ui = newUi;
        playField = newPlayField;
        scores = ui.Controller.Scores;
        clock = ui.Controller.Clock;
        hudBase = ui.Hud.anchoredPosition;
        BuildBeams();
        BuildTag();
        BuildWall();
        scores.ComboChanged += OnComboChanged;
        Apply();
    }

    public void ResetForSong()
    {
        active = false;
        intensity = 0f;
        glitchT = 0f;
        Apply();
    }

    private void BuildBeams()
    {
        // (x, 기울기, 민트인지)
        float[] xs = { 360f, 1560f, 790f, 1130f };
        float[] angles = { 22f, -22f, 9f, -9f }; // 양수 = 아래쪽이 오른쪽(가운데)으로
        beams = new RectTransform[4];
        beamImages = new Image[4];
        beamBaseAngle = new float[4];
        for (int i = 0; i < 4; i++)
        {
            bool outer = i < 2;
            var img = IngameV2Art.Img(outer ? "Beam Mint" : "Beam White", ui.BackgroundLayer, IngameV2Art.Beam(),
                outer ? IngameV2Art.Mint : Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(xs[i], 60f);
            rt.sizeDelta = new Vector2(outer ? 620f : 420f, 1250f);
            beamBaseAngle[i] = angles[i];
            beams[i] = rt;
            beamImages[i] = img;
        }
    }

    private void BuildTag()
    {
        float k = IngameV2Art.K;
        var bg = IngameV2Art.Img("Spotlight Tag", ui.ComboTagAnchor, null, IngameV2Art.Mint);
        IngameV2Art.Stretch(bg.rectTransform);
        var glow = IngameV2Art.Img("Glow", bg.rectTransform, IngameV2Art.SoftBar(), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.5f));
        IngameV2Art.Stretch(glow.rectTransform);
        glow.rectTransform.offsetMin = new Vector2(-20, -14);
        glow.rectTransform.offsetMax = new Vector2(20, 14);
        glow.transform.SetAsFirstSibling();
        var text = IngameV2Art.Text("Text", bg.rectTransform, ui.FontLabelBold,
            "SPOTLIGHT · " + comboThreshold + "+ COMBO", 11 * k, IngameV2Art.Ink, TextAlignmentOptions.Center, 12f);
        IngameV2Art.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(14f, 0f);
        // ★ 는 글꼴에 없어서 그림으로 붙인다
        var star = IngameV2Art.Img("Star", bg.rectTransform, IngameV2Art.Star(), IngameV2Art.Ink);
        star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        star.rectTransform.sizeDelta = new Vector2(13f, 13f);
        star.rectTransform.anchoredPosition = new Vector2(-text.preferredWidth * 0.5f - 2f, 0f);
        tagObject = ui.ComboTagAnchor.gameObject;
        tagGroup = tagObject.AddComponent<CanvasGroup>();
    }

    private void BuildWall()
    {
        var go = new GameObject("Spectrum Wall (V2)");
        go.transform.SetParent(playField.transform, false);
        wallMesh = new Mesh { name = "Spectrum Wall" };
        wallMesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = wallMesh;
        var r = go.AddComponent<MeshRenderer>();
        var mat = playField.NewMaterial("V2 Spectrum Wall", Texture2D.whiteTexture, Color.white, false);
        mat.renderQueue = 3004;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void OnComboChanged(int combo)
    {
        if (!active && combo >= comboThreshold)
        {
            active = true;
            glitchT = 0.18f;
            ui.Flash();
        }
        else if (active && combo < comboThreshold)
        {
            active = false;
        }
    }

    private void Update()
    {
        if (ui == null) return;
        float dt = Time.unscaledDeltaTime;
        float target = active ? 1f : 0f;
        float speed = active ? 1f / Mathf.Max(0.01f, fadeInTime) : 1f / Mathf.Max(0.01f, fadeOutTime);
        intensity = Mathf.MoveTowards(intensity, target, dt * speed);

        // 켜지는 순간 글리치: HUD를 잠깐 지직 흔든다
        if (glitchT > 0f)
        {
            glitchT -= dt;
            ui.Hud.anchoredPosition = hudBase + (glitchT > 0f ? new Vector2(Random.Range(-6f, 6f), Random.Range(-2f, 2f)) : Vector2.zero);
        }

        Apply();
    }

    private void Apply()
    {
        bool visible = intensity > 0.001f;
        if (tagObject != null)
        {
            tagObject.SetActive(visible);
            tagGroup.alpha = intensity;
        }

        // 박자 맥동: 박마다 밝아졌다가 줄어든다
        float pulse = 0f;
        if (clock != null && clock.IsPlaying)
        {
            float beatFrac = (float)(clock.CurrentBeat % 1.0);
            pulse = Mathf.Exp(-beatFrac * 5f);
        }

        if (beams != null)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < beams.Length; i++)
            {
                beams[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float sway = Mathf.Sin(t * (0.35f + i * 0.07f) + i * 1.7f) * swayDegrees;
                beams[i].localRotation = Quaternion.Euler(0f, 0f, beamBaseAngle[i] + sway);
                bool outer = i < 2;
                float a = (outer ? outerBeamAlpha : innerBeamAlpha) * (0.82f + 0.18f * pulse) * intensity;
                beamImages[i].color = IngameV2Art.WithAlpha(outer ? IngameV2Art.Mint : Color.white, a);
            }
        }

        if (wallMesh != null) BuildWallMesh(visible);
    }

    private void BuildWallMesh(bool visible)
    {
        batch.Clear();
        if (visible)
        {
            float[] bands = ui.Spectrum.Bands;
            float start = playField.NearZ + 0.4f, end = playField.FarGateZ - 0.2f;
            int count = Mathf.Max(1, Mathf.FloorToInt((end - start) / barSpacing));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i <= count; i++)
                {
                    float z = start + i * barSpacing;
                    // 앞쪽은 저음, 뒤쪽은 고음 (좌우는 조금 어긋나게)
                    int bi = Mathf.Clamp(Mathf.RoundToInt(i / (float)count * (bands.Length - 1)) + (side > 0 ? 1 : 0), 0, bands.Length - 1);
                    float level = bands[bi];
                    float h = (0.12f + level * barMaxHeight) * intensity;
                    float x = side * (playField.HalfWidthAtZ(z) + railGap);
                    float w = barWidth * 0.5f;
                    Color body = IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.4f * intensity);
                    Color bodyTop = IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.6f * intensity);
                    batch.Quad(new Vector3(x - w, 0f, z), new Vector3(x + w, 0f, z), new Vector3(x - w, h, z), new Vector3(x + w, h, z),
                        IngameV2Art.WithAlpha(body, 0.15f * intensity), bodyTop);
                    Color cap = level > 0.72f ? IngameV2Art.Red : Color.white;
                    cap = IngameV2Art.WithAlpha(cap, 0.95f * intensity);
                    batch.Quad(new Vector3(x - w, h, z), new Vector3(x + w, h, z), new Vector3(x - w, h + 0.06f, z), new Vector3(x + w, h + 0.06f, z), cap, cap);
                }
            }
        }
        batch.Apply(wallMesh);
    }

    private void OnDestroy()
    {
        if (scores != null) scores.ComboChanged -= OnComboChanged;
        if (wallMesh != null) Destroy(wallMesh);
    }
}
