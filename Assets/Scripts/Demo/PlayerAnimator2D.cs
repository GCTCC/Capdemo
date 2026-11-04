using UnityEngine;

// 플레이어 속도와 바닥 상태를 Animator에 넘겨 Idle/Run/공중/착지 애니메이션을 고르게 하고,
// 가는 방향으로 그림을 뒤집는다. 시트 그림은 오른쪽을 보고 있다.
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerController2D))]
public class PlayerAnimator2D : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int VelocityYId = Animator.StringToHash("VelocityY");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");

    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D body;
    private PlayerController2D controller;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController2D>();
    }

    private void Update()
    {
        var velocity = body.linearVelocity;
        if (animator)
        {
            animator.SetFloat(SpeedId, Mathf.Abs(velocity.x));
            animator.SetFloat(VelocityYId, velocity.y);
            animator.SetBool(GroundedId, controller.IsGrounded);
        }
        if (spriteRenderer && Mathf.Abs(velocity.x) > 0.01f) spriteRenderer.flipX = velocity.x < 0f;
    }
}
