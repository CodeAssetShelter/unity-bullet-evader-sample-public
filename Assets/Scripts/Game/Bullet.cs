using UnityEngine;

public enum BulletPattern
{
    None,
    Normal,
    Spread,
    Fan,
    Winder,
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

    private float m_SpreadTimer = 0f;
    private const float SPREAD_INTERVAL = 0.8f;

    private bool m_UseFan = false;
    private Vector2 m_FanCenter;
    private float m_FanAngularSpeedDeg;

    private Camera m_Cam;

    public ushort m_BulletId = 0;

    private void Awake()
    {
        m_Cam = Camera.main;
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
            default:
                m_Rb.linearVelocity = m_Dir * m_Speed * m_LevelSpeed;
                break;
        }

        //if (m_Pattern == BulletPattern.Homing && m_PlayerTransform)
        //    ApplyHomingRotation();

        if (m_Pattern == BulletPattern.Spread && m_Launch)
        {
            m_SpreadTimer += Time.fixedDeltaTime;
            if (m_SpreadTimer >= SPREAD_INTERVAL)
            {
                m_SpreadTimer = 0f;
                FireSpread();
            }
        }

        CheckScreenBoundary();

        m_LifeTimer += Time.fixedDeltaTime;
        if (m_LifeTimer >= m_Lifespan)
            ReleaseObject();
    }

    private void CheckScreenBoundary()
    {
        if (m_Cam == null) return;

        Vector3 vp = m_Cam.WorldToViewportPoint(transform.position);
        bool isInScreen = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f && vp.z > 0f;

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
        Vector2 offset = (Vector2)transform.position - m_FanCenter;
        float radius = offset.magnitude;
        if (radius < 0.01f)
        {
            m_Dir = Vector2.right;
            return;
        }
        Vector2 radial = offset / radius;
        Vector2 tangent = new Vector2(-radial.y, radial.x);
        float omega = m_FanAngularSpeedDeg * Mathf.Deg2Rad;
        float v = omega * radius;
        m_Dir = tangent * v;
        m_Rb.linearVelocity = m_Dir;
    }

    private void FireSpread()
    {
        Vector2 right = Quaternion.Euler(0, 0, 90) * m_Dir;
        Vector2 left = Quaternion.Euler(0, 0, -90) * m_Dir;
    }

    public void SetId(ushort _id)
    {
        if (_id > 0)
            m_BulletId = _id;
    }

    public void SetPattern(BulletPattern pattern, Vector2 dir, float levelSpeed)
    {
        m_Dir = dir.normalized;
        m_LevelSpeed = levelSpeed;
        m_Pattern = pattern;
        m_PlayerTransform = null;

        switch (pattern)
        {
            case BulletPattern.Normal:
                break;
            case BulletPattern.Spread:
                m_SpreadTimer = 0f;
                break;
            case BulletPattern.Fan:
                float baseAngularDeg = 45f + levelSpeed;
                Vector2 offset = (Vector2)transform.position - Vector2.zero;
                Vector2 dirToZero = Vector2.zero - (Vector2)transform.position;
                float signed = Vector2.SignedAngle(offset.normalized, dirToZero.normalized);
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
