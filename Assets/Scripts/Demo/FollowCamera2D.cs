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

    [Header("화면 범위")]
    [Tooltip("이 배경 밖은 보여 주지 않는다. 비우면 씬에서 찾고, 없으면 범위 제한 없이 따라간다.")]
    [SerializeField] private MapBackground bounds;

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
        if (!target)
        {
            var player = FindAnyObjectByType<PlayerController2D>();
            if (player) target = player.transform;
        }
        if (!bounds) bounds = FindAnyObjectByType<MapBackground>();
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
        goal = ClampToBounds(goal, goalSize);

        // 클로즈업에서 돌아오는 동안에도 클로즈업 속도로 움직인다
        bool zoomed = Mathf.Abs(cam.orthographicSize - normalSize) > 0.01f;
        float smoothTime = Focused || zoomed ? closeUpSmoothTime : followSmoothTime;
        transform.position = Vector3.SmoothDamp(transform.position, WithZ(goal), ref moveVelocity, smoothTime);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, goalSize, ref zoomVelocity, closeUpSmoothTime);
    }

    // 화면 가장자리가 배경 밖으로 나가지 않게 카메라 중심을 안쪽으로 당긴다. 배경이 화면보다 좁으면 가운데에 둔다
    private Vector3 ClampToBounds(Vector3 goal, float size)
    {
        if (!bounds || !bounds.HasImage) return goal;
        var area = bounds.Area;
        Vector2 half = new Vector2(size * cam.aspect, size);
        goal.x = area.size.x <= half.x * 2f ? area.center.x : Mathf.Clamp(goal.x, area.min.x + half.x, area.max.x - half.x);
        goal.y = area.size.y <= half.y * 2f ? area.center.y : Mathf.Clamp(goal.y, area.min.y + half.y, area.max.y - half.y);
        return goal;
    }

    private Vector3 WithZ(Vector3 p) => new Vector3(p.x, p.y, transform.position.z);
}
