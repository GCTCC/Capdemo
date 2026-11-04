using UnityEngine;

// 카메라를 일정 비율로 따라가서 먼 배경이 천천히 지나가는 것처럼 보이게 한다.
// 1이면 카메라에 붙어 화면에 고정되고, 0이면 일반 오브젝트처럼 월드에 고정된다.
[DefaultExecutionOrder(100)] // FollowCamera2D가 이번 프레임에 움직인 뒤에 따라간다
public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [Tooltip("가로로 카메라를 따라가는 비율. 1에 가까울수록 멀리 있는 것처럼 느리게 지나간다.")]
    [SerializeField, Range(0f, 1f)] private float followX = 0.9f;
    [Tooltip("세로로 카메라를 따라가는 비율")]
    [SerializeField, Range(0f, 1f)] private float followY = 1f;

    private Vector3 origin;

    private void Awake()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        origin = transform.position;
    }

    private void LateUpdate()
    {
        if (!cameraTransform) return;
        Vector3 cam = cameraTransform.position;
        transform.position = new Vector3(origin.x + cam.x * followX, origin.y + cam.y * followY, origin.z);
    }
}
