using UnityEngine;

/// <summary>
/// 배경 시차 레이어.
///
/// factor = 월드(지형) 대비 스크롤 비율
///   0    = 전혀 스크롤 안 함 → 화면에 붙어 있음 (무한 원경)
///   0.5  = 지형의 절반 속도
///   1    = 지형과 동일 (월드 고정)
///   >1   = 지형보다 빠름 (전경, 지형 앞을 스쳐 지나감)
///
/// 필요 캔버스 = 화면 크기 + factor × 카메라 이동범위
/// 가로는 SpriteRenderer 를 Tiled 로 늘려 쓰므로 factor 와 무관하고, 세로만 제약이 된다.
/// 자연 풍경은 세로 심리스가 불가능하므로 factor.y 는 낮게(0.25 이하) 두는 것이 원칙.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("잔혹동화/Parallax Layer")]
public class ParallaxLayer : MonoBehaviour
{
    [Header("스크롤 비율 (월드 대비)")]
    [Tooltip("0 = 화면 고정(무한 원경) · 1 = 지형과 동일 · 1 초과 = 전경")]
    public Vector2 factor = new Vector2(0.5f, 0.125f);

    [Header("카메라가 (0,0) 일 때의 레이어 위치")]
    public Vector2 anchor;

    [Tooltip("비워두면 Camera.main 을 자동으로 찾는다")]
    public Transform cam;

    void OnEnable() { ResolveCam(); Apply(); }
    void LateUpdate() { Apply(); }

    void ResolveCam()
    {
        if (cam != null) return;
        var c = Camera.main;
        if (c == null)
        {
#if UNITY_2023_1_OR_NEWER
            c = Object.FindFirstObjectByType<Camera>();
#else
            c = Object.FindObjectOfType<Camera>();
#endif
        }
        if (c != null) cam = c.transform;
    }

    public void Apply()
    {
        ResolveCam();
        if (cam == null) return;
        Vector3 c = cam.position;
        transform.position = new Vector3(
            anchor.x + c.x * (1f - factor.x),
            anchor.y + c.y * (1f - factor.y),
            transform.position.z);
    }

    /// <summary>씬 뷰에서 눈으로 맞춘 현재 위치를 앵커로 고정한다.</summary>
    [ContextMenu("현재 위치를 앵커로 굽기")]
    public void BakeAnchor()
    {
        ResolveCam();
        if (cam == null) return;
        Vector3 c = cam.position;
        anchor = new Vector2(
            transform.position.x - c.x * (1f - factor.x),
            transform.position.y - c.y * (1f - factor.y));
    }
}
