using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 고정 플레이필드, 카메라와 레인 입력 발광을 담당한다.
// 편집 모드에서도 실행되어 Play 전에 Generated Stage를 Hierarchy에 만든다.
[ExecuteAlways]
[DisallowMultipleComponent]
public class RhythmPlayField : MonoBehaviour
{
    private const string GeneratedRootName = "Generated Stage";
    private const string StageVersionMarkerName = "Stage Version 10 - Gate Edge Aligned Rails";
    private const float FarGateOffset = 0.15f;
    private const float FarGateHalfDepth = 0.10f;

    [Header("Play Field Shape")]
    [SerializeField] private float nearZ = 0.0f;
    [SerializeField] private float farZ = 20.0f;
    [SerializeField] private float nearHalfWidth = 5.8f;
    [SerializeField] private float farHalfWidth = 2.35f;
    [SerializeField] private float judgeZ = 1.15f;

    [Header("Materials - Play Field")]
    [SerializeField] private Material boardMaterial;
    [SerializeField] private Material sideFloorMaterial;
    [SerializeField] private Material railMaterial;
    [SerializeField] private Material railGlowMaterial;
    [SerializeField] private Material gridMaterial;
    [SerializeField] private Material judgeMaterial;

    [Header("Materials - Lane Input")]
    [SerializeField] private Material leftLaneGlowMaterial;
    [SerializeField] private Material rightLaneGlowMaterial;

    [Header("Materials - Cyber City")]
    [SerializeField] private Material cityMaterialA;
    [SerializeField] private Material cityMaterialB;
    [SerializeField] private Material cityCyanMaterial;
    [SerializeField] private Material cityMagentaMaterial;

    private Transform generatedRoot;
    private MeshRenderer[] laneGlowRenderers;
    private int innerRailPixelHeight;

    public float NearZ { get { return nearZ; } }
    public float FarZ { get { return farZ; } }
    public float NoteSpawnZ { get { return FarGateZ; } }
    public float NearHalfWidth { get { return nearHalfWidth; } }
    public float FarHalfWidth { get { return farHalfWidth; } }
    public float NoteSpawnHalfWidth { get { return HalfWidthAtZ(FarGateZ); } }
    public float JudgeZ { get { return judgeZ; } }
    private float FarGateZ { get { return farZ - FarGateOffset; } }
    // 세로선은 Far Gate 면과 겹치지 않고, 플레이어 쪽 가장자리에 맞닿아 끝난다.
    private float LaneEndZ { get { return FarGateZ - FarGateHalfDepth; } }
    private float LaneEndHalfWidth { get { return HalfWidthAtZ(LaneEndZ); } }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // 컴파일·역직렬화 도중 Hierarchy를 수정하지 않도록 다음 에디터 틱으로 미룬다.
            EditorApplication.delayCall -= EnsureStageInEditor;
            EditorApplication.delayCall += EnsureStageInEditor;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= EnsureStageInEditor;
#endif
    }

#if UNITY_EDITOR
    private void EnsureStageInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        if (EnsureStage())
        {
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif

    // 게임 시작 시에는 이미 만들어진 필드를 재사용하고 참조만 복구한다.
    public void PrepareForPlay()
    {
        SetupCamera();
        EnsureStage();
        CacheLaneGlowRenderers();
    }

    // Inspector의 Generate / Refresh 버튼에서 호출한다.
    [ContextMenu("Generate or Refresh Stage")]
    public void RebuildStage()
    {
        ClearGeneratedStage();

        GameObject rootObject = new GameObject(GeneratedRootName);
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
        laneGlowRenderers = new MeshRenderer[RhythmGameController.LaneCount];

        BuildStage();

#if UNITY_EDITOR
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    [ContextMenu("Clear Generated Stage")]
    public void ClearGeneratedStage()
    {
        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot == null)
        {
            generatedRoot = null;
            laneGlowRenderers = null;
            return;
        }

        MeshFilter[] filters = existingRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null)
            {
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && !AssetDatabase.Contains(mesh))
            {
                DestroyImmediate(mesh);
            }
            else
#endif
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
        }

        DestroySceneObject(existingRoot.gameObject);
        generatedRoot = null;
        laneGlowRenderers = null;
    }

    // 컨트롤러가 매 프레임 전달한 키 입력 상태로 레인 발광을 켜고 끈다.
    public void UpdateLaneGlows(Func<int, bool> isLaneHeld)
    {
        if (laneGlowRenderers == null || laneGlowRenderers.Length == 0)
        {
            CacheLaneGlowRenderers();
        }

        for (int lane = 0; lane < RhythmGameController.LaneCount; lane++)
        {
            MeshRenderer renderer = laneGlowRenderers[lane];
            if (renderer != null)
            {
                renderer.enabled = isLaneHeld != null && isLaneHeld(lane);
            }
        }
    }

    // 정상적인 Generated Stage가 있으면 유지하고, 없거나 Mesh가 유실됐을 때만 다시 만든다.
    private bool EnsureStage()
    {
        generatedRoot = transform.Find(GeneratedRootName);
        if (HasValidGeneratedStage())
        {
            CacheLaneGlowRenderers();
            return false;
        }

        RebuildStage();
        return true;
    }

    private bool HasValidGeneratedStage()
    {
        if (generatedRoot == null)
        {
            return false;
        }

        // 이전 굵은 레일로 생성된 Stage는 한 번만 자동 재생성한다.
        if (generatedRoot.Find(StageVersionMarkerName) == null)
        {
            return false;
        }

        MeshFilter[] filters = generatedRoot.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
            {
                return false;
            }
        }

        return true;
    }

    private void SetupCamera()
    {
        Camera gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new GameObject("Rhythm Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.008f, 0.012f, 0.035f, 1.0f);
        gameCamera.fieldOfView = 55.0f;
        gameCamera.nearClipPlane = 0.01f;
        gameCamera.farClipPlane = 100.0f;
        gameCamera.transform.position = new Vector3(0.0f, 8.0f, -9.2f);
        gameCamera.transform.rotation = Quaternion.LookRotation(
            new Vector3(0.0f, 0.0f, 8.2f) - gameCamera.transform.position,
            Vector3.up);
    }

    private void BuildStage()
    {
        GameObject versionMarker = new GameObject(StageVersionMarkerName);
        versionMarker.transform.SetParent(generatedRoot, false);

        CreateQuadMesh(
            "Left Cyber Deck",
            new Vector3(-13.0f, -0.03f, nearZ),
            new Vector3(-nearHalfWidth, -0.03f, nearZ),
            new Vector3(-13.0f, -0.03f, farZ + 5.0f),
            new Vector3(-farHalfWidth, -0.03f, farZ + 5.0f),
            sideFloorMaterial);

        CreateQuadMesh(
            "Right Cyber Deck",
            new Vector3(nearHalfWidth, -0.03f, nearZ),
            new Vector3(13.0f, -0.03f, nearZ),
            new Vector3(farHalfWidth, -0.03f, farZ + 5.0f),
            new Vector3(13.0f, -0.03f, farZ + 5.0f),
            sideFloorMaterial);

        CreateQuadMesh(
            "Play Field",
            new Vector3(-nearHalfWidth, 0.0f, nearZ),
            new Vector3(nearHalfWidth, 0.0f, nearZ),
            new Vector3(-farHalfWidth, 0.0f, farZ),
            new Vector3(farHalfWidth, 0.0f, farZ),
            boardMaterial);

        for (int i = 0; i <= RhythmGameController.LaneCount; i++)
        {
            float ratio = (float)i / RhythmGameController.LaneCount;
            float nearX = Mathf.Lerp(-nearHalfWidth, nearHalfWidth, ratio);
            float farX = Mathf.Lerp(-LaneEndHalfWidth, LaneEndHalfWidth, ratio);
            bool isOuterRail = i == 0 || i == RhythmGameController.LaneCount;

            if (isOuterRail)
            {
                CreateRail("Lane Glow Rail " + i, nearX, farX, 0.085f, railGlowMaterial);
                CreateRail("Lane Rail " + i, nearX, farX, 0.025f, railMaterial);
            }
            else
            {
                // 가운데 선을 포함한 내부선은 모두 같은 규칙으로 생성한다.
                CreateRail("Lane Rail " + i, nearX, farX, 0.008f, gridMaterial);
            }
        }

        for (int lane = 0; lane < RhythmGameController.LaneCount; lane++)
        {
            CreateLaneGlow(lane);
        }

        // 일반 그리드는 제거하고 판정선과 먼 쪽 경계선만 유지한다.
        // 외곽 레일의 글로우 영역과 살짝 맞물려 끝점의 시각적 빈틈을 없앤다.
        CreateHorizontalBar("Judgement Line", judgeZ, 0.0f, 0.055f, 0.055f, judgeMaterial);
        CreateHorizontalBar(
            "Far Gate",
            FarGateZ,
            -0.01f,
            0.055f,
            FarGateHalfDepth,
            railMaterial);
        BuildCyberCity();
        BuildHorizonPortal();
        innerRailPixelHeight = 0;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || generatedRoot == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null || camera.pixelHeight <= 0
            || camera.pixelHeight == innerRailPixelHeight)
        {
            return;
        }

        // 화면상 좌우 0.6픽셀씩 계산한다. 원근에 임의의 폭 배수를 적용하지 않는다.
        for (int i = 1; i < RhythmGameController.LaneCount; i++)
        {
            Transform rail = generatedRoot.Find("Lane Rail " + i);
            if (rail == null)
            {
                continue;
            }

            MeshFilter filter = rail.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            float ratio = i / (float)RhythmGameController.LaneCount;
            Vector3 nearPoint = camera.WorldToScreenPoint(generatedRoot.TransformPoint(
                new Vector3(Mathf.Lerp(-nearHalfWidth, nearHalfWidth, ratio), 0.035f, nearZ)));
            Vector3 farPoint = camera.WorldToScreenPoint(generatedRoot.TransformPoint(
                new Vector3(
                    Mathf.Lerp(-LaneEndHalfWidth, LaneEndHalfWidth, ratio),
                    0.035f,
                    LaneEndZ)));
            const float halfPixelWidth = 0.6f;
            Vector3 offset = new Vector3(halfPixelWidth, 0.0f, 0.0f);
            filter.sharedMesh.vertices = new Vector3[]
            {
                rail.InverseTransformPoint(camera.ScreenToWorldPoint(nearPoint - offset)),
                rail.InverseTransformPoint(camera.ScreenToWorldPoint(nearPoint + offset)),
                rail.InverseTransformPoint(camera.ScreenToWorldPoint(farPoint - offset)),
                rail.InverseTransformPoint(camera.ScreenToWorldPoint(farPoint + offset))
            };
            filter.sharedMesh.RecalculateBounds();
        }

        innerRailPixelHeight = camera.pixelHeight;
    }

    private void CreateLaneGlow(int lane)
    {
        float nearLaneWidth = nearHalfWidth * 2.0f / RhythmGameController.LaneCount;
        float farLaneWidth = LaneEndHalfWidth * 2.0f / RhythmGameController.LaneCount;
        float nearLeft = -nearHalfWidth + nearLaneWidth * lane + 0.05f;
        float nearRight = nearLeft + nearLaneWidth - 0.10f;
        float farLeft = -LaneEndHalfWidth + farLaneWidth * lane + 0.025f;
        float farRight = farLeft + farLaneWidth - 0.05f;

        Material glowMaterial = lane < 3
            ? leftLaneGlowMaterial
            : rightLaneGlowMaterial;

        GameObject glow = CreateQuadMesh(
            "Held Lane Glow " + (lane + 1),
            new Vector3(nearLeft, 0.018f, nearZ),
            new Vector3(nearRight, 0.018f, nearZ),
            new Vector3(farLeft, 0.018f, LaneEndZ),
            new Vector3(farRight, 0.018f, LaneEndZ),
            glowMaterial);

        laneGlowRenderers[lane] = glow.GetComponent<MeshRenderer>();
        laneGlowRenderers[lane].enabled = false;
    }

    private void BuildCyberCity()
    {
        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            float side = sideIndex == 0 ? -1.0f : 1.0f;
            for (int i = 0; i < 9; i++)
            {
                float z = 1.8f + i * 2.65f;
                float height = 1.4f + ((i * 7) % 5) * 0.72f;
                float width = 0.72f + (i % 3) * 0.23f;
                float x = side * (HalfWidthAtZ(Mathf.Min(z, farZ))
                    + 1.55f + (i % 2) * 0.48f);
                Material buildingMaterial = i % 2 == 0 ? cityMaterialA : cityMaterialB;

                CreateBox(
                    "Cyber Tower",
                    new Vector3(x, height * 0.5f, z),
                    new Vector3(width, height, 0.85f),
                    buildingMaterial);

                Material accent = (i + sideIndex) % 2 == 0
                    ? cityCyanMaterial
                    : cityMagentaMaterial;
                float innerFaceX = x - side * (width * 0.52f);
                CreateBox(
                    "Tower Neon",
                    new Vector3(innerFaceX, height * 0.58f, z - 0.44f),
                    new Vector3(0.055f, height * 0.55f, 0.035f),
                    accent);
            }
        }
    }

    private void BuildHorizonPortal()
    {
        float z = farZ + 0.35f;
        CreateBox(
            "Horizon Left",
            new Vector3(-farHalfWidth - 0.35f, 1.7f, z),
            new Vector3(0.10f, 3.4f, 0.10f),
            cityCyanMaterial);
        CreateBox(
            "Horizon Right",
            new Vector3(farHalfWidth + 0.35f, 1.7f, z),
            new Vector3(0.10f, 3.4f, 0.10f),
            cityCyanMaterial);
        CreateBox(
            "Horizon Top",
            new Vector3(0.0f, 3.4f, z),
            new Vector3(farHalfWidth * 2.0f + 0.8f, 0.10f, 0.10f),
            railMaterial);
    }

    private GameObject CreateBox(
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(generatedRoot, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;

        Collider boxCollider = box.GetComponent<Collider>();
        if (boxCollider != null)
        {
            DestroySceneObject(boxCollider);
        }

        return box;
    }

    private void CreateRail(
        string objectName,
        float nearX,
        float farX,
        float halfThickness,
        Material material)
    {
        CreateRail(objectName, nearX, farX,
            halfThickness, halfThickness * 0.45f, material);
    }

    private void CreateRail(
        string objectName,
        float nearX,
        float farX,
        float nearHalfThickness,
        float farHalfThickness,
        Material material)
    {
        CreateQuadMesh(
            objectName,
            new Vector3(nearX - nearHalfThickness, 0.035f, nearZ),
            new Vector3(nearX + nearHalfThickness, 0.035f, nearZ),
            new Vector3(farX - farHalfThickness, 0.035f, LaneEndZ),
            new Vector3(farX + farHalfThickness, 0.035f, LaneEndZ),
            material);
    }

    private void CreateHorizontalBar(
        string objectName,
        float z,
        float y,
        float sideInset,
        float halfDepth,
        Material material)
    {
        float nearBarHalfWidth = Mathf.Max(
            0.0f,
            HalfWidthAtZ(z - halfDepth) - sideInset);
        float farBarHalfWidth = Mathf.Max(
            0.0f,
            HalfWidthAtZ(z + halfDepth) - sideInset);
        CreateQuadMesh(
            objectName,
            new Vector3(-nearBarHalfWidth, y + 0.045f, z - halfDepth),
            new Vector3(nearBarHalfWidth, y + 0.045f, z - halfDepth),
            new Vector3(-farBarHalfWidth, y + 0.045f, z + halfDepth),
            new Vector3(farBarHalfWidth, y + 0.045f, z + halfDepth),
            material);
    }

    private GameObject CreateQuadMesh(
        string objectName,
        Vector3 nearLeft,
        Vector3 nearRight,
        Vector3 farLeft,
        Vector3 farRight,
        Material material)
    {
        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(generatedRoot, false);

        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        mesh.name = objectName + " Mesh";
        mesh.vertices = new Vector3[] { nearLeft, nearRight, farLeft, farRight };
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        return meshObject;
    }

    private void CacheLaneGlowRenderers()
    {
        laneGlowRenderers = new MeshRenderer[RhythmGameController.LaneCount];
        if (generatedRoot == null)
        {
            generatedRoot = transform.Find(GeneratedRootName);
        }

        if (generatedRoot == null)
        {
            return;
        }

        for (int lane = 0; lane < RhythmGameController.LaneCount; lane++)
        {
            Transform glow = generatedRoot.Find("Held Lane Glow " + (lane + 1));
            if (glow != null)
            {
                laneGlowRenderers[lane] = glow.GetComponent<MeshRenderer>();
                laneGlowRenderers[lane].enabled = false;
            }
        }
    }

    private float HalfWidthAtZ(float z)
    {
        return Mathf.Lerp(nearHalfWidth, farHalfWidth, NormalizedZ(z));
    }

    private float NormalizedZ(float z)
    {
        return Mathf.Clamp01((z - nearZ) / (farZ - nearZ));
    }

    private static void DestroySceneObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
