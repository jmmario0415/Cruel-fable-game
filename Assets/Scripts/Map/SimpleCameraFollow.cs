using UnityEngine;

/// <summary>
/// 최소 카메라 추적. 시차를 눈으로 확인하려면 카메라가 움직여야 해서 같이 넣는다.
/// ParallaxLayer 보다 먼저 실행되어야 한다 (실행 순서 -100 / +100 으로 지정).
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("잔혹동화/Simple Camera Follow")]
public class SimpleCameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("캐릭터 발 기준 오프셋. y 는 캐릭터 키의 절반쯤")]
    public Vector2 offset = new Vector2(0f, 2.5f);
    [Range(0f, 1f)] public float smooth = 0.15f;

    [Header("방 경계로 클램프")]
    public bool clampToRoom = true;
    public Vector2 roomMin = new Vector2(0f, 0f);
    public Vector2 roomMax = new Vector2(60f, 24f);

    Camera cam;
    Vector3 vel;

    void Awake() { cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();

        Vector3 want = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);

        if (clampToRoom)
        {
            float hh = cam.orthographicSize;
            float hw = hh * cam.aspect;
            want.x = Mathf.Clamp(want.x, roomMin.x + hw, Mathf.Max(roomMin.x + hw, roomMax.x - hw));
            want.y = Mathf.Clamp(want.y, roomMin.y + hh, Mathf.Max(roomMin.y + hh, roomMax.y - hh));
        }

        transform.position = smooth <= 0f
            ? want
            : Vector3.SmoothDamp(transform.position, want, ref vel, smooth);
    }
}
