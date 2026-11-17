using System.Collections.Generic;
using UnityEngine;

// 노트 겉모습만 시안 E4로 바꾼다 (원본 NoteManager는 고치지 않는다).
// NoteManager가 만든 노트 오브젝트("Tap Note Lane N" / "Hold Note Lane N")의 원래 납작한 판은 숨기고,
// 같은 위치에 원근을 따르는 3D 슬랩(윗면, 앞면, 옆면 + 윗면 앞 모서리 흰 하이라이트)을 그린다.
//  - 색은 왼손/오른손이 아니라 노트 종류로: 탭 #C9FFF0, 롱노트 #FF4D5E
// 노트 위치·판정은 원본 그대로이므로 판정 결과는 바뀌지 않는다.
[DisallowMultipleComponent]
public class NoteSkinV2 : MonoBehaviour
{
    [Header("슬랩 모양")]
    [SerializeField] private float slabHeight = 0.2f;
    [SerializeField] private float tapNearHalfDepth = 0.27f;
    [SerializeField] private float tapFarHalfDepth = 0.17f;
    [SerializeField, Range(0f, 0.2f)] private float laneInset = 0.05f;
    [SerializeField, Range(0f, 1f)] private float holdBodyAlpha = 0.5f;

    private NoteManager noteManager;
    private RhythmPlayFieldV2 playField;
    private Material material;

    private class Skin
    {
        public MeshRenderer original;
        public Mesh mesh;
        public GameObject go;
    }

    private readonly Dictionary<Transform, Skin> skins = new Dictionary<Transform, Skin>();
    private readonly List<Transform> stale = new List<Transform>();
    private readonly IngameV2MeshBatch batch = new IngameV2MeshBatch();

    public void Initialize(NoteManager newNoteManager, RhythmPlayFieldV2 newPlayField)
    {
        noteManager = newNoteManager;
        playField = newPlayField;
        material = playField.NewMaterial("V2 Note Slab", Texture2D.whiteTexture, Color.white, false);
        material.renderQueue = 3020;
    }

    private void LateUpdate()
    {
        if (noteManager == null || playField == null) return;

        Transform parent = noteManager.transform;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform note = parent.GetChild(i);
            bool isTap = note.name.StartsWith("Tap Note");
            bool isHold = !isTap && note.name.StartsWith("Hold Note");
            if (!isTap && !isHold) continue;

            Skin skin;
            if (!skins.TryGetValue(note, out skin))
            {
                skin = CreateSkin(note);
                skins[note] = skin;
            }
            if (skin.original != null && skin.original.enabled) skin.original.enabled = false;

            var filter = skin.original != null ? skin.original.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) continue;
            int lane = LaneOf(note.name);
            if (lane < 0) continue;

            Bounds b = filter.sharedMesh.bounds;
            batch.Clear();
            if (isTap) BuildTap(lane, b.center.z);
            else BuildHold(lane, b.min.z, b.max.z, b.max.y > 0.125f);
            batch.Apply(skin.mesh);
        }

        // NoteManager가 지운 노트의 겉모습 정리
        stale.Clear();
        foreach (var pair in skins)
        {
            if (pair.Key == null) stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++)
        {
            Skin s = skins[stale[i]];
            if (s.mesh != null) Destroy(s.mesh);
            skins.Remove(stale[i]);
        }
    }

    private Skin CreateSkin(Transform note)
    {
        var skin = new Skin();
        skin.original = note.GetComponent<MeshRenderer>();
        skin.go = new GameObject("Slab (V2)");
        skin.go.transform.SetParent(note, false);
        skin.mesh = new Mesh { name = "V2 Note Slab" };
        skin.mesh.MarkDynamic();
        skin.go.AddComponent<MeshFilter>().sharedMesh = skin.mesh;
        var r = skin.go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return skin;
    }

    private static int LaneOf(string name)
    {
        int idx = name.LastIndexOf(' ');
        int lane;
        if (idx >= 0 && int.TryParse(name.Substring(idx + 1), out lane)) return lane - 1;
        return -1;
    }

    private float Norm(float z)
    {
        return Mathf.Clamp01((z - playField.NearZ) / (playField.FarZ - playField.NearZ));
    }

    private void LaneEdges(int lane, float z, out float l, out float r)
    {
        playField.LaneBoundsAtZ(lane, z, out l, out r);
        float inset = (r - l) * laneInset;
        l += inset;
        r -= inset;
    }

    private void BuildTap(int lane, float centerZ)
    {
        float half = Mathf.Lerp(tapNearHalfDepth, tapFarHalfDepth, Norm(centerZ));
        float z0 = centerZ - half, z1 = Mathf.Min(centerZ + half, playField.FarGateZ);
        Slab(lane, z0, z1, slabHeight, IngameV2Art.TapNote, 1f, true);
    }

    private void BuildHold(int lane, float headZ, float tailZ, bool holding)
    {
        float capHalf = Mathf.Lerp(tapNearHalfDepth, tapFarHalfDepth, Norm(headZ));
        tailZ = Mathf.Min(tailZ, playField.FarGateZ);
        // 몸통: 반투명 빨강 판 (앞뒤 캡 사이)
        float l0, r0, l1, r1;
        float bodyY = slabHeight * 0.55f;
        LaneEdges(lane, headZ, out l0, out r0);
        LaneEdges(lane, tailZ, out l1, out r1);
        float shrink0 = (r0 - l0) * 0.1f, shrink1 = (r1 - l1) * 0.1f;
        Color body = IngameV2Art.WithAlpha(Color.Lerp(IngameV2Art.HoldNote, Color.white, holding ? 0.15f : 0f), holdBodyAlpha);
        batch.Quad(new Vector3(l0 + shrink0, bodyY, headZ), new Vector3(r0 - shrink0, bodyY, headZ),
            new Vector3(l1 + shrink1, bodyY, tailZ), new Vector3(r1 - shrink1, bodyY, tailZ),
            body, IngameV2Art.WithAlpha(body, holdBodyAlpha * 0.75f));
        // 끝 캡(얇게) → 머리 캡(두껍게) 순서로 그려서 머리가 위에 보이게 한다
        Slab(lane, tailZ - capHalf * 1.2f, tailZ, slabHeight * 0.8f, IngameV2Art.HoldNote, 0.95f, false);
        Slab(lane, headZ - capHalf, headZ + capHalf, slabHeight, IngameV2Art.HoldNote, 1f, true);
    }

    // z0(앞)~z1(뒤) 사이 슬랩. 앞면·옆면 먼저, 윗면, 하이라이트 순서로 그린다 (ZWrite가 없어서 순서가 곧 앞뒤)
    private void Slab(int lane, float z0, float z1, float height, Color top, float alpha, bool highlight)
    {
        if (z1 <= z0) return;
        float l0, r0, l1, r1;
        LaneEdges(lane, z0, out l0, out r0);
        LaneEdges(lane, z1, out l1, out r1);
        float y0 = 0.02f, y1 = y0 + height;

        Color front = IngameV2Art.WithAlpha(Color.Lerp(top, Color.black, 0.32f), alpha);
        Color side = IngameV2Art.WithAlpha(Color.Lerp(top, Color.black, 0.5f), alpha);
        Color topC = IngameV2Art.WithAlpha(top, alpha);
        Color topFar = IngameV2Art.WithAlpha(Color.Lerp(top, Color.black, 0.08f), alpha);

        // 옆면 (카메라가 가운데 위에 있으므로 바깥쪽 레인은 안쪽 옆면이 보인다)
        batch.Quad(new Vector3(l0, y0, z0), new Vector3(l0, y1, z0), new Vector3(l1, y0, z1), new Vector3(l1, y1, z1), side, side);
        batch.Quad(new Vector3(r0, y1, z0), new Vector3(r0, y0, z0), new Vector3(r1, y1, z1), new Vector3(r1, y0, z1), side, side);
        // 앞면
        batch.Quad(new Vector3(l0, y0, z0), new Vector3(r0, y0, z0), new Vector3(l0, y1, z0), new Vector3(r0, y1, z0), front, front);
        // 윗면
        batch.Quad(new Vector3(l0, y1, z0), new Vector3(r0, y1, z0), new Vector3(l1, y1, z1), new Vector3(r1, y1, z1), topC, topFar);
        // 윗면 앞 모서리 흰 하이라이트
        if (highlight)
        {
            float hz = z0 + (z1 - z0) * 0.16f;
            float lh, rh;
            LaneEdges(lane, hz, out lh, out rh);
            Color white = IngameV2Art.WithAlpha(Color.white, alpha);
            batch.Quad(new Vector3(l0, y1 + 0.002f, z0), new Vector3(r0, y1 + 0.002f, z0),
                new Vector3(lh, y1 + 0.002f, hz), new Vector3(rh, y1 + 0.002f, hz), white, IngameV2Art.WithAlpha(Color.white, alpha * 0.6f));
        }
    }

    private void OnDestroy()
    {
        foreach (var pair in skins)
        {
            if (pair.Value.mesh != null) Destroy(pair.Value.mesh);
        }
        skins.Clear();
    }
}
