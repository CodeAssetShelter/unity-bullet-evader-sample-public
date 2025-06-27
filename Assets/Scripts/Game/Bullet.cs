using UnityEngine;

public enum BulletPattern
{
    None,
    Normal,
    Spread,
    Fan,
    Winder,
    Cage,
    State_Count
}

public class Bullet : MonoBehaviour
{
    [SerializeField] private Rigidbody2D m_Rb;
    [SerializeField] private float m_Speed = 5f;
    [SerializeField] private float m_LevelSpeed = 1f;
    [SerializeField] private float m_Lifespan = 15f;

    public bool IsLaunched => m_Launch;

    private Vector2 m_Dir = Vector2.zero;
    private Transform m_PlayerTransform;

    private BulletPattern m_Pattern = BulletPattern.None;
    private float m_LifeTimer = 0f;

    private bool m_Launch = false;
    private bool m_WasInScreen = false;

    private float m_SpreadTimer = 4f;
    private float m_SpreadTimeStamp = 0;

    private Vector2 m_FanCenter;
    private float m_FanAngularSpeedDeg;

    private Camera m_Cam;

    public ushort m_BulletId = 0;

    private float m_InvFixedDT = 0.02f;

    private void Awake()
    {
        m_Cam = Camera.main;
        m_InvFixedDT = 1f / Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        m_Launch = false;
        m_WasInScreen = false;
        m_LifeTimer = 0f;
        m_SpreadTimer = 0f;
        m_Pattern = BulletPattern.None;
    }

    private void FixedUpdate()
    {
        switch (m_Pattern)
        {
            case BulletPattern.Fan:
                UpdateFan();
                break;
            case BulletPattern.Spread:
                UpdateSpread();
                MoveBullet();
                break;
            default:
                MoveBullet();
                break;
        }

        CheckScreenBoundary();

        m_LifeTimer += Time.fixedDeltaTime;
        if (m_LifeTimer >= m_Lifespan)
            ReleaseObject();
    }

    private void MoveBullet()
    {
        m_Rb.linearVelocity = m_Dir * m_Speed * m_LevelSpeed;
    }

    const float VIEWPORT_MARGIN = 0.08f;
    private void CheckScreenBoundary()
    {
        if (m_Cam == null) return;

        Vector3 vp = m_Cam.WorldToViewportPoint(transform.position);
        bool isInScreen = vp.z > 0f &&
                          vp.x >= -VIEWPORT_MARGIN && vp.x <= 1f + VIEWPORT_MARGIN &&
                          vp.y >= -VIEWPORT_MARGIN && vp.y <= 1f + VIEWPORT_MARGIN;

        if (!m_WasInScreen && isInScreen)
        {
            m_WasInScreen = true;
            m_Launch = true;
        }
        else if (m_WasInScreen && !isInScreen)
        {
            ReleaseObject();
        }
    }

    private void ApplyHomingRotation()
    {
        Vector2 currentDirection = m_Dir;
        Vector2 targetDirection = ((Vector2)m_PlayerTransform.position - (Vector2)transform.position).normalized;
        float maxRotateAngle = Mathf.Clamp(28f + 2f * m_LevelSpeed, 30f, 60f) * Time.fixedDeltaTime;
        float angleBetween = Vector2.SignedAngle(currentDirection, targetDirection);
        float rotateAngle = Mathf.Clamp(angleBetween, -maxRotateAngle, maxRotateAngle);
        m_Dir = (Quaternion.Euler(0, 0, rotateAngle) * currentDirection).normalized;
    }

    private void UpdateFan()
    {
        float dt = Time.fixedDeltaTime;

        /* 1) 현재 위치와 pivot-to-bullet 벡터 r' */
        Vector2 curPos = m_Rb.position;
        Vector2 offset = curPos - m_FanCenter;

        /* 3) 이번 스텝 회전각 Δθ = ω·Δt (deg → rad) */
        float deltaRad = m_FanAngularSpeedDeg * Mathf.Deg2Rad * dt;

        /* 4) r⃗ 를 Δθ 만큼 회전 : r' = R(Δθ)·r  */
        float cos = Mathf.Cos(deltaRad);
        float sin = Mathf.Sin(deltaRad);
        Vector2 rotatedOffset = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos);

        /* 5) 목표 위치 = pivot + r' */
        Vector2 targetPos = m_FanCenter + rotatedOffset;

        /* 6) v = Δp / Δt  →  linearVelocity 적용 */
        m_Rb.linearVelocity = ((targetPos - curPos) * m_InvFixedDT) * (0.4f + (m_LevelSpeed * 0.1f));
    }

    private void UpdateSpread()
    {
        if (m_SpreadTimeStamp > m_SpreadTimer)
        {
            m_SpreadTimeStamp = 0;
            BulletSpawner.Instance.RequestSpreadShot(transform.position, m_Dir);
        }
        m_SpreadTimeStamp += Time.fixedDeltaTime;
    }

    public void SetId(ushort _id)
    {
        if (_id > 0)
            m_BulletId = _id;
    }

    public void SetPattern(BulletPattern pattern, Vector2 dir, float levelSpeed, float bulletOffset = -1)
    {
        m_Dir = dir.normalized;
        m_LevelSpeed = levelSpeed;
        m_Pattern = pattern;
        m_PlayerTransform = null;

        switch (pattern)
        {
            // Normal : 일반탄
            // Spread : 확산탄, 일반탄 알고리즘에 확산탄 생성은 BulletSpawner 에서 관리
            // Cage : 가두기, 일반탄 알고리즘 따름
            // Winder : 와인더, 일반탄 알고리즘 따름
            case BulletPattern.Normal:
                break;
            case BulletPattern.Spread:
                if (bulletOffset > 0)
                {
                    m_SpreadTimer = bulletOffset * 0.1f;
                }
                break;
            case BulletPattern.Cage:
            case BulletPattern.Winder:
                break;
            case BulletPattern.Fan:
                // pivot = 뷰포트 (dir) → 월드 좌표
                m_FanCenter = dir;

                // 회전 속도 및 방향 결정
                float baseAngularDeg = 45f + levelSpeed;

                Vector2 offset = (Vector2)transform.position - m_FanCenter;  // pivot → bullet
                Vector2 dirToScreenCtr = Vector2.zero - m_FanCenter;                 // pivot → (0,0)

                float signed = Vector2.SignedAngle(offset.normalized,
                                                   dirToScreenCtr.normalized);        // CCW 양수
                m_FanAngularSpeedDeg = baseAngularDeg * Mathf.Sign(signed);
                break;
        }
    }

    private void ReleaseObject()
    {
        m_Rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);

        LocalObjectPool.Instance.Release(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            BulletSpawner.Instance.RPC_ReleaseBullet(m_BulletId);
        }        
    }
}
