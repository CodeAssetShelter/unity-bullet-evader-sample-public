using Fusion;
using System.Collections;
using UnityEngine;

public interface BaseActions
{
    public void Hit();
    public void Destroy();
}

public class SpaceshipController : NetworkBehaviour, BaseActions, ISpawned
{
    [SerializeField] private SpriteRenderer m_AircraftSpr;
    [SerializeField] private CircleCollider2D m_Collider;
    [SerializeField] Rigidbody2D m_Rigidbody;

    [SerializeField] public bool m_IsAlive = false;
    
    public override void Spawned()
    {
        base.Spawned();
        m_IsAlive = true;
    }

    public void SetAircraft(Sprite _spr)
    {
        m_AircraftSpr.sprite = _spr;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"{collision.gameObject.tag} IN");
        if (collision.CompareTag("Bullet"))
        {
            Debug.Log("Hit");
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
        m_Collider.enabled = false;
        m_Collider.gameObject.SetActive(false);
        m_IsAlive = false;
        SpawnManager.Instance.RPC_DestroyAnim(Runner.LocalPlayer);
    }


    public void PlayDestroyAnim()
    {
        StartCoroutine(CorDestroyAnimation());
    }

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
        gameObject.SetActive(false);

        ResetPlayerState();

        // 우선 여기서 종료
        // 이후에 애니메이션 처리를 하던지 마이그레이션 하던지...
        Runner.Shutdown();
    }

    public void ResetPlayerState()
    {
        m_Rigidbody.linearVelocity = Vector2.zero;
        m_AircraftSpr.color = Color.white;
        m_Collider.gameObject.SetActive(true);
        m_Collider.enabled = true;
    }
}
