using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class csCameraFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 followOffset = new Vector2(0f, 1.2f);
    [SerializeField] private float followSmoothTime = 0.2f;

    [Header("Movement Zoom")]
    [SerializeField] private float idleSize = 6f; 
    [SerializeField] private float movingSize = 5.2f; 
    [SerializeField] private float movingSpeedThreshold = 0.1f; 
    [SerializeField] private float zoomSmoothTime = 0.25f;

    [Header("Overview")]
    [SerializeField] private Vector2 overviewPosition = new Vector2(4f, 0f);
    [SerializeField] private float overviewSize = 8.5f;

    private Camera cameraComponent;
    private Rigidbody2D targetRigidbody;
    private Vector3 followVelocity;
    private float zoomVelocity;
    private bool isOverview;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();

        if (target != null)
        {
            targetRigidbody = target.GetComponent<Rigidbody2D>();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
        {
            isOverview = !isOverview;
        }
    }

    private void LateUpdate()
    {
        if (target == null || cameraComponent == null)
        {
            return;
        }

        bool isMoving = targetRigidbody != null &&
                        Mathf.Abs(targetRigidbody.linearVelocity.x) > movingSpeedThreshold; //움직이는지 확인

        Vector3 desiredPosition = isOverview
            ? new Vector3(overviewPosition.x, overviewPosition.y, transform.position.z)
            : new Vector3(
                target.position.x + followOffset.x,
                target.position.y + followOffset.y,
                transform.position.z); //카메라 목표 위치

        float desiredSize = isOverview
            ? overviewSize
            : isMoving ? movingSize : idleSize; //카메라 목표 범위크기

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime); //(현재위치, 목표위치, 카메라 이동속도, 부드러움), 속도값은 SmoothDamp가 알아서 계산하여 계속 값을 갱신함(개꿀)

        cameraComponent.orthographicSize = Mathf.SmoothDamp(
            cameraComponent.orthographicSize,
            desiredSize,
            ref zoomVelocity,
            zoomSmoothTime); //(현재범위, 목표범위, 줌 변화속도, 부드러움)
    }
}
