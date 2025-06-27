using UnityEngine;

public class BulletTest : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Transform pivot;              // 공전 중심
    public float radius = 5f;        // 고정 반지름
    public float angularSpeedDeg = 45f; // +CCW, –CW  (°/sec)

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;            // 중력 무시
    }

    private void FixedUpdate()
    {
        if (pivot == null) return;

        float dt = Time.fixedDeltaTime;

        /* 1) 현재 위치와 pivot-to-bullet 벡터 r⃗ */
        Vector2 curPos = _rb.position;
        Vector2 offset = curPos - (Vector2)pivot.position;

        /* 2) r⃗ 길이를 원하는 반지름으로 강제 (오차 보정) */
        offset = offset.sqrMagnitude < 1e-6f
                 ? Vector2.right * radius          // pivot과 겹쳤을 때
                 : offset.normalized * radius;     // 항상 정확히 radius

        /* 3) 이번 스텝 회전각 Δθ = ω·Δt (deg → rad) */
        float deltaRad = angularSpeedDeg * Mathf.Deg2Rad * dt;

        /* 4) r⃗ 를 Δθ 만큼 회전 : r' = R(Δθ)·r  */
        float cos = Mathf.Cos(deltaRad);
        float sin = Mathf.Sin(deltaRad);
        Vector2 rotatedOffset = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos);

        /* 5) 목표 위치 = pivot + r' */
        Vector2 targetPos = (Vector2)pivot.position + rotatedOffset;

        /* 6) v = Δp / Δt  →  linearVelocity 적용 */
        _rb.linearVelocity = (targetPos - curPos) / dt;
    }
}
