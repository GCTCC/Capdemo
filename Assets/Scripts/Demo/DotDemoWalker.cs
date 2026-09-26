using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(SpriteRenderer))]
public class DotDemoWalker : MonoBehaviour
{
    public Sprite idleSprite;
    public Sprite[] walkFrames;
    [Min(0)] public float moveSpeed = 3f;
    [Min(1)] public float framesPerSecond = 8f;
    public bool keepInsideCamera = true;
    private SpriteRenderer visual;
    private float walkTime;
    private bool wasMoving;
    private void Awake() { visual = GetComponent<SpriteRenderer>(); if (idleSprite) visual.sprite = idleSprite; }
    private void Update()
    {
        Vector2 direction = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            direction.x = ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1 : 0) - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1 : 0);
            direction.y = ((keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) ? 1 : 0) - ((keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) ? 1 : 0);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        direction.x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        direction.y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
#endif
        Step(direction, Time.deltaTime);
    }
    public void Step(Vector2 direction, float dt)
    {
        if (!visual) visual = GetComponent<SpriteRenderer>();
        direction = Vector2.ClampMagnitude(direction, 1f);
        bool moving = direction.sqrMagnitude > .0001f;
        if (moving)
        {
            if (!wasMoving) walkTime = 0;
            transform.position += (Vector3)(direction * moveSpeed * dt);
            if (walkFrames != null && walkFrames.Length > 0)
                visual.sprite = walkFrames[Mathf.FloorToInt(walkTime * framesPerSecond) % walkFrames.Length];
            walkTime += dt;
            ClampToCamera();
        }
        else { walkTime = 0; if (idleSprite) visual.sprite = idleSprite; }
        wasMoving = moving;
    }
    private void ClampToCamera()
    {
        var cam = Camera.main;
        if (!keepInsideCamera || !cam || !cam.orthographic) return;
        float depth = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
        var lo = cam.ViewportToWorldPoint(new Vector3(0, 0, depth));
        var hi = cam.ViewportToWorldPoint(new Vector3(1, 1, depth));
        var bounds = visual.bounds;
        var offset = bounds.center - transform.position;
        float xmin = lo.x + bounds.extents.x - offset.x, xmax = hi.x - bounds.extents.x - offset.x;
        float ymin = lo.y + bounds.extents.y - offset.y, ymax = hi.y - bounds.extents.y - offset.y;
        var p = transform.position;
        if (xmin <= xmax) p.x = Mathf.Clamp(p.x, xmin, xmax);
        if (ymin <= ymax) p.y = Mathf.Clamp(p.y, ymin, ymax);
        transform.position = p;
    }
}
