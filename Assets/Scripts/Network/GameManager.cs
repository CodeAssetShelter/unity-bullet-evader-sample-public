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
using System.Runtime.CompilerServices;
using static Fusion.Editor.FusionHubWindow;

public class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft, IAfterSpawned
{
    public static GameManager Instance;

    public enum GameState
    {
        Ready = 0,
        ReadyMultiplay,
        Play,
        GameOverAll,
        StateCount
    }

    [Networked] public GameState m_State { get; set; } = GameState.Ready;

    /// <summary>
    /// 1이 기본값
    /// </summary>
    [Networked] public float GameLevel { get; private set; } = 1;
    [Networked] public int ReadyPlayerCount { get; set; } = 0;


    // *――――― 플레이 관련 전역변수 ―――――――――――――――
    [Header("- UI Text")]
    [SerializeField] private TextMeshProUGUI m_ReadyText;
    [SerializeField] private TextMeshProUGUI m_ReadyMultiText;
    [SerializeField] private TextMeshProUGUI m_ReadyMultiGetReadyText;
    [SerializeField] private TextMeshProUGUI m_ReadyMultiPlayerCountText;
    [SerializeField] private TextMeshProUGUI m_MultiplayWaitForOthers;
    [SerializeField] private TextMeshProUGUI m_MultiplayNowAvailableText;

    [Space(5)]
    [SerializeField] private GameObject m_GameOverText;
    [SerializeField] private GameObject m_GameOverDetailText;
    [Space(5)]
    [SerializeField] private GameObject m_ObserverModeText;
    [Space(5)]
    [SerializeField] private TextMeshProUGUI m_ScoreHeadText;
    [SerializeField] private TextMeshProUGUI m_ScoreTailText;

    private const float m_LevelUpInterval = 30f;
    private float m_LevelUpTimeStamp = 0;

    private const float m_DifficultyPlus = 0.3f;

    [Space(20)]
    [SerializeField] private SpawnManager m_SpawnManager;

    [Space(20)]
    [SerializeField] private int m_ScoreHead = 0;
    [SerializeField] private int m_ScoreTail = 0;
    private const int SCORE_HEAD_MAX = 1000000;
    private const int SCORE_TAIL_MAX = 1000000;
    private const string SCORE_FORMAT = "{0}";
    private const string READYCOUNT_FORMAT = "{0}/{1}";

    [Space(20)]
    [SerializeField] private NetworkObject m_MyPlayer;

    public Dictionary<PlayerRef, Transform> m_PlayerList = new();
    public Dictionary<PlayerRef, bool> m_ReadyPlayerList = new();
    public List<Transform> m_PlayerTransformList = new();

    private bool m_NowPlaying = false;

    private readonly System.Random m_Rng = new();

    private ChangeDetector m_ChangeDetector;
    public override void Spawned()
    {
        base.Spawned();

        // *-------- Init -----------------------------
        m_ChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        InitScoreText();

        Debug.Log("GameManager Spawned()");
    }

    private void Start()
    {
        SoundManager.Instance.PreloadSound(SoundManager.SoundType.ExplosionSmall_000);
        SoundManager.Instance.PreloadSound(SoundManager.SoundType.ExplosionSmall_001);
        SoundManager.Instance.PreloadSound(SoundManager.SoundType.BGM_001);
    }
    public void AfterSpawned()
    {
        ShowActiveMultiplayWaitingUI(m_State);
    }

    private void ShowActiveMultiplayWaitingUI(GameState _state)
    {
        if (m_NowPlaying) return;

        m_ReadyText.gameObject.SetActive(_state == GameState.Ready || _state == GameState.Play);
        m_ReadyMultiText.gameObject.SetActive(_state == GameState.ReadyMultiplay);
        m_ReadyMultiGetReadyText.gameObject.SetActive(_state == GameState.ReadyMultiplay);
        m_ReadyMultiPlayerCountText.gameObject.SetActive(_state == GameState.ReadyMultiplay);
        RefreshReadyCountText();
    }
    public override void Render()
    {
        foreach (var change in m_ChangeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
        {
            switch (change)
            {
                case nameof(ReadyPlayerCount):
                    if (m_State != GameState.ReadyMultiplay) break;
                    var reader_count = GetPropertyReader<int>(nameof(ReadyPlayerCount));
                    var (previous_count, current_count) = reader_count.Read(previousBuffer, currentBuffer);
                    RefreshReadyPlayerCount(previous_count, current_count);
                    break;
            }
        }
    }

    private void RefreshReadyPlayerCount(int _prevCount, int _currCount)
    {
        if (_prevCount == _currCount) return;
        ReadyPlayerCount = Mathf.Clamp(0, _currCount, Runner.ActivePlayers.Count());
        RefreshReadyCountText();
    }

    private void RefreshReadyCountText()
    {
        m_ReadyMultiPlayerCountText.SetText(READYCOUNT_FORMAT, ReadyPlayerCount, Runner.ActivePlayers.Count());
        if (Runner.IsServer && ReadyPlayerCount >= Runner.ActivePlayers.Count())
        {
            m_MultiplayWaitForOthers.gameObject.SetActive(ReadyPlayerCount < Runner.ActivePlayers.Count());
            m_MultiplayNowAvailableText.gameObject.SetActive(ReadyPlayerCount >= Runner.ActivePlayers.Count());

            if (m_CoHostToPlayGame == null && m_State == GameState.ReadyMultiplay)
                m_CoHostToPlayGame = StartCoroutine(CorHostToPlayGame());
        }
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

    private void InitScoreText()
    {
        m_ScoreHeadText.SetText(SCORE_FORMAT, 0);
        m_ScoreTailText.SetText(SCORE_FORMAT, 0);
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
        m_NowPlaying = true;
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
        if (m_CoHostToPlayGame != null)
            StopCoroutine(m_CoHostToPlayGame);
        m_State = GameState.Play;

        SoundManager.Instance.PlayMusic(SoundManager.SoundType.BGM_001);
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcNowPrepared(PlayerRef _player)
    {
        if (Runner.IsServer)
        {
            ++ReadyPlayerCount;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcStartGameAll()
    {
        m_ReadyText.gameObject.SetActive(false);
        m_ReadyMultiText.gameObject.SetActive(false);
        m_MultiplayWaitForOthers.gameObject.SetActive(false);
        m_ReadyMultiGetReadyText.gameObject.SetActive(false);
        m_ScoreHeadText.gameObject.SetActive(true);
        m_ScoreTailText.gameObject.SetActive(true);

        if (Runner.IsServer)
        {
            foreach (var playerRef in Runner.ActivePlayers)
            {
                m_SpawnManager.SpawnPlayer(playerRef);
            }
            GameStart();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcInitWaitGame(PlayerRef _player)
    {
        if (Runner.LocalPlayer == _player && m_CoWaitforOthers == null)
        {
            if (m_State == GameState.Play)
                m_CoLateJoin = StartCoroutine(CorLateJoin());
            else
                m_CoWaitforOthers = StartCoroutine(CorWaitForOthers());
        }
    }
    #endregion



    Coroutine m_CoWaitforOthers;
    IEnumerator CorWaitForOthers()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_CoWaitforOthers = null;
                if (m_State == GameState.Ready || m_State == GameState.Play)
                {
                    m_ReadyText.gameObject.SetActive(false);
                    m_ScoreHeadText.gameObject.SetActive(true);
                    m_ScoreTailText.gameObject.SetActive(true);
                    SpawnPlayer();
                }
                else if (m_State == GameState.ReadyMultiplay)
                {
                    RpcNowPrepared(Runner.LocalPlayer);
                    m_ReadyText.gameObject.SetActive(false);
                    m_MultiplayWaitForOthers.gameObject.SetActive(true);
                    m_ReadyMultiGetReadyText.gameObject.SetActive(false);
                }
                yield break;
            }
            yield return null;
        }
    }

    Coroutine m_CoHostToPlayGame;
    IEnumerator CorHostToPlayGame()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space) && ReadyPlayerCount >= Runner.ActivePlayers.Count())
            {
                m_ReadyText.gameObject.SetActive(false);
                m_MultiplayWaitForOthers.gameObject.SetActive(false);
                m_ReadyMultiGetReadyText.gameObject.SetActive(false);
                m_ScoreHeadText.gameObject.SetActive(true);
                m_ScoreTailText.gameObject.SetActive(true);
                RpcStartGameAll();
            }
            yield return null;
        }
    }

    Coroutine m_CoLateJoin;
    IEnumerator CorLateJoin()
    {
        float timeStamp = 0;
        float waitTimeMax = 30f;

        void StartGame()
        {
            m_CoLateJoin = null;
            if (m_State == GameState.Play)
            {
                m_ReadyText.gameObject.SetActive(false);
                m_ScoreHeadText.gameObject.SetActive(true);
                m_ScoreTailText.gameObject.SetActive(true);
                SpawnPlayer();
                SoundManager.Instance.PlayMusic(SoundManager.SoundType.BGM_001);
            }
        }

        while (timeStamp < waitTimeMax)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                StartGame();
                yield break;
            }
            timeStamp += Time.deltaTime;
            yield return null;
        }

        StartGame();
    }

    Coroutine m_CoWaitObserber;
    IEnumerator CorWaitObserber()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_ObserverModeText.gameObject.SetActive(true);
                m_GameOverText.gameObject.SetActive(false);
                m_GameOverDetailText.gameObject.SetActive(false);
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
        m_ReadyPlayerList[player] = false;
        if (Runner.ActivePlayers.Count() > 1 && m_State == GameState.Ready)
        {
            m_State = GameState.ReadyMultiplay;
        }

        RpcInitWaitGame(player);
        ShowActiveMultiplayWaitingUI(m_State);
    }

    public void PlayerLeft(PlayerRef player)
    {
        Debug.Log("Some Left - " + player);
        if (Runner.IsServer && m_PlayerList.TryGetValue(player, out var playerTransform))
            Runner.Despawn(playerTransform.GetComponent<NetworkObject>());
        m_PlayerList.Remove(player);
        m_ReadyPlayerList.Remove(player);
        m_PlayerTransformList = m_PlayerList.Values.ToList();

        if (Runner.IsServer)
            --ReadyPlayerCount;
        RefreshReadyCountText();
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
