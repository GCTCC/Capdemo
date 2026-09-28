using UnityEngine;
using UnityEngine.InputSystem;

// 횡스크롤 플레이어: A/D·←/→로 좌우 이동, 스페이스바로 점프.
// NPC 근처에서는 스페이스바가 점프 대신 대화를 연다. 대화 중에는 이동·점프를 막고 스페이스바로 대사를 넘긴다.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [Tooltip("점프 최고 높이(유닛). 중력에 맞춰 점프 속도를 계산한다.")]
    [SerializeField, Min(0f)] private float jumpHeight = 2.5f;
    [Tooltip("바닥에서 떨어진 뒤에도 이 시간(초) 동안은 점프를 받아 준다.")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.08f;
    [Tooltip("바닥 판정 거리(유닛)")]
    [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.05f;
    [SerializeField] private DemoDialogue dialogue;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
    private ContactFilter2D groundFilter;
    private float lastGroundedTime = float.NegativeInfinity;
    private float moveInput;
    private bool jumpQueued;
    private NpcInteract nearbyNpc;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        groundFilter = new ContactFilter2D { useTriggers = false };
        groundFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (dialogue && dialogue.IsOpen)
        {
            moveInput = 0f;
            if (AdvancePressed(keyboard)) dialogue.Advance();
            return;
        }

        moveInput = ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f)
                  - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1f : 0f);

        if (!keyboard.spaceKey.wasPressedThisFrame) return;
        if (nearbyNpc && dialogue) dialogue.Open(nearbyNpc, transform);
        else jumpQueued = true;
    }

    private void FixedUpdate()
    {
        if (IsGrounded()) lastGroundedTime = Time.time;

        var velocity = body.linearVelocity;
        velocity.x = moveInput * moveSpeed;
        if (jumpQueued && Time.time - lastGroundedTime <= coyoteTime)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
            velocity.y = Mathf.Sqrt(2f * gravity * jumpHeight);
            lastGroundedTime = float.NegativeInfinity;
        }
        jumpQueued = false;
        body.linearVelocity = velocity;
    }

    // 발밑으로 콜라이더를 살짝 밀어 봐서 닿는 게 있으면 바닥에 서 있는 것
    private bool IsGrounded()
    {
        if (body.linearVelocity.y > 0.01f) return false;
        return bodyCollider.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance) > 0;
    }

    private static bool AdvancePressed(Keyboard keyboard) =>
        keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
        keyboard.numpadEnterKey.wasPressedThisFrame;

    // NpcInteract가 대화 범위에 들어오고 나갈 때 알려 준다
    public void SetNearbyNpc(NpcInteract npc) => nearbyNpc = npc;

    public void ClearNearbyNpc(NpcInteract npc)
    {
        if (nearbyNpc == npc) nearbyNpc = null;
    }
}
