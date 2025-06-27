using Fusion;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;

public class GameManager : NetworkBehaviour
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

    [SerializeField] private int ScoreTail = 0;
    [SerializeField] private int ScoreHead = 0;

    [SerializeField] private NetworkObject m_MyPlayer;

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

    public NetworkObject SpawnPlayer(ref Action<PlayerInputBase> _act)
    {
        if (m_State == GameState.Ready && m_MyPlayer == null)
        {
            m_MyPlayer = SpawnManager.Instance.SpawnPlayer(Runner.LocalPlayer);
            _act = m_MyPlayer.GetComponent<PlayerMovementController>().Move;
            GameStart();
            return m_MyPlayer;
        }
        return m_MyPlayer;
    }

    private void GameStart()
    {
        m_State = GameState.Play;
        m_ReadyText.SetActive(false);
        // 테스트 패턴 시작
        BulletSpawner.Instance.RunPattern(BulletPattern.Spread);
        BulletSpawner.Instance.RunPattern(BulletPattern.Winder);
    }

    /// <summary>
    /// 게임 레벨 향상 함수
    /// </summary>
    // 여기서 업데이트를 하지않고 [Networked] 를 걸면 게임오버시 에러 사출 위험
    [Rpc(sources: RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcUpdateGameLevel(float _value)
    {
        GameLevel = _value;
    }
}
