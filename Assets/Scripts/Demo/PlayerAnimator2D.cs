using UnityEngine;

// 플레이어 속도를 보고 Idle/Walk 애니메이션을 바꾸고, 가는 방향으로 그림을 뒤집는다.
// 시트 그림은 오른쪽을 보고 있다.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimator2D : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");

    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D body;

    private void Awake() => body = GetComponent<Rigidbody2D>();

    private void Update()
    {
        float vx = body.linearVelocity.x;
        if (animator) animator.SetFloat(SpeedId, Mathf.Abs(vx));
        if (spriteRenderer && Mathf.Abs(vx) > 0.01f) spriteRenderer.flipX = vx < 0f;
    }
}
