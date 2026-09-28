using UnityEngine;

// 평소에는 플레이어를 따라가고, 대화 중에는 플레이어와 NPC 사이로 옮겨 가며 줌인한다.
// 위치와 줌 모두 SmoothDamp로 옮겨서 프레임레이트와 상관없이 가속·감속하며 부드럽게 도착한다.
[RequireComponent(typeof(Camera))]
public class FollowCamera2D : MonoBehaviour
{
    [Header("따라가기")]
    [SerializeField] private Transform target;
    [Tooltip("플레이어 기준 카메라 위치")]
    [SerializeField] private Vector2 followOffset = new Vector2(0f, 1.5f);
    [Tooltip("평소 화면 크기 (Orthographic Size)")]
    [SerializeField, Min(0.1f)] private float normalSize = 5f;
    [Tooltip("따라가는 데 걸리는 시간(초). 0이면 바로 붙는다.")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.15f;

    [Header("대화 클로즈업")]
    [Tooltip("클로즈업 정도. 1이면 줌인하지 않고, 작을수록 가깝게 당겨진다.")]
    [SerializeField, Range(0.2f, 1f)] private float closeUpZoom = 0.55f;
    [Tooltip("두 캐릭터 중간 지점 기준 카메라 위치. 대사창에 가리지 않게 아래로 내려 캐릭터를 화면 위쪽에 둔다.")]
    [SerializeField] private Vector2 closeUpOffset = new Vector2(0f, -0.6f);
    [Tooltip("두 캐릭터가 멀리 있을 때 화면 양옆에 남길 여백(유닛). 둘 다 화면에 들어오도록 필요하면 덜 당긴다.")]
    [SerializeField, Min(0f)] private float closeUpPadding = 1f;
    [Tooltip("클로즈업으로 들어가고 나오는 데 걸리는 시간(초)")]
    [SerializeField, Min(0f)] private float closeUpSmoothTime = 0.4f;

    private Camera cam;
    private Transform focusA, focusB;
    private Vector3 moveVelocity;
    private float zoomVelocity;

    private bool Focused => focusA && focusB;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = normalSize;
        if (target) transform.position = WithZ(target.position + (Vector3)followOffset);
    }

    public void FocusOn(Transform a, Transform b)
    {
        focusA = a;
        focusB = b;
    }

    public void ClearFocus() => focusA = focusB = null;

    private void LateUpdate()
    {
        Vector3 goal;
        float goalSize;
        if (Focused)
        {
            Vector3 a = focusA.position, b = focusB.position;
            goal = (a + b) * 0.5f + (Vector3)closeUpOffset;
            float fitSize = (Mathf.Abs(a.x - b.x) * 0.5f + closeUpPadding) / cam.aspect;
            goalSize = Mathf.Max(normalSize * closeUpZoom, fitSize);
        }
        else if (target)
        {
            goal = target.position + (Vector3)followOffset;
            goalSize = normalSize;
        }
        else return;

        // 클로즈업에서 돌아오는 동안에도 클로즈업 속도로 움직인다
        bool zoomed = Mathf.Abs(cam.orthographicSize - normalSize) > 0.01f;
        float smoothTime = Focused || zoomed ? closeUpSmoothTime : followSmoothTime;
        transform.position = Vector3.SmoothDamp(transform.position, WithZ(goal), ref moveVelocity, smoothTime);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, goalSize, ref zoomVelocity, closeUpSmoothTime);
    }

    private Vector3 WithZ(Vector3 p) => new Vector3(p.x, p.y, transform.position.z);
}
