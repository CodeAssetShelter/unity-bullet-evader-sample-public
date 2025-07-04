using Fusion;
using System.Collections;
using UnityEngine;

public interface BaseActions
{
    public void Hit();
    public void Destroy();
}

public class SpaceshipController : NetworkBehaviour, BaseActions, ISpawned, IGameDestroyPlayer
{
    [SerializeField] private SpriteRenderer m_AircraftSpr;
    [SerializeField] private CircleCollider2D m_Collider;
    [SerializeField] Rigidbody2D m_Rigidbody;

    [SerializeField] public bool m_IsAlive = false;
    [SerializeField] private bool m_IsMine = false;

    private MasterInputController m_MIC;

    private void OnEnable()
    {
        ActiveInvincible();
    }

    public override void Spawned()
    {
        base.Spawned();

        if (Object.HasInputAuthority)
        {
            Runner.SetIsSimulated(Object, true);
            m_MIC = Runner.GetPlayerObject(Runner.LocalPlayer).GetComponent<MasterInputController>();
            m_MIC.RegisterPlayer(gameObject);

            GameManager.Instance.ConnectPlayer(Object);
            m_IsMine = true;
        }
        else
        {
            m_IsMine = false;
            m_Collider.enabled = false;
        }

        m_IsAlive = true;
    }

    public void SetAircraft(Sprite _spr)
    {
        m_AircraftSpr.sprite = _spr;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!Object.HasInputAuthority) return;
        Debug.Log($"{collision.gameObject.tag} IN");
        if (collision.CompareTag("Bullet") && m_IsAlive)
        {
            m_IsAlive = false;
            Hit();
        }
    }
    public void Hit()
    {
        // 플레이어가 체력을 가지고 있다면, 아래에서 조건분기 작성
        Destroy();
    }

    public void Destroy()
    {
        GameManager.Instance.Life--;
        m_Collider.enabled = false;
        m_Collider.gameObject.SetActive(false);
        m_IsAlive = false;
        m_MIC.DestroyPlayer(Runner.LocalPlayer);
    }

    public void ActiveInvincible()
    {
        StartCoroutine(CorActiveInvincible());
    }

    IEnumerator CorActiveInvincible()
    {
        if (!m_AircraftSpr) yield break;

        float interval = 0.1f;
        float timer = 0f;
        float duration = 4.5f;
        bool on = true;
        // WaitForSeconds 캐싱 → GC Zero
        var wait = new WaitForSeconds(interval);

        m_Collider.enabled = false;

        while (timer < duration)
        {
            on = !on;
            m_AircraftSpr.enabled = on;

            yield return wait;
            timer += interval;
        }

        m_AircraftSpr.enabled = true;            // 종료 시 원상 복구
        m_Collider.enabled = true;
    }

    public void DestroyPlayer(PlayerRef _playerRef)
    {
        StartCoroutine(CorDestroyAnimation());
    }

    //public void PlayDestroyAnim()
    //{
    //    StartCoroutine(CorDestroyAnimation());
    //}

    IEnumerator CorDestroyAnimation()
    {
        float timeStamp = 0;
        var wait = new WaitForFixedUpdate();

        m_Rigidbody.linearVelocity = (Vector2.right + Vector2.down) * 0.2f;

        // 무언가 처리하고 싶은게 있다면 여기서
        while (timeStamp < 2.0f)
        {
            timeStamp += Time.fixedDeltaTime;
            m_AircraftSpr.color = Color.Lerp(Color.white, Color.clear, timeStamp * 0.5f);
            yield return wait;
        }
        m_AircraftSpr.color = Color.clear;

        GameManager.Instance.Reborn();
        gameObject.SetActive(false);
        ResetPlayerState();
    }

    public void ResetPlayerState()
    {
        m_Rigidbody.transform.position = Vector3.zero;
        m_Rigidbody.linearVelocity = Vector2.zero;
        m_AircraftSpr.color = Color.white;
        m_Collider.gameObject.SetActive(true);

        if (m_IsMine)
        {
            m_Collider.enabled = true;
            m_IsAlive = true;
        }
    }
}
