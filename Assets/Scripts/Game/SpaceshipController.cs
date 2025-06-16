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
        // �÷��̾ ü���� ������ �ִٸ�, �Ʒ����� ���Ǻб� �ۼ�
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

        // ���� ó���ϰ� ������ �ִٸ� ���⼭
        while (timeStamp < 2.0f)
        {
            timeStamp += Time.fixedDeltaTime;
            m_AircraftSpr.color = Color.Lerp(Color.white, Color.clear, timeStamp * 0.5f);
            yield return wait;
        }
        m_AircraftSpr.color = Color.clear;
        gameObject.SetActive(false);

        ResetPlayerState();

        // �켱 ���⼭ ����
        // ���Ŀ� �ִϸ��̼� ó���� �ϴ��� ���̱׷��̼� �ϴ���...
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
