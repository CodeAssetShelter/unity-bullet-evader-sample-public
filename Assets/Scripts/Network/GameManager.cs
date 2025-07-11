using Fusion;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Input = UnityEngine.Input;
using UnityEngine.UIElements;
using Fusion.Sockets;
using Random = UnityEngine.Random;
using NUnit.Framework;
using Unity.VisualScripting;
using TMPro;

public class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static GameManager Instance;

    public enum GameState
    {
        Ready = 0,
        Play,
        GameOverAll,
        StateCount
    }

    public GameState m_State = GameState.Ready;

    /// <summary>
    /// 1이 기본값
    /// </summary>
    [Networked] public float GameLevel { get; private set; } = 1;

    // *――――― 플레이 관련 전역변수 ―――――――――――――――
    [Space(10)]
    [SerializeField] private GameObject m_ReadyText;
    [SerializeField] private GameObject m_GameOverText;
    [SerializeField] private GameObject m_GameOverDetailText;
    [SerializeField] private GameObject m_ObserverModeText;
    [SerializeField] private TextMeshProUGUI m_ScoreHeadText;
    [SerializeField] private TextMeshProUGUI m_ScoreTailText;

    private const float m_LevelUpInterval = 30f;
    private float m_LevelUpTimeStamp = 0;

    private const float m_DifficultyPlus = 0.3f;

    [Space(10)]
    [SerializeField] private SpawnManager m_SpawnManager;

    [Space(10)]
    [SerializeField] private int m_ScoreHead = 0;
    [SerializeField] private int m_ScoreTail = 0;
    private const int SCORE_HEAD_MAX = 1000000;
    private const int SCORE_TAIL_MAX = 1000000;
    private const string SCORE_FORMAT = "{0}";

    //[Space(10)]
    //[SerializeField] private int m_Life = 3;
    //public int Life { get { return m_Life; } set { m_Life = Mathf.Max(0, value); } }

    [Space(10)]
    [SerializeField] private NetworkObject m_MyPlayer;

    public Dictionary<PlayerRef, Transform> m_PlayerList = new();
    public List<Transform> m_PlayerTransformList = new();

    private readonly System.Random m_Rng = new();

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

    #region *---------- Main Logic -----------------------------
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
                    RpcUpdateGameLevel(Mathf.Clamp(GetGameLevel() + m_DifficultyPlus, 1, 5));
                }
                m_ScoreTail++;
                break;
            case GameState.GameOverAll:
                break;
            case GameState.StateCount:
                break;
            default:
                break;
        }
    }

    public void UpdateScore()
    {
        if (m_State != GameState.Play) return;

        m_ScoreTail++;

        if (m_ScoreTail >= SCORE_TAIL_MAX)
        {
            m_ScoreTail = 0;
            m_ScoreHead += (int)(1 * GameLevel);
            m_ScoreHeadText.SetText(SCORE_FORMAT, m_ScoreHead);
        }
        m_ScoreTailText.SetText(SCORE_FORMAT, m_ScoreTail);
    }

    public void ActiveGameOverUI()
    {
        m_GameOverText.SetActive(true);
        m_GameOverDetailText.SetActive(true);

        if (m_CoWaitObserber != null) StopCoroutine(m_CoWaitObserber);
        m_CoWaitObserber = StartCoroutine(CorWaitObserber());
    }

    #endregion
    public void SpawnPlayer()
    {
        if (m_MyPlayer != null) return;
        m_SpawnManager.RpcRequestSpawnPlayer(Runner.LocalPlayer);
    }

    public void AddPlayer(PlayerRef _playerRef, Transform _transform)
    {
        m_PlayerList[_playerRef] = _transform;
        m_PlayerTransformList = m_PlayerList.Values.ToList();
    }

    public Sprite GetAircraftSprite(int _idx) 
    {
        var sprList = m_SpawnManager.m_AircraftSprites;
        return sprList[_idx % sprList.Count];
    }

    public void GetExplosionEffect(Vector2 _pivot, float _boundX = 0, float _boundY = 0)
    {
        float x = Random.Range(_boundX, _boundX * -1);
        float y = Random.Range(_boundY, _boundY * -1);
        _pivot.x += x; _pivot.y += y;
        LocalObjectPool.Instance.Get(PoolKey.EXPLOSION, _pivot, Quaternion.identity);
    }

    private void GameStart()
    {
        m_State = GameState.Play;
        // 테스트 패턴 시작
        BulletSpawner.Instance.RunPattern(BulletPattern.Normal);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Spread);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Winder);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Cage);
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

    [Rpc(sources: RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcTestRequest(PlayerRef _player)
    {
        Debug.Log($"Request by {_player}!");
        RpcTestResponse(Runner.LocalPlayer);
    }

    [Rpc(sources: RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcTestResponse(PlayerRef _player)
    {
        Debug.Log($"I'm {_player} : Response All!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcInitWaitGame(PlayerRef _player)
    {
        if (Runner.LocalPlayer == _player && m_CoWaitStart == null)
        {
            m_CoWaitStart = StartCoroutine(CorWaitStart());
        }
    }
    #endregion



    Coroutine m_CoWaitStart;
    IEnumerator CorWaitStart()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_CoWaitStart = null;
                m_ReadyText.SetActive(false);
                m_ScoreHeadText.gameObject.SetActive(true);
                m_ScoreTailText.gameObject.SetActive(true);
                SpawnPlayer();
                yield break;
            }
            yield return null;
        }
    }

    Coroutine m_CoWaitObserber;
    IEnumerator CorWaitObserber()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_CoWaitObserber = null;
                m_ObserverModeText.SetActive(true);
                m_GameOverText.SetActive(false);
                m_GameOverDetailText.SetActive(false);
                yield break;
            }
            yield return null;
        }
    }

    #region *----- Network Interfaces -------------------------------
    public void PlayerJoined(PlayerRef player)
    {
        //var networkObject = m_SpawnManager.SpawnPlayerMasterController(player);
        //GameManager.Instance.RpcTestRequest(player);
        Debug.Log($"Joined - {player}");
        RpcInitWaitGame(player);
    }
    public void PlayerLeft(PlayerRef player)
    {
        Debug.Log("Some Left - " + player);
        if (Runner.IsServer)
            Runner.Despawn(m_PlayerList[player].GetComponent<NetworkObject>());
        m_PlayerList.Remove(player);
        m_PlayerTransformList = m_PlayerList.Values.ToList();
    }

    #endregion

    #region *------- Utils -----------------------------------
    public NetworkRunner GetRunner()
    {
        return Runner != null ? Runner : null;
    }

    public float GetGameLevel()
    {
        if (Object.IsValid)
            return GameLevel;
        return 1;
    }

    public Transform GetRandomPlayerTransform()
    {
        int count = m_PlayerTransformList.Count;
        if (count == 0)       // 리스트가 비어 있으면 즉시 반환
            return null;

        int start = m_Rng.Next(count);   // 무작위 시작 인덱스
        for (int i = 0; i < count; ++i)
        {
            int idx = (start + i) % count;             // 1회 순환
            Transform t = m_PlayerTransformList[idx];
            if (t != null && t.gameObject.activeSelf)  // 활성 GO만 통과
                return t;
        }
        return null;   // 모든 항목이 비활성 / null 인 경우
    }

    public Vector2 GetRandomPlayerPosition()
    {
        var res = GetRandomPlayerTransform();
        return res == null ? Vector2.zero : res.position;
    }

    public SpaceshipController GetPlayer(PlayerRef _playerRef)
    {
        // 이미 캐시돼 있으면 바로 반환 ─ O(1)
        if (m_PlayerList.TryGetValue(_playerRef, out var cachedTr))
            return cachedTr.GetComponent<SpaceshipController>();

        // Runner 에게서 플레이어 오브젝트를 얻어-온 뒤 캐시에 저장
        if (Runner.TryGetPlayerObject(_playerRef, out var obj))
        {
            var tr = obj.transform;
            m_PlayerList[_playerRef] = tr;          // 캐싱
            return tr.GetComponent<SpaceshipController>();
        }

        // 아직 스폰되지 않았거나 룸에 없음
        return null;
    }
    #endregion
}
