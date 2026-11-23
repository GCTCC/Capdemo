using System;
using System.Collections.Generic;
using UnityEngine;

// 인게임 v2(시안 E4) 홀로그램 하이웨이.
// 원본 RhythmPlayField와 모양(레인 6개, 원근, 판정선 위치)은 같고, 그리는 방식만 새로 만들었다.
//  - 어두운 바탕 위에 민트 반투명 그라데이션 + 스캔라인 바닥 (프로젝터로 쏜 홀로그램 느낌)
//  - 레인 구분선(가운데만 진하게), 양쪽 레일 글로우, 마디선, 판정선(흰 선 + 민트 글로우)
//  - 앞쪽 이미터(받침 + 빛나는 줄 + LED 점), Far Gate(민트 바 + 기둥)
//  - 키 입력 효과(빛기둥, 링, 바닥 글로우, 롱노트 홀드 중 레인 전체 빛), 가끔 깜빡이는 글리치 슬라이스
// 원본과 달리 에디터에서 미리 만들지 않고, Play를 누르면 코드로 만든다 (씬이 가벼워진다).
[DisallowMultipleComponent]
public class RhythmPlayFieldV2 : MonoBehaviour
{
    private const string RootName = "Holo Highway (V2)";
    private const float FarGateOffset = 0.15f;

    [Header("모양 (원본 RhythmPlayField와 같은 값)")]
    [SerializeField] private float nearZ = 0.0f;
    [SerializeField] private float farZ = 20.0f;
    [SerializeField] private float nearHalfWidth = 5.8f;
    [SerializeField] private float farHalfWidth = 2.35f;
    [SerializeField] private float judgeZ = 1.15f;

    [Header("카메라 (원본과 같은 위치)")]
    [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 8f, -9.2f);
    [SerializeField] private Vector3 cameraLookAt = new Vector3(0f, 0f, 8.2f);
    [SerializeField] private float cameraFov = 55f;

    [Header("셰이더 (CapDemo/IngameV2/HoloWorld)")]
    [SerializeField] private Shader worldShader;

    [Header("글리치 슬라이스")]
    [SerializeField] private float glitchMinInterval = 0.5f;
    [SerializeField] private float glitchMaxInterval = 1.8f;
    [SerializeField, Range(0f, 1f)] private float glitchAlpha = 0.45f;

    private Transform root;
    private RhythmClock clock;
    private float noteSpeed = 15f;

    // 매 프레임 다시 만드는 메시들
    private Mesh dynNormal;   // 마디선, 글리치 슬라이스
    private Mesh dynAdd;      // 빛기둥, 레인 빛
    private Mesh dynDot;      // 판정선 위 바닥 글로우
    private Mesh dynRing;     // 흰 타원 링
    private readonly IngameV2MeshBatch batch = new IngameV2MeshBatch();

    private readonly float[] laneLight = new float[RhythmGameController.LaneCount];
    private readonly float[] laneHoldLight = new float[RhythmGameController.LaneCount];
    private readonly bool[] laneRed = new bool[RhythmGameController.LaneCount];

    private struct Slice
    {
        public float z, x0, x1, life, maxLife;
        public Color color;
    }
    private readonly List<Slice> slices = new List<Slice>();
    private float nextSliceTime;

    private readonly List<Material> createdMaterials = new List<Material>();

    public float NearZ { get { return nearZ; } }
    public float FarZ { get { return farZ; } }
    public float JudgeZ { get { return judgeZ; } }
    public float NearHalfWidth { get { return nearHalfWidth; } }
    public float FarGateZ { get { return farZ - FarGateOffset; } }
    public float NoteSpawnZ { get { return FarGateZ; } }
    public float NoteSpawnHalfWidth { get { return HalfWidthAtZ(FarGateZ); } }
    public Shader WorldShader { get { return worldShader; } }

    public Vector3 GateLeftWorld { get { return transform.TransformPoint(new Vector3(-HalfWidthAtZ(FarGateZ) - 0.1f, 0.55f, FarGateZ)); } }
    public Vector3 GateRightWorld { get { return transform.TransformPoint(new Vector3(HalfWidthAtZ(FarGateZ) + 0.1f, 0.55f, FarGateZ)); } }

    public float HalfWidthAtZ(float z)
    {
        return Mathf.Lerp(nearHalfWidth, farHalfWidth, Mathf.Clamp01((z - nearZ) / (farZ - nearZ)));
    }

    public void LaneBoundsAtZ(int lane, float z, out float left, out float right)
    {
        float hw = HalfWidthAtZ(z);
        float lw = hw * 2f / RhythmGameController.LaneCount;
        left = -hw + lw * lane;
        right = left + lw;
    }

    public void PrepareForPlay()
    {
        SetupCamera();
        Build();
    }

    // 마디선을 노트와 같은 속도로 흘리기 위해 시계와 노트 속도를 받는다
    public void SetTiming(RhythmClock newClock, float newNoteSpeed)
    {
        clock = newClock;
        noteSpeed = Mathf.Max(0.01f, newNoteSpeed);
    }

    // ── 재료 ──
    public Material NewMaterial(string name, Texture texture, Color tint, bool additive, float scan = 0f)
    {
        Shader shader = worldShader != null ? worldShader : Shader.Find("CapDemo/IngameV2/HoloWorld");
        var mat = new Material(shader);
        mat.name = name;
        mat.hideFlags = HideFlags.DontSave;
        mat.mainTexture = texture != null ? texture : Texture2D.whiteTexture;
        mat.color = tint;
        mat.SetFloat("_Scan", scan);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive
            ? (float)UnityEngine.Rendering.BlendMode.One
            : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        createdMaterials.Add(mat);
        return mat;
    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Rhythm Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.03f, 0.03f, 1f);
        cam.fieldOfView = cameraFov;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        cam.transform.position = cameraPosition;
        cam.transform.rotation = Quaternion.LookRotation(cameraLookAt - cameraPosition, Vector3.up);
    }

    private void Build()
    {
        var old = transform.Find(RootName);
        if (old != null) Destroy(old.gameObject);
        root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        Texture2D white = Texture2D.whiteTexture;
        Texture2D profile = IngameV2Art.GlowProfile();
        Color mint = IngameV2Art.Mint;
        float gateZ = FarGateZ;
        float endZ = gateZ - 0.1f; // 세로선은 게이트 앞에서 끝난다
        int lanes = RhythmGameController.LaneCount;

        // 1) 어두운 받침판 (하이웨이보다 살짝 넓다)
        batch.Clear();
        batch.Quad(P(-nearHalfWidth - 0.35f, -0.02f, nearZ), P(nearHalfWidth + 0.35f, -0.02f, nearZ),
            P(-farHalfWidth - 0.2f, -0.02f, farZ), P(farHalfWidth + 0.2f, -0.02f, farZ),
            new Color(0.004f, 0.016f, 0.018f, 0.9f), new Color(0.004f, 0.016f, 0.018f, 0.8f));
        AddStatic("Base Plate", NewMaterial("V2 Base", white, Color.white, false), 0);

        // 2) 홀로 바닥: 민트 반투명 그라데이션 + 스캔라인
        batch.Clear();
        batch.Quad(P(-nearHalfWidth, 0f, nearZ), P(nearHalfWidth, 0f, nearZ),
            P(-farHalfWidth, 0f, farZ), P(farHalfWidth, 0f, farZ),
            IngameV2Art.WithAlpha(mint, 0.08f), IngameV2Art.WithAlpha(mint, 0.025f));
        AddStatic("Holo Floor", NewMaterial("V2 Floor", white, Color.white, false, 0.35f), 1);

        // 3) 레인 구분선: 모두 민트 0.22, 같은 굵기
        //    (2026-11-23 재이: 가운데 선만 두꺼워 보인다는 피드백으로 가운데 강조를 없앰)
        batch.Clear();
        for (int i = 1; i < lanes; i++)
        {
            float r = i / (float)lanes;
            float nx = Mathf.Lerp(-nearHalfWidth, nearHalfWidth, r);
            float fx = Mathf.Lerp(-HalfWidthAtZ(endZ), HalfWidthAtZ(endZ), r);
            const float a = 0.22f;
            const float w = 0.02f;
            batch.Quad(P(nx - w, 0.01f, nearZ), P(nx + w, 0.01f, nearZ), P(fx - w * 0.4f, 0.01f, endZ), P(fx + w * 0.4f, 0.01f, endZ),
                IngameV2Art.WithAlpha(mint, a), IngameV2Art.WithAlpha(mint, a * 0.8f));
        }
        AddStatic("Lane Lines", NewMaterial("V2 Lines", white, Color.white, false), 2);

        // 4) 양쪽 레일: 민트 실선 + 블러 글로우
        batch.Clear();
        for (int s = -1; s <= 1; s += 2)
        {
            float nx = s * nearHalfWidth, fx = s * HalfWidthAtZ(endZ);
            batch.Quad(P(nx - 0.045f, 0.015f, nearZ), P(nx + 0.045f, 0.015f, nearZ), P(fx - 0.02f, 0.015f, endZ), P(fx + 0.02f, 0.015f, endZ),
                IngameV2Art.WithAlpha(IngameV2Art.MintLight, 0.95f), IngameV2Art.WithAlpha(mint, 0.85f));
        }
        AddStatic("Rails", NewMaterial("V2 Rails", white, Color.white, false), 4);
        batch.Clear();
        for (int s = -1; s <= 1; s += 2)
        {
            float nx = s * nearHalfWidth, fx = s * HalfWidthAtZ(endZ);
            batch.QuadUV(P(nx - 0.24f, 0.012f, nearZ), P(nx + 0.24f, 0.012f, nearZ), P(fx - 0.1f, 0.012f, endZ), P(fx + 0.1f, 0.012f, endZ),
                IngameV2Art.WithAlpha(mint, 0.45f), IngameV2Art.WithAlpha(mint, 0.3f), false);
        }
        AddStatic("Rail Glow", NewMaterial("V2 Rail Glow", profile, Color.white, true), 3);

        // 5) 판정선: 굵은 흰 선 + 민트 블러 글로우
        batch.Clear();
        float jhw0 = HalfWidthAtZ(judgeZ - 0.05f), jhw1 = HalfWidthAtZ(judgeZ + 0.05f);
        batch.Quad(P(-jhw0, 0.03f, judgeZ - 0.05f), P(jhw0, 0.03f, judgeZ - 0.05f), P(-jhw1, 0.03f, judgeZ + 0.05f), P(jhw1, 0.03f, judgeZ + 0.05f),
            Color.white, Color.white);
        AddStatic("Judge Line", NewMaterial("V2 Judge", white, Color.white, false), 6);
        batch.Clear();
        float g0 = HalfWidthAtZ(judgeZ - 0.3f), g1 = HalfWidthAtZ(judgeZ + 0.3f);
        batch.QuadUV(P(-g0, 0.025f, judgeZ - 0.3f), P(g0, 0.025f, judgeZ - 0.3f), P(-g1, 0.025f, judgeZ + 0.3f), P(g1, 0.025f, judgeZ + 0.3f),
            IngameV2Art.WithAlpha(mint, 0.6f), IngameV2Art.WithAlpha(mint, 0.6f), true);
        AddStatic("Judge Glow", NewMaterial("V2 Judge Glow", profile, Color.white, true), 5);

        // 6) Far Gate: 민트 바 + 양쪽 기둥
        batch.Clear();
        float ghw = HalfWidthAtZ(gateZ);
        batch.Quad(P(-ghw, 0.0f, gateZ), P(ghw, 0.0f, gateZ), P(-ghw, 0.16f, gateZ), P(ghw, 0.16f, gateZ),
            IngameV2Art.WithAlpha(mint, 0.75f), IngameV2Art.WithAlpha(IngameV2Art.MintLight, 0.95f));
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (ghw + 0.02f);
            batch.Quad(P(x - 0.05f, 0f, gateZ), P(x + 0.05f, 0f, gateZ), P(x - 0.05f, 0.55f, gateZ), P(x + 0.05f, 0.55f, gateZ),
                IngameV2Art.WithAlpha(IngameV2Art.MintLight, 0.95f), IngameV2Art.WithAlpha(mint, 0.85f));
        }
        AddStatic("Far Gate", NewMaterial("V2 Gate", white, Color.white, false), 6);
        batch.Clear();
        batch.QuadUV(P(-ghw - 0.3f, -0.3f, gateZ + 0.01f), P(ghw + 0.3f, -0.3f, gateZ + 0.01f), P(-ghw - 0.3f, 0.5f, gateZ + 0.01f), P(ghw + 0.3f, 0.5f, gateZ + 0.01f),
            IngameV2Art.WithAlpha(mint, 0.45f), IngameV2Art.WithAlpha(mint, 0.45f), true);
        AddStatic("Far Gate Glow", NewMaterial("V2 Gate Glow", profile, Color.white, true), 5);

        // 7) 이미터: 하이웨이 앞쪽 끝 아래 어두운 받침 + 빛나는 민트 줄 + LED 점
        batch.Clear();
        float ew = nearHalfWidth + 0.55f;
        batch.Quad(P(-ew, -0.55f, nearZ - 0.05f), P(ew, -0.55f, nearZ - 0.05f), P(-ew, -0.03f, nearZ - 0.05f), P(ew, -0.03f, nearZ - 0.05f),
            new Color(0.01f, 0.025f, 0.03f, 0.92f), new Color(0.02f, 0.05f, 0.055f, 0.92f));
        AddStatic("Emitter Base", NewMaterial("V2 Emitter", white, Color.white, false), 1);
        batch.Clear();
        batch.Quad(P(-ew, -0.06f, nearZ - 0.06f), P(ew, -0.06f, nearZ - 0.06f), P(-ew, -0.02f, nearZ - 0.06f), P(ew, -0.02f, nearZ - 0.06f),
            IngameV2Art.WithAlpha(IngameV2Art.MintLight, 0.9f), IngameV2Art.WithAlpha(IngameV2Art.MintLight, 0.9f));
        AddStatic("Emitter Line", NewMaterial("V2 Emitter Line", white, Color.white, false), 2);
        batch.Clear();
        batch.Quad(P(-ew, -0.10f, nearZ - 0.07f), P(ew, -0.10f, nearZ - 0.07f), P(-ew, -0.04f, nearZ - 0.07f), P(ew, -0.04f, nearZ - 0.07f),
            IngameV2Art.WithAlpha(IngameV2Art.Mint, 0f), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.35f));
        batch.Quad(P(-ew, -0.02f, nearZ - 0.07f), P(ew, -0.02f, nearZ - 0.07f), P(-ew, 0.06f, nearZ - 0.07f), P(ew, 0.06f, nearZ - 0.07f),
            IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.35f), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0f));
        int ledCount = 15;
        for (int i = 0; i < ledCount; i++)
        {
            float x = Mathf.Lerp(-ew + 0.4f, ew - 0.4f, i / (float)(ledCount - 1));
            Color led = (i % 5 == 2) ? IngameV2Art.Red : IngameV2Art.Mint;
            batch.Quad(P(x - 0.05f, -0.30f, nearZ - 0.07f), P(x + 0.05f, -0.30f, nearZ - 0.07f), P(x - 0.05f, -0.20f, nearZ - 0.07f), P(x + 0.05f, -0.20f, nearZ - 0.07f),
                IngameV2Art.WithAlpha(led, 0.9f), IngameV2Art.WithAlpha(led, 0.9f));
        }
        AddStatic("Emitter Glow + LEDs", NewMaterial("V2 Emitter Glow", white, Color.white, true), 3);
        // 이미터 글로우는 LED와 묶어 흰 텍스처 + 정점 알파(위아래로 흐려짐)로 그린다

        // 매 프레임 다시 만드는 메시들
        dynNormal = AddDynamic("Measure Lines + Glitch", NewMaterial("V2 Dyn", white, Color.white, false), 7);
        dynAdd = AddDynamic("Lane Beams", NewMaterial("V2 Beams", white, Color.white, true), 8);
        dynDot = AddDynamic("Lane Floor Glow", NewMaterial("V2 Floor Glow", IngameV2Art.SoftDotTexture(), Color.white, true), 8);
        dynRing = AddDynamic("Lane Rings", NewMaterial("V2 Rings", IngameV2Art.RingTexture(), Color.white, false), 9);

        nextSliceTime = Time.time + 1f;
    }

    private Vector3 P(float x, float y, float z) { return new Vector3(x, y, z); }

    private void AddStatic(string name, Material mat, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var mesh = new Mesh { name = name };
        batch.Apply(mesh);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        mat.renderQueue = 3000 + order;
    }

    private Mesh AddDynamic(string name, Material mat, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        mat.renderQueue = 3000 + order;
        return mesh;
    }

    // 컨트롤러가 매 프레임 부른다. isHeld: 키를 누르고 있는지, isHoldingLong: 롱노트를 누르고 있는지
    public void UpdateLaneEffects(Func<int, bool> isHeld, Func<int, bool> isHoldingLong)
    {
        if (root == null) return;
        float dt = Time.unscaledDeltaTime;
        for (int lane = 0; lane < laneLight.Length; lane++)
        {
            bool held = isHeld != null && isHeld(lane);
            bool holdingLong = isHoldingLong != null && isHoldingLong(lane);
            if (held) laneRed[lane] = holdingLong;
            laneLight[lane] = Mathf.MoveTowards(laneLight[lane], held ? 1f : 0f, dt * (held ? 14f : 6f));
            laneHoldLight[lane] = Mathf.MoveTowards(laneHoldLight[lane], holdingLong ? 1f : 0f, dt * 10f);
        }
        RebuildLaneEffects();
        RebuildMeasureAndGlitch(dt);
    }

    private void RebuildLaneEffects()
    {
        float z = judgeZ;
        // 빛기둥 + 레인 빛
        batch.Clear();
        for (int lane = 0; lane < laneLight.Length; lane++)
        {
            float k = laneLight[lane];
            float hold = laneHoldLight[lane];
            if (k <= 0.001f && hold <= 0.001f) continue;
            Color c = laneRed[lane] ? IngameV2Art.Red : IngameV2Art.Mint;
            float l0, r0, l1, r1;
            LaneBoundsAtZ(lane, z, out l0, out r0);
            float pad = (r0 - l0) * 0.06f;
            if (k > 0.001f)
            {
                // 판정선에서 위로 올라가는 빛기둥
                batch.Quad(P(l0 + pad, 0.02f, z), P(r0 - pad, 0.02f, z), P(l0 + pad, 1.7f, z + 0.5f), P(r0 - pad, 1.7f, z + 0.5f),
                    IngameV2Art.WithAlpha(c, 0.42f * k), IngameV2Art.WithAlpha(c, 0f));
                // 판정선에서 뒤로 레인을 따라 흐려지는 빛
                float farEnd = Mathf.Min(z + 9f, FarGateZ);
                LaneBoundsAtZ(lane, farEnd, out l1, out r1);
                batch.Quad(P(l0 + pad, 0.02f, z), P(r0 - pad, 0.02f, z), P(l1, 0.02f, farEnd), P(r1, 0.02f, farEnd),
                    IngameV2Art.WithAlpha(c, 0.32f * k), IngameV2Art.WithAlpha(c, 0f));
            }
            if (hold > 0.001f)
            {
                // 롱노트를 누르는 동안 레인 전체에 같은 색 빛
                float endZ = FarGateZ - 0.1f;
                float ln, rn;
                LaneBoundsAtZ(lane, nearZ, out ln, out rn);
                LaneBoundsAtZ(lane, endZ, out l1, out r1);
                batch.Quad(P(ln, 0.015f, nearZ), P(rn, 0.015f, nearZ), P(l1, 0.015f, endZ), P(r1, 0.015f, endZ),
                    IngameV2Art.WithAlpha(c, 0.22f * hold), IngameV2Art.WithAlpha(c, 0.10f * hold));
            }
        }
        batch.Apply(dynAdd);

        // 바닥 글로우
        batch.Clear();
        for (int lane = 0; lane < laneLight.Length; lane++)
        {
            float k = laneLight[lane];
            if (k <= 0.001f) continue;
            Color c = laneRed[lane] ? IngameV2Art.Red : IngameV2Art.Mint;
            float l, r;
            LaneBoundsAtZ(lane, z, out l, out r);
            float cx = (l + r) * 0.5f, hw = (r - l) * 0.85f;
            batch.QuadUV2(P(cx - hw, 0.02f, z - 0.75f), P(cx + hw, 0.02f, z - 0.75f), P(cx - hw, 0.02f, z + 0.75f), P(cx + hw, 0.02f, z + 0.75f),
                IngameV2Art.WithAlpha(c, 0.75f * k));
        }
        batch.Apply(dynDot);

        // 흰 타원 링
        batch.Clear();
        for (int lane = 0; lane < laneLight.Length; lane++)
        {
            float k = laneLight[lane];
            if (k <= 0.001f) continue;
            float l, r;
            LaneBoundsAtZ(lane, z, out l, out r);
            float cx = (l + r) * 0.5f, hw = (r - l) * 0.42f;
            Color ring = laneRed[lane] ? Color.Lerp(Color.white, IngameV2Art.Red, 0.35f) : Color.white;
            batch.QuadUV2(P(cx - hw, 0.04f, z - 0.42f), P(cx + hw, 0.04f, z - 0.42f), P(cx - hw, 0.04f, z + 0.42f), P(cx + hw, 0.04f, z + 0.42f),
                IngameV2Art.WithAlpha(ring, 0.9f * k));
        }
        batch.Apply(dynRing);
    }

    private void RebuildMeasureAndGlitch(float dt)
    {
        batch.Clear();
        float endZ = FarGateZ - 0.1f;

        // 마디선 (4박마다, 민트 0.16): 노트와 같은 속도로 흘러온다
        if (clock != null && clock.IsPlaying && clock.HasStarted)
        {
            float bpm = Mathf.Max(1f, clock.CurrentBpm);
            float secondsPerBeat = 60f / bpm;
            double beat = clock.CurrentBeat;
            int firstMeasure = Mathf.FloorToInt((float)(beat / 4.0));
            for (int m = firstMeasure; m < firstMeasure + 64; m++)
            {
                float z = judgeZ + (float)((m * 4 - beat) * secondsPerBeat) * noteSpeed;
                if (z < judgeZ - 0.2f) continue;
                if (z > endZ) break;
                float hw = HalfWidthAtZ(z);
                batch.Quad(P(-hw, 0.008f, z - 0.025f), P(hw, 0.008f, z - 0.025f), P(-hw, 0.008f, z + 0.025f), P(hw, 0.008f, z + 0.025f),
                    IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.16f), IngameV2Art.WithAlpha(IngameV2Art.Mint, 0.16f));
            }
        }

        // 글리치 슬라이스: 가끔 짧게 깜빡인다
        if (Time.time >= nextSliceTime)
        {
            nextSliceTime = Time.time + UnityEngine.Random.Range(glitchMinInterval, glitchMaxInterval);
            int count = UnityEngine.Random.Range(1, 3);
            for (int i = 0; i < count; i++)
            {
                float z = UnityEngine.Random.Range(judgeZ + 3f, FarGateZ - 4f);
                float hw = HalfWidthAtZ(z);
                float a = UnityEngine.Random.Range(-hw, hw * 0.4f);
                float b = a + UnityEngine.Random.Range(hw * 0.3f, hw * 0.9f);
                Color[] colors = { IngameV2Art.Mint, IngameV2Art.Red, IngameV2Art.Cyan };
                var s = new Slice
                {
                    z = z, x0 = a, x1 = Mathf.Min(b, hw),
                    maxLife = UnityEngine.Random.Range(0.06f, 0.16f),
                    color = colors[UnityEngine.Random.Range(0, colors.Length)],
                };
                s.life = s.maxLife;
                slices.Add(s);
            }
        }
        for (int i = slices.Count - 1; i >= 0; i--)
        {
            var s = slices[i];
            s.life -= dt;
            if (s.life <= 0f) { slices.RemoveAt(i); continue; }
            slices[i] = s;
            Color c = IngameV2Art.WithAlpha(s.color, glitchAlpha);
            batch.Quad(P(s.x0, 0.02f, s.z - 0.03f), P(s.x1, 0.02f, s.z - 0.03f), P(s.x0, 0.02f, s.z + 0.03f), P(s.x1, 0.02f, s.z + 0.03f), c, c);
        }
        batch.Apply(dynNormal);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < createdMaterials.Count; i++)
        {
            if (createdMaterials[i] != null) Destroy(createdMaterials[i]);
        }
        createdMaterials.Clear();
    }
}

// 정점 색이 들어간 사각형들을 하나의 메시로 묶는 도우미
public class IngameV2MeshBatch
{
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> tris = new List<int>();

    public void Clear()
    {
        verts.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
    }

    // nearLeft, nearRight, farLeft, farRight 순서. near 색 → far 색
    public void Quad(Vector3 nl, Vector3 nr, Vector3 fl, Vector3 fr, Color nearColor, Color farColor)
    {
        Add(nl, nr, fl, fr, nearColor, nearColor, farColor, farColor,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1));
    }

    // 글로우 단면 텍스처: alongZ가 false면 가로(U)로, true면 앞뒤(V→U)로 흐려진다
    public void QuadUV(Vector3 nl, Vector3 nr, Vector3 fl, Vector3 fr, Color nearColor, Color farColor, bool alongZ)
    {
        if (!alongZ)
            Add(nl, nr, fl, fr, nearColor, nearColor, farColor, farColor,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1));
        else
            Add(nl, nr, fl, fr, nearColor, nearColor, farColor, farColor,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1));
    }

    // 2D 텍스처(원, 링)를 그대로 붙인다
    public void QuadUV2(Vector3 nl, Vector3 nr, Vector3 fl, Vector3 fr, Color color)
    {
        Add(nl, nr, fl, fr, color, color, color, color,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1));
    }

    public void Add(Vector3 nl, Vector3 nr, Vector3 fl, Vector3 fr, Color cnl, Color cnr, Color cfl, Color cfr,
        Vector2 unl, Vector2 unr, Vector2 ufl, Vector2 ufr)
    {
        int i = verts.Count;
        verts.Add(nl); verts.Add(nr); verts.Add(fl); verts.Add(fr);
        colors.Add(cnl); colors.Add(cnr); colors.Add(cfl); colors.Add(cfr);
        uvs.Add(unl); uvs.Add(unr); uvs.Add(ufl); uvs.Add(ufr);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
        tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
    }

    public void Apply(Mesh mesh)
    {
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        // 매 프레임 바뀌는 메시는 화면 밖으로 잘리지 않게 넉넉한 범위를 준다
        mesh.bounds = new Bounds(new Vector3(0f, 1f, 10f), new Vector3(40f, 10f, 40f));
    }
}
