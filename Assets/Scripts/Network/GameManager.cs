using Fusion;
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;
using System.Collections;
using System.Runtime.InteropServices.WindowsRuntime;

public class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static GameManager Instance;

    public enum GameState
    {
        Ready = 0,
        Play,
        GameOver,
        StateCount
    }

    public GameState m_State = GameState.Ready;

    /// <summary>
    /// 1이 기본값
    /// </summary>
    public float GameLevel { get; private set; } = 1;

    // *――――― 플레이 관련 전역변수 ―――――――――――――――
    [SerializeField] private GameObject m_ReadyText;

    private const float m_LevelUpInterval = 30f;
    private float m_LevelUpTimeStamp = 0;

    private const float m_DifficultyPlus = 0.3f;

    [Space(10)]
    [SerializeField] private SpawnManager m_SpawnManager;

    [Space(10)]
    [SerializeField] private int ScoreTail = 0;
    [SerializeField] private int ScoreHead = 0;

    [Space(10)]
    [SerializeField] private int m_Life = 3;
    public int Life { get { return m_Life; } set { m_Life = Mathf.Max(0, value); } }

    [Space(10)]
    [SerializeField] private NetworkObject m_MyPlayer;

    public Dictionary<PlayerRef, Transform> m_PlayerList = new();

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("GameManager Spawned()");
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        GamePlay();
    }

    private void GamePlay()
    {
        //GetInput() 은 다른 유저가 아닌 내 입력권한만 검사
        switch (m_State)
        {
            case GameState.Ready:
                if (m_PlayerList.Any())
                {
                    GameStart();
                }
                break;
            case GameState.Play:
                if (m_LevelUpTimeStamp > m_LevelUpInterval)
                {
                    m_LevelUpTimeStamp = 0;
                    RpcUpdateGameLevel(Mathf.Clamp(GameLevel + m_DifficultyPlus, 1, 5));
                }
                ScoreTail++;
                break;
            case GameState.GameOver:
                break;
            case GameState.StateCount:
                break;
            default:
                break;
        }
    }

    public void SpawnPlayer()
    {
        if (m_MyPlayer == null && Life > 0)
        {
            m_SpawnManager.RpcRequestSpawnPlayer(Runner.LocalPlayer);
        }
    }

    public void ConnectPlayer(NetworkObject _obj)
    {
        m_MyPlayer = _obj;
        if (m_MyPlayer == null) return;
        if (m_MyPlayer.GetComponent<PlayerMovementController>() == null) {

            Debug.LogError(m_MyPlayer.name);
        }
        Debug.Log($"{Runner.LocalPlayer} Connected");
        m_PlayerList.Add(Runner.LocalPlayer, m_MyPlayer.transform);
    }

    private void GameStart()
    {
        m_State = GameState.Play;
        m_ReadyText.SetActive(false);
        // 테스트 패턴 시작
        BulletSpawner.Instance.RunPattern(BulletPattern.Normal);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Spread);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Winder);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Cage);
    }

    public void DestroyPlayerAnim(PlayerRef _playerRef)
    {
        m_SpawnManager.PlayDestroyAnim(_playerRef);
    }

    public Transform GetRandomPlayerTransform()
    {
        if (m_PlayerList.Count == 0) return null;
        var res = m_PlayerList.FirstOrDefault();
        return res.Value != null ? res.Value : null;
    }

    public void Reborn()
    {
        if (m_State == GameState.Play && Life > 0 && m_CoReborn == null)
        {
            m_CoReborn = StartCoroutine(CorReborn());
        }
    }
    Coroutine m_CoReborn;
    IEnumerator CorReborn()
    {
        Debug.Log("Reborn");
        yield return new WaitForSeconds(3.5f);
        m_MyPlayer.gameObject.SetActive(true);
        m_CoReborn = null;
    }

    #region ---------- R P C --------------------------------------
    /// <summary>
    /// 게임 레벨 향상 함수
    /// </summary>
    // 여기서 업데이트를 하지않고 [Networked] 를 걸면 게임오버시 에러 사출 위험
    [Rpc(sources: RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcUpdateGameLevel(float _value)
    {
        GameLevel = _value;
    }
    #endregion

    public void PlayerJoined(PlayerRef player)
    {
        var networkObject = m_SpawnManager.SpawnPlayerMasterController(player);
    }

    public void PlayerLeft(PlayerRef player)
    {
        m_PlayerList.Remove(player);
    }

    public NetworkRunner GetRunner()
    {
        return Runner != null ? Runner : null;
    }
}
