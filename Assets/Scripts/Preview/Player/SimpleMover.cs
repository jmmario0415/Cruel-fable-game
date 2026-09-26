using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 최소 이동 컨트롤러 — 좌우 이동과 점프만.
/// 대시·벽점프·공격·사다리·매달리기 없음. 규격 확인용 임시 스크립트.
///
/// 기본값은 `게임플레이-수치.md` 의 PPU 32 / 3유닛 캐릭터 기준.
/// 중력은 rb.gravityScale = 0 으로 두고 코드에서 계산한다 (프로젝트 규약).
///   중력    = 2h / t²
///   점프속도 = 2h / t
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("잔혹동화/Simple Mover")]
public class SimpleMover : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("유닛/초. 3유닛 캐릭터 기준 9 = 초당 캐릭터 키 3배")]
    public float moveSpeed = 9f;
    public float accel = 90f;
    public float decel = 120f;
    [Range(0f, 1f)] public float airControl = 0.65f;

    [Header("점프")]
    [Tooltip("유닛. 5.1 = 3유닛 캐릭터 키의 1.7배")]
    public float jumpHeight = 5.1f;
    [Tooltip("초. 점프 정점까지 걸리는 시간")]
    public float timeToApex = 0.38f;
    [Tooltip("하강 시 중력 배수")]
    public float fallMultiplier = 1.65f;
    [Tooltip("상승 중 점프키를 뗐을 때 중력 배수 (가변 점프)")]
    public float releaseMultiplier = 2.6f;
    public float maxFallSpeed = 30f;

    [Header("접지 판정")]
    public LayerMask groundMask = 1 << 6;          // 6 = Ground
    public Vector2 footSize = new Vector2(0.7f, 0.12f);
    public float coyoteTime = 0.10f;
    public float jumpBuffer = 0.12f;

    [Header("표시")]
    [Tooltip("좌우 반전할 자식. 비우면 자기 자신")]
    public Transform visual;

    Rigidbody2D rb;
    float inputX, coyote, buffered;
    bool holdJump, grounded;

    float Gravity   { get { return 2f * jumpHeight / (timeToApex * timeToApex); } }
    float JumpSpeed { get { return 2f * jumpHeight / timeToApex; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;                      // 중력은 아래에서 직접 계산
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        if (visual == null) visual = transform;
    }

    void Update()
    {
        inputX = ReadX();
        bool jumpDown = ReadJumpDown();
        holdJump = ReadJumpHeld();

        if (jumpDown) buffered = jumpBuffer;
        else if (buffered > 0f) buffered -= Time.deltaTime;
        if (coyote > 0f) coyote -= Time.deltaTime;

        if (Mathf.Abs(inputX) > 0.01f)
        {
            Vector3 s = visual.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(inputX);
            visual.localScale = s;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;

        // 접지
        Vector2 c = (Vector2)transform.position + Vector2.up * (footSize.y * 0.5f - 0.04f);
        grounded = Physics2D.OverlapBox(c, footSize, 0f, groundMask);
        if (grounded && v.y <= 0.01f) coyote = coyoteTime;

        // 좌우
        float target = inputX * moveSpeed;
        float a = (Mathf.Abs(target) > 0.01f ? accel : decel) * (grounded ? 1f : airControl);
        v.x = Mathf.MoveTowards(v.x, target, a * dt);

        // 점프
        if (buffered > 0f && coyote > 0f)
        {
            buffered = 0f;
            coyote = 0f;
            v.y = JumpSpeed;
        }

        // 중력
        float g = Gravity;
        if (v.y < 0f) g *= fallMultiplier;
        else if (v.y > 0f && !holdJump) g *= releaseMultiplier;
        v.y = Mathf.Max(v.y - g * dt, -maxFallSpeed);
        if (grounded && v.y < 0f) v.y = -1f;        // 경사·틈에서 붙어 있게

        rb.linearVelocity = v;
    }

    // ── 입력 ────────────────────────────────────────────────────────────
    static float ReadX()
    {
#if ENABLE_INPUT_SYSTEM
        float x = 0f;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
        }
        var g = Gamepad.current;
        if (g != null && Mathf.Abs(g.leftStick.x.ReadValue()) > 0.25f)
            x = g.leftStick.x.ReadValue();
        return Mathf.Clamp(x, -1f, 1f);
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    static bool ReadJumpDown()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)) return true;
        var g = Gamepad.current;
        return g != null && g.buttonSouth.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    static bool ReadJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null && (k.spaceKey.isPressed || k.wKey.isPressed || k.upArrowKey.isPressed)) return true;
        var g = Gamepad.current;
        return g != null && g.buttonSouth.isPressed;
#else
        return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#endif
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Application.isPlaying && grounded ? Color.green : Color.yellow;
        Vector2 c = (Vector2)transform.position + Vector2.up * (footSize.y * 0.5f - 0.04f);
        Gizmos.DrawWireCube(c, footSize);
    }
}
