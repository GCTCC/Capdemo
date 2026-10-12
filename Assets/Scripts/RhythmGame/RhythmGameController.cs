using UnityEngine;
using UnityEngine.InputSystem;

// 리듬게임 씬의 시작, 입력, 플레이필드와 UI를 관리하고 나머지 시스템을 연결한다.
[RequireComponent(typeof(RhythmClock))]
[RequireComponent(typeof(NoteManager))]
[RequireComponent(typeof(JudgementScoreManager))]
[RequireComponent(typeof(ChartAutoLoader))]
[DisallowMultipleComponent]
public class RhythmGameController : MonoBehaviour
{
    private const int LaneCount = 6;

    [Header("Play Field")]
    public float nearZ = 0.0f;
    public float farZ = 20.0f;
    public float nearHalfWidth = 5.8f;
    public float farHalfWidth = 2.35f;
    public float judgeZ = 1.15f;

    [Header("Notes")]
    public float noteSpeed = 15.0f;
    public float minSpawnInterval = 0.52f;
    public float maxSpawnInterval = 0.78f;

    [Range(0.0f, 1.0f)]
    public float doubleNoteChance = 0.32f;

    [Header("Chart")]
    // 실제 곡과 채보를 연결한다. 비워 두면 기존처럼 랜덤 노트를 생성한다.
    public ChartData chartData;

    [Header("Input System")]
    public InputActionAsset inputActions;

    private readonly string[] laneLabels = { "A", "S", "D", "J", "K", "L" };
    private readonly string[] fallbackBindings =
    {
        "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
        "<Keyboard>/j", "<Keyboard>/k", "<Keyboard>/l"
    };

    private InputActionMap gameplayActionMap;
    private InputAction[] laneActions;
    private bool ownsRuntimeActionMap;

    // 분리된 리듬게임 시스템 참조다.
    private RhythmClock rhythmClock;
    private NoteManager noteManager;
    private JudgementScoreManager judgementScoreManager;
    private ChartAutoLoader chartAutoLoader;

    // Inspector의 ChartData 또는 자동 탐색 결과 중 실제 플레이에 사용할 데이터다.
    private ChartData activeChartData;

    // 모든 머티리얼 슬롯은 기본값이 None이다.
    // 프로젝트의 .mat 에셋을 Inspector에서 원하는 슬롯에 직접 연결한다.
    [Header("Materials - Play Field")]
    [SerializeField] private Material boardMaterial;
    [SerializeField] private Material sideFloorMaterial;
    [SerializeField] private Material railMaterial;
    [SerializeField] private Material railGlowMaterial;
    [SerializeField] private Material gridMaterial;
    [SerializeField] private Material judgeMaterial;

    [Header("Materials - Notes and Lane Input")]
    [SerializeField] private Material leftNoteMaterial;
    [SerializeField] private Material rightNoteMaterial;
    [SerializeField] private Material leftLaneGlowMaterial;
    [SerializeField] private Material rightLaneGlowMaterial;

    [Header("Materials - Cyber City")]
    [SerializeField] private Material cityMaterialA;
    [SerializeField] private Material cityMaterialB;
    [SerializeField] private Material cityCyanMaterial;
    [SerializeField] private Material cityMagentaMaterial;

    private MeshRenderer[] laneGlowRenderers;

    private Camera gameCamera;
    private float judgementTimer;
    private string judgementText = "READY";

    private void Awake()
    {
        Application.targetFrameRate = 60;

        // RequireComponent로 보장된 세 시스템을 가져온다.
        rhythmClock = GetComponent<RhythmClock>();
        noteManager = GetComponent<NoteManager>();
        judgementScoreManager = GetComponent<JudgementScoreManager>();
        chartAutoLoader = GetComponent<ChartAutoLoader>();

        // Inspector에 직접 연결한 ChartData가 가장 높은 우선순위를 가진다.
        // 연결된 에셋이 없을 때만 Resources/RhythmSongs의 동명 파일을 자동 탐색한다.
        activeChartData = chartData;
        if (activeChartData == null && chartAutoLoader != null)
        {
            chartAutoLoader.TryLoadChart(out activeChartData);
        }

        BuildInputActions();
        BuildCamera();
        BuildStage();

        // 점수 시스템이 판정할 노트 목록을 찾을 수 있도록 연결한다.
        judgementScoreManager.Initialize(noteManager);

        // NoteManager에 이동과 랜덤 생성에 필요한 설정을 전달한다.
        noteManager.Initialize(
            rhythmClock,
            activeChartData,
            leftNoteMaterial,
            rightNoteMaterial,
            nearZ,
            farZ,
            judgeZ,
            nearHalfWidth,
            farHalfWidth,
            noteSpeed,
            minSpawnInterval,
            maxSpawnInterval,
            doubleNoteChance);

        // 시스템끼리 직접 UI를 수정하지 않도록 이벤트로 연결한다.
        noteManager.NoteMissed += HandleNoteMissed;
        judgementScoreManager.Judged += HandleJudged;

        // 모든 시스템 준비가 끝난 뒤 음악 시계를 시작한다.
        rhythmClock.StartClock(activeChartData);
    }

    private void OnEnable()
    {
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Enable();
        }
    }

    private void OnDisable()
    {
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Disable();
        }
    }

    private void Update()
    {
        HandleLaneActions();
        UpdateLaneGlows();
        noteManager.Tick();

        if (judgementTimer > 0.0f)
        {
            judgementTimer -= Time.unscaledDeltaTime;
        }
    }

    // 외부 일시정지 UI에서 호출할 수 있는 정지 함수다.
    public void PauseGame()
    {
        rhythmClock.PauseClock();
    }

    // 외부 일시정지 UI에서 호출할 수 있는 재개 함수다.
    public void ResumeGame()
    {
        rhythmClock.ResumeClock();
    }

    // 결과 화면이나 스토리에 전달할 현재 플레이 결과를 만든다.
    public PlayResult CreatePlayResult()
    {
        return judgementScoreManager.CreatePlayResult();
    }

    // Input Actions 에셋 또는 예비 런타임 액션을 6개 레인에 연결한다.
    private void BuildInputActions()
    {
        laneActions = new InputAction[LaneCount];

        if (inputActions != null)
        {
            gameplayActionMap = inputActions.FindActionMap("Gameplay", true);
            for (int i = 0; i < LaneCount; i++)
            {
                laneActions[i] = gameplayActionMap.FindAction("Lane" + (i + 1), true);
            }
            return;
        }

        // Input Actions 에셋이 없는 수동 씬에서도 키 입력이 동작하게 한다.
        ownsRuntimeActionMap = true;
        gameplayActionMap = new InputActionMap("Gameplay");
        for (int i = 0; i < LaneCount; i++)
        {
            laneActions[i] = gameplayActionMap.AddAction(
                "Lane" + (i + 1),
                InputActionType.Button,
                fallbackBindings[i]);
        }
    }

    // 새로 눌린 레인 키와 정확한 음악 시각을 판정 시스템에 전달한다.
    private void HandleLaneActions()
    {
        if (rhythmClock.IsPaused)
        {
            return;
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            if (laneActions[lane].WasPressedThisFrame())
            {
                judgementScoreManager.TryJudge(lane, rhythmClock.SongTime);
            }
        }
    }

    private bool IsLaneHeld(int lane)
    {
        return laneActions != null
            && laneActions[lane] != null
            && laneActions[lane].IsPressed();
    }

    // 노트가 판정 가능 시간을 지나쳤을 때 콤보와 MISS 수를 갱신한다.
    private void HandleNoteMissed(NoteManager.RuntimeNote note)
    {
        judgementScoreManager.RegisterMiss(note);
    }

    // 판정 시스템의 결과를 화면 중앙 문구로 변환한다.
    private void HandleJudged(JudgementResult result)
    {
        ShowJudgement(result.judgement.ToString().ToUpperInvariant());
    }

    private void ShowJudgement(string text)
    {
        judgementText = text;
        judgementTimer = 0.45f;
    }

    // 사다리꼴 필드를 비스듬히 내려다보는 원근 카메라를 만든다.
    private void BuildCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject newCameraObject = new GameObject("Rhythm Camera");
            newCameraObject.tag = "MainCamera";
            gameCamera = newCameraObject.AddComponent<Camera>();
            newCameraObject.AddComponent<AudioListener>();
        }

        GameObject cameraObject = gameCamera.gameObject;

        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.008f, 0.012f, 0.035f, 1.0f);

        gameCamera.fieldOfView = 55.0f;
        gameCamera.nearClipPlane = 0.01f;
        gameCamera.farClipPlane = 100.0f;

        cameraObject.transform.position = new Vector3(0.0f, 8.0f, -9.2f);

        cameraObject.transform.rotation = Quaternion.LookRotation(
            new Vector3(0.0f, 0.0f, 8.2f) - cameraObject.transform.position, Vector3.up);
    }

    // 플레이필드, 레일, 격자, 판정선과 사이버 도시를 만든다.
    private void BuildStage()
    {
        // Renderer 참조만 런타임에 만들고 머티리얼은 Inspector 값을 그대로 사용한다.
        laneGlowRenderers = new MeshRenderer[LaneCount];

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

        // 6개 레인의 양 끝을 포함해 7개의 경계선을 만든다.
        for (int i = 0; i <= LaneCount; i++)
        {
            float ratio = (float)i / LaneCount;
            float nearX = Mathf.Lerp(-nearHalfWidth, nearHalfWidth, ratio);
            float farX = Mathf.Lerp(-farHalfWidth, farHalfWidth, ratio);
            CreateRail("Lane Glow Rail " + i, nearX, farX, 0.085f, railGlowMaterial);
            CreateRail("Lane Rail " + i, nearX, farX, 0.025f, railMaterial);
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            CreateLaneGlow(lane);
        }

        for (float z = 2.6f; z < farZ - 0.5f; z += 2.45f)
        {
            CreateGridBar(z);
        }

        CreateHorizontalBar("Judgement Line", judgeZ, 0.0f, judgeMaterial);
        CreateHorizontalBar("Far Gate", farZ - 0.15f, -0.01f, railMaterial);
        BuildCyberCity();
        BuildHorizonPortal();
    }

    // 키를 누르는 동안 켜질 레인 전체의 반투명 사다리꼴을 만든다.
    private void CreateLaneGlow(int lane)
    {
        float nearLaneWidth = nearHalfWidth * 2.0f / LaneCount;
        float farLaneWidth = farHalfWidth * 2.0f / LaneCount;
        float nearLeft = -nearHalfWidth + nearLaneWidth * lane + 0.05f;
        float nearRight = nearLeft + nearLaneWidth - 0.10f;
        float farLeft = -farHalfWidth + farLaneWidth * lane + 0.025f;
        float farRight = farLeft + farLaneWidth - 0.05f;

        Material laneGlowMaterial = lane < 3
            ? leftLaneGlowMaterial
            : rightLaneGlowMaterial;

        GameObject glow = CreateQuadMesh(
            "Held Lane Glow " + (lane + 1),
            new Vector3(nearLeft, 0.018f, nearZ),
            new Vector3(nearRight, 0.018f, nearZ),
            new Vector3(farLeft, 0.018f, farZ),
            new Vector3(farRight, 0.018f, farZ),
            laneGlowMaterial);

        laneGlowRenderers[lane] = glow.GetComponent<MeshRenderer>();
        laneGlowRenderers[lane].enabled = false;
    }

    private void CreateGridBar(float z)
    {
        float halfWidth = HalfWidthAtZ(z);
        float depth = 0.025f;
        CreateQuadMesh(
            "Cyber Grid " + z,
            new Vector3(-halfWidth, 0.012f, z - depth),
            new Vector3(halfWidth, 0.012f, z - depth),
            new Vector3(-halfWidth, 0.012f, z + depth),
            new Vector3(halfWidth, 0.012f, z + depth),
            gridMaterial);
    }

    // 플레이필드 양옆에 높이와 색상이 다른 건물을 배치한다.
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
                float x = side * (HalfWidthAtZ(Mathf.Min(z, farZ)) + 1.55f + (i % 2) * 0.48f);
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
            cityMagentaMaterial);
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
        box.transform.parent = transform;
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;

        Collider boxCollider = box.GetComponent<Collider>();
        if (boxCollider != null)
        {
            Destroy(boxCollider);
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
        CreateQuadMesh(
            objectName,
            new Vector3(nearX - halfThickness, 0.025f, nearZ),
            new Vector3(nearX + halfThickness, 0.025f, nearZ),
            new Vector3(farX - halfThickness * 0.45f, 0.025f, farZ),
            new Vector3(farX + halfThickness * 0.45f, 0.025f, farZ),
            material);
    }

    private void CreateHorizontalBar(
        string objectName,
        float z,
        float y,
        Material material)
    {
        float halfWidth = HalfWidthAtZ(z);
        float depth = 0.13f;
        CreateQuadMesh(
            objectName,
            new Vector3(-halfWidth, y + 0.045f, z - depth),
            new Vector3(halfWidth, y + 0.045f, z - depth),
            new Vector3(-halfWidth, y + 0.045f, z + depth),
            new Vector3(halfWidth, y + 0.045f, z + depth),
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
        meshObject.transform.parent = transform;

        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        mesh.name = objectName + " Mesh";
        mesh.vertices = new Vector3[] { nearLeft, nearRight, farLeft, farRight };
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        return meshObject;
    }

    private void UpdateLaneGlows()
    {
        if (laneGlowRenderers == null)
        {
            return;
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            bool held = IsLaneHeld(lane);
            laneGlowRenderers[lane].enabled = held;
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

    // 점수, 콤보, 판정과 키 안내를 화면에 표시한다.
    private void OnGUI()
    {
        DrawScreenRect(
            new Rect(14, 12, 365, 112),
            new Color(0.01f, 0.02f, 0.06f, 0.78f));
        DrawScreenRect(
            new Rect(14, 12, 5, 112),
            new Color(0.05f, 0.95f, 1.0f, 0.95f));
        DrawScreenRect(
            new Rect(Screen.width - 230, 18, 210, 34),
            new Color(0.05f, 0.01f, 0.08f, 0.72f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = Mathf.Max(20, Screen.height / 32);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.30f, 1.0f, 0.95f);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = Mathf.Max(15, Screen.height / 48);
        infoStyle.normal.textColor = new Color(0.60f, 0.95f, 1.0f);

        GUI.Label(new Rect(28, 18, 500, 45), "CYBER//RHYTHM", titleStyle);
        GUI.Label(
            new Rect(24, 58, 450, 28),
            "SCORE  " + judgementScoreManager.Score,
            infoStyle);
        GUI.Label(
            new Rect(24, 84, 450, 28),
            "COMBO  " + judgementScoreManager.Combo
                + "    BEST  " + judgementScoreManager.MaxCombo,
            infoStyle);

        GUIStyle statusStyle = new GUIStyle(infoStyle);
        statusStyle.alignment = TextAnchor.MiddleCenter;
        statusStyle.normal.textColor = new Color(1.0f, 0.25f, 0.80f);
        GUI.Label(
            new Rect(Screen.width - 230, 20, 210, 28),
            "BEAT // " + rhythmClock.CurrentBeat.ToString("0.0"),
            statusStyle);

        if (judgementTimer > 0.0f || judgementText == "READY")
        {
            GUIStyle judgeStyle = new GUIStyle(titleStyle);
            judgeStyle.alignment = TextAnchor.MiddleCenter;
            judgeStyle.fontSize = Mathf.Max(28, Screen.height / 20);
            judgeStyle.normal.textColor = judgementText == "MISS"
                ? new Color(1.0f, 0.35f, 0.45f)
                : new Color(0.40f, 1.0f, 0.90f);
            GUI.Label(
                new Rect(0, Screen.height * 0.45f, Screen.width, 70),
                judgementText,
                judgeStyle);
        }

        DrawKeyHints();
    }

    private void DrawScreenRect(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawKeyHints()
    {
        GUIStyle keyStyle = new GUIStyle(GUI.skin.box);
        keyStyle.fontSize = Mathf.Max(16, Screen.height / 42);
        keyStyle.fontStyle = FontStyle.Bold;
        keyStyle.alignment = TextAnchor.MiddleCenter;
        keyStyle.normal.textColor = Color.white;

        float totalWidth = Mathf.Min(Screen.width * 0.72f, 720.0f);
        float keyWidth = totalWidth / LaneCount;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - 62.0f;

        for (int i = 0; i < LaneCount; i++)
        {
            GUI.backgroundColor = IsLaneHeld(i)
                ? (i < 3
                    ? new Color(0.05f, 1.0f, 1.0f)
                    : new Color(1.0f, 0.08f, 0.78f))
                : new Color(0.16f, 0.12f, 0.28f);
            GUI.Box(
                new Rect(startX + keyWidth * i + 3.0f, y, keyWidth - 6.0f, 42.0f),
                laneLabels[i],
                keyStyle);
        }

        GUI.backgroundColor = Color.white;
    }

    private void OnDestroy()
    {
        if (noteManager != null)
        {
            noteManager.NoteMissed -= HandleNoteMissed;
        }

        if (judgementScoreManager != null)
        {
            judgementScoreManager.Judged -= HandleJudged;
        }

        if (rhythmClock != null)
        {
            rhythmClock.StopClock();
        }

        if (ownsRuntimeActionMap && gameplayActionMap != null)
        {
            gameplayActionMap.Dispose();
        }

        // Inspector에서 연결한 .mat 에셋은 프로젝트 자산이므로 여기서 Destroy하지 않는다.
    }
}
