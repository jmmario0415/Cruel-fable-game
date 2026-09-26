using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody2D))]
public class csPlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 35f;
    [SerializeField] private float deceleration = 45f;

    private Rigidbody2D rb;
    private float moveDirection;
    private bool isFacingRight = true;

    private bool jumpRequested;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        //rb.freezeRotation = true;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        //이동
        moveDirection = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            moveDirection = -1f;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            moveDirection = 1f;
        }

        //점프
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame) &&
            IsGrounded())
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        //이동
        float targetSpeed = moveDirection * moveSpeed;

        float rate = moveDirection != 0f
            ? acceleration
            : deceleration; //이동 입력이 있으면 가속, 아니면 감속

        float newSpeed = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetSpeed,
            rate * Time.fixedDeltaTime //(현재 속도, 목표 속도, 프레임 당 변하는 속도량)
        );

        rb.linearVelocity = new Vector2(
            newSpeed,
            rb.linearVelocity.y
        );

        UpdateFacingDirection();

        //점프
        if (jumpRequested)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                jumpForce
            );

            jumpRequested = false;
        }
    }

    private void UpdateFacingDirection()
    {
        if (moveDirection > 0f && !isFacingRight)
        {
            Flip();
        }
        else if (moveDirection < 0f && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !isFacingRight;
        }
    }

    private bool IsGrounded()
        {
            return Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            ); //(검사 윈 중심 위치, 검사 원 반지름, 바닥으로 인정할 레이어 이름)
        }
}
