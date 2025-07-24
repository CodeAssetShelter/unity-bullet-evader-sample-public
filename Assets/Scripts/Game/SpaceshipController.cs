using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;


public class SpaceshipController : NetworkBehaviour, ISpawned, IAfterSpawned
{
    [Networked] public int Life { get; set; } = 3;

    [SerializeField] private SpriteRenderer m_AircraftSpr;
    [SerializeField] private List<SpriteRenderer> m_SpriteList;
    [SerializeField] private CircleCollider2D m_Collider;
    [SerializeField] Rigidbody2D m_Rigidbody;

    [Networked] public NetworkBool m_Hide { get; set; } = false;
    [Networked] public NetworkBool m_Invincible { get; set; } = false;
    [Networked] public NetworkBool m_Destroying { get; set; } = false;


    // *-------- Timer ----------------------------
    [Networked] private TickTimer m_InvincibleTick { get; set; }
    private const float m_InvincibleTime = 4.5f;
    [Networked] private TickTimer m_RebornTick { get; set; }
    private const float m_RebornTime = 2.5f;


    [Networked] private int m_SprIdx { get; set; } = 0;

    [Networked] public NetworkBool m_IsAlive { get; set; } = false;
    [Networked] public NetworkBool m_CanControl { get; set; } = false;

    private ChangeDetector m_ChangeDetector;

    private Dictionary<string, object> m_ResumeData;

    public override void Spawned()
    {
        base.Spawned();

        // *-------- Init -----------------------------
        m_ChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // *-------- Settings -------------------------
        m_AircraftSpr.sprite = GameManager.Instance.GetAircraftSprite(m_SprIdx);

        // *-------- Debug ----------------------------
        // if (Object.HasStateAuthority)
            // Life = 1;

        // *-------- Init -----------------------------
        if (Object.HasInputAuthority)
        {
            Debug.Log($"{Object.InputAuthority} SPAWN");
            Runner.SetIsSimulated(Object, true);
        }

        if (Object.HasStateAuthority && GameManager.Instance != null)
        {
            var res = GameManager.Instance.GetResumeData(Object.InputAuthority, Defines.MIG_PLAYER_LIFE);
            Life = res == null ? Life : (int)res;
        }

        m_IsAlive = true;
        m_Destroying = false;
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
                //case nameof(m_Hide):
                //    var reader_hide = GetPropertyReader<NetworkBool>(nameof(m_Hide));
                //    var (previous_hide, current_hide)= reader_hide.Read(previousBuffer, currentBuffer);
                //    ToggleHide(previous_hide, current_hide);
                //    break;
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
                case nameof(m_Destroying):
                    var reader_destroy = GetPropertyReader<NetworkBool>(nameof(m_Destroying));
                    var (previous_destroy, current_destroy) = reader_destroy.Read(previousBuffer, currentBuffer);
                    ToggleDestroyAnim(previous_destroy, current_destroy);
                    break;
            }
        }
    }

    private void FixedUpdate()
    {
        ToggleHide(m_Hide);
        UpdateScore();
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
        if (GameManager.Instance != null && m_CanControl && m_IsAlive && Runner != null)
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

    private void ToggleHide(bool _isCurrHide)
    {
        //if (_isCurrHide == _isPrevHide) return;

        if (m_Hide)
        {
            if (m_CoHideAnim != null) return;
            m_CoHideAnim = StartCoroutine(CorHideAnim());
            FadePlayer(0);
            m_Hide = _isCurrHide;
        }
        else
        {
            if (m_CoHideAnim != null)
                StopCoroutine(m_CoHideAnim);
            m_CoHideAnim = null;
            ToggleVisual(true);
        }
    }

    Coroutine m_CoHideAnim;
    IEnumerator CorHideAnim()
    {
        int interval = 4;
        int timeStamp = 0;
        bool on = false;
        Debug.Log($"{Object.InputAuthority} Hide Anim IN");
        while (true)
        {
            timeStamp++;
            if (timeStamp > interval)
            {
                on = !on;
                ToggleVisual(on);
                timeStamp = 0;
            }
            yield return null;
        }
    }


    private void ToggleVisual(bool _isShow)
    {
        // 오늘 고칠거
        m_AircraftSpr.enabled = _isShow;
        m_SpriteList.ForEach(x => x.enabled = _isShow);
    }


    private void ToggleDestroyAnim(bool _prevBool, bool _currBool)
    {
        if (_prevBool == _currBool)
            return;

        if (_currBool)
        {
            if (m_CoActiveDestroyAnim != null) StopCoroutine(CorActiveDestroyAnim());
            m_CoActiveDestroyAnim = StartCoroutine(CorActiveDestroyAnim());
        }
    }

    Coroutine m_CoActiveDestroyAnim;
    IEnumerator CorActiveDestroyAnim()
    {
        if (!m_Destroying) yield break;
        float delay = Random.Range(0.1f, 0.15f);
        float timeStamp = 0;
        var wait = new WaitForSeconds(delay);

        while (m_Destroying || timeStamp < m_RebornTime)
        {
            if (gameObject == null)
            {
                yield break;
            }
            float t = timeStamp / m_RebornTime;

            GameManager.Instance.GetExplosionEffect(transform.position, 0.1f, 0.1f);
            FadePlayer(t);
            timeStamp += delay;
            yield return wait;
        }
    }


    private void ActiveDestroyBehavior(PlayerRef _playerRef)
    {
        StartCoroutine(CorActiveDestroyBehavior(_playerRef));
    }
    IEnumerator CorActiveDestroyBehavior(PlayerRef _playerRef)
    {
        // 아예 로컬에서 전부 처리하고 싶다면 이쪽으로
        // 중간 입장시 싱크 X
        const float playTime = 2.5f;
        float timeStamp = 0;
        float delay = Random.Range(0.1f, 0.15f);
        var wait = new WaitForSeconds(delay);

        ActiveInvincible(false, false);
        SetConstVelocity(new Vector2(0.2f, -0.2f));

        //Transform transform = GameManager.Instance.GetPlayer(_playerRef).transform;

        if (Object.HasStateAuthority)
        {
            m_Destroying = true;
            m_CanControl = false;
        }

        while (timeStamp < playTime)
        {
            if (gameObject == null)
            {
                yield break;
            }
            timeStamp += Time.deltaTime;
            yield return null;
        }

        SetConstVelocity(Vector2.zero);


        if (HasStateAuthority)
        {
            m_Destroying = false;
            if (Life <= 0)
            {
                yield return new WaitForSeconds(2.0f);
                GameManager.Instance.AddGameUserCount();
                RpcYouAreGameOver(Object.InputAuthority);
            }

            m_IsAlive = false;
            if (Life > 0)
            {
                m_RebornTick = TickTimer.CreateFromSeconds(Runner, m_RebornTime);
            }
        }
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
        if (Object.InputAuthority == _playerRef)
        {
            Life = Mathf.Clamp(--Life, 0, 3);
            ActiveDestroyBehavior(_playerRef);
        }
        Debug.Log($"{Object.InputAuthority} : Call Rpc Invincible - {_playerRef}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RpcYouAreGameOver(PlayerRef _playerRef)
    {
        if (Object.InputAuthority == _playerRef)
        {
            GameManager.Instance.ActiveGameOverUI();
        }
    }
    #endregion
}
