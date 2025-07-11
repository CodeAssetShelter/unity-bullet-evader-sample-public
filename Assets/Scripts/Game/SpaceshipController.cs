using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class SpaceshipController : NetworkBehaviour, ISpawned, IAfterSpawned
{
    [Networked] public int Life { get; set; } = 3;

    [SerializeField] private SpriteRenderer m_AircraftSpr;
    [SerializeField] private List<SpriteRenderer> m_SpriteList;
    [SerializeField] private CircleCollider2D m_Collider;
    [SerializeField] Rigidbody2D m_Rigidbody;

    [Networked] public NetworkBool m_Hide { get; set; } = false;
    [Networked] public NetworkBool m_Invincible { get; set; } = false;


    // *-------- Timer ----------------------------
    [Networked] private TickTimer m_InvincibleTick { get; set; }
    private const float m_InvincibleTime = 4.5f;
    [Networked] private TickTimer m_RebornTick { get; set; }
    private const float m_RebornTime = 2.5f;

    [Networked] private int m_SprIdx { get; set; } = 0;

    [Networked] public NetworkBool m_IsAlive { get; set; } = false;
    [Networked] public NetworkBool m_CanControl { get; set; } = false;

    private ChangeDetector m_ChangeDetector;

    public override void Spawned()
    {
        base.Spawned();

        // *-------- Init -----------------------------
        m_ChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // *-------- Settings -------------------------
        m_AircraftSpr.sprite = GameManager.Instance.GetAircraftSprite(m_SprIdx);
        if (Object.HasInputAuthority)
        {
            Debug.Log($"{Object.InputAuthority} SPAWN");
            Runner.SetIsSimulated(Object, true);
        }

        m_IsAlive = true;
    }

    public void AfterSpawned()
    {
        if (Object.HasInputAuthority)
        {
            m_IsAlive = true;
            RpcActiveInvincible(Object.InputAuthority);
        }
    }

    public override void Render()
    {
        foreach (var change in m_ChangeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
        {
            switch (change)
            {
                case nameof(m_Hide):
                    var reader_hide = GetPropertyReader<NetworkBool>(nameof(m_Hide));
                    var (previous_hide, current_hide)= reader_hide.Read(previousBuffer, currentBuffer);
                    ToggleHide(previous_hide, current_hide);
                    break;
                case nameof(m_Invincible):
                    var reader_invincible = GetPropertyReader<NetworkBool>(nameof(m_Invincible));
                    var (previous_invin, current_invin) = reader_invincible.Read(previousBuffer, currentBuffer);
                    ActiveInvincible(current_invin);
                    break;
                case nameof(m_IsAlive):
                    var reader_alive = GetPropertyReader<NetworkBool>(nameof(m_IsAlive));
                    var (previous_alive, current_alive) = reader_alive.Read(previousBuffer, currentBuffer);
                    ToggleVisual(current_alive);
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        
        if (m_InvincibleTick.Expired(Runner))
        {
            Debug.Log($"{Object.InputAuthority} : Reborn - {Object.InputAuthority}");
            m_InvincibleTick = default;
            m_Invincible = false;
        }
        if (m_RebornTick.Expired(Runner))
        {
            RpcActiveInvincible(Object.InputAuthority);
            FadePlayer(0);
            m_IsAlive = true;
            m_RebornTick = default;
        }
    }

    private void UpdateScore()
    {
        if (m_CanControl && m_IsAlive)
        {
            GameManager.Instance.UpdateScore();
        }
    }

    private void ActiveInvincible(bool _isOn, bool _isActiveTimer = true)
    {
        if (!Runner.IsServer) return;
        m_Invincible = _isOn;
        m_Hide = _isOn;
        m_Collider.enabled = !m_Invincible;

        if (!_isActiveTimer) return;
        if (m_InvincibleTick.ExpiredOrNotRunning(Runner) && m_Invincible)
        {
            Debug.Log($"Start Reborn - {Object.InputAuthority}");
            m_InvincibleTick = TickTimer.CreateFromSeconds(Runner, m_InvincibleTime);
        }
    }

    private void ToggleHide(bool _isPrevHide, bool _isCurrHide)
    {
        if (_isCurrHide == _isPrevHide) return;
        m_Hide = _isCurrHide;
        
        if (m_CoHideAnim != null) StopCoroutine(m_CoHideAnim);
        if (m_Hide)
        {
            m_CoHideAnim = StartCoroutine(CorHideAnim());
        }
        else
        {
            ToggleVisual(true);
        }
    }

    Coroutine m_CoHideAnim;
    IEnumerator CorHideAnim()
    {
        var wait = new WaitForSeconds(0.1f);
        bool on = false;
        while (true)
        {
            on = !on;
            ToggleVisual(on);
            yield return wait;
        }
    }


    private void ToggleVisual(bool _isShow)
    {
        m_AircraftSpr.enabled = _isShow;
        m_SpriteList.ForEach(x => x.enabled = _isShow);
    }


    private void PlayDestroyAnim(PlayerRef _playerRef)
    {
        StartCoroutine(CorPlayDestroyAnim(_playerRef));
    }
    IEnumerator CorPlayDestroyAnim(PlayerRef _playerRef)
    {
        const float playTime = 2.5f;
        float timeStamp = 0;
        float delay = UnityEngine.Random.Range(0.1f, 0.15f);
        var wait = new WaitForSeconds(delay);

        ActiveInvincible(false, false);
        SetConstVelocity(new Vector2(0.2f, -0.2f));

        Transform transform = GameManager.Instance.GetPlayer(_playerRef).transform;

        if (Object.HasStateAuthority)
            m_CanControl = false;

        while (timeStamp < playTime)
        {
            if (transform == null)
            {
                yield break;
            }

            float t = timeStamp / playTime;

            FadePlayer(t);
            GameManager.Instance.GetExplosionEffect(transform.position, 0.1f, 0.1f);
            timeStamp += delay;
            yield return wait;
        }

        SetConstVelocity(Vector2.zero);
        FadePlayer(1);

        if (Life <= 0 && Object.HasInputAuthority)
        {
            yield return new WaitForSeconds(2.0f);
            GameManager.Instance.ActiveGameOverUI();
        }

        if (Object.HasStateAuthority)
        {
            m_IsAlive = false;
            if (Life > 0)
            {
                m_RebornTick = TickTimer.CreateFromSeconds(Runner, m_RebornTime);
            }
        }

        // 활성화 안해도 될듯
        // 어차피 부활 무적 타이밍 이후 자동 활성화
        //ActiveInvincible(true, false);
    }

    private void FadePlayer(float _t)
    {
        m_AircraftSpr.color = Color.Lerp(Color.white, Color.clear, _t);
        m_SpriteList[0].color = Color.Lerp(Color.red, Color.clear, _t);
        m_SpriteList[1].color = Color.Lerp(Color.white, Color.clear, _t);
    }

    public void SetAircraft(int _Idx)
    {
        if (Object.HasStateAuthority)
            m_SprIdx = _Idx;
    }

    private void SetConstVelocity(Vector2 _vel)
    {
        if (!Runner.IsServer) return;
        m_Rigidbody.linearVelocity = _vel;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!Object.HasInputAuthority) return;
        if (collision.CompareTag("Bullet") && m_IsAlive && m_CanControl)
        {
            //m_IsAlive = false;
            //RpcActiveInvincible(Object.InputAuthority);
            RpcHit(Object.InputAuthority);
        }
    }


    #region *---------- RPC ---------------------------------
    [Rpc (RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcActiveInvincible(PlayerRef _playerRef)
    {
        if (!Runner.IsServer) return;
        if (Object.InputAuthority == _playerRef)
        {
            m_CanControl = true;
            ActiveInvincible(true);
        }
        Debug.Log($"{Object.InputAuthority} : Call Rpc Invincible - {_playerRef}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcHit(PlayerRef _playerRef)
    {
        if (!Runner.IsServer) return;
        if (Object.InputAuthority == _playerRef)
        {
            Life = Mathf.Clamp(--Life, 0, 3);
            PlayDestroyAnim(_playerRef);
        }
        Debug.Log($"{Object.InputAuthority} : Call Rpc Invincible - {_playerRef}");
    }
    #endregion
}
