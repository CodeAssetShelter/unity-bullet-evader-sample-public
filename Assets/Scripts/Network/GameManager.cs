using Fusion;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Input = UnityEngine.Input;
using Fusion.Sockets;
using Random = UnityEngine.Random;
using TMPro;
using System.Threading.Tasks;

public static class Defines
{
    public const string HIGH_SCORE_HEAD = "highScoreHead";
    public const string HIGH_SCORE_TAIL = "highScoreTail";
    public const string HIGH_SCORE_HEAD_TEMP = "highScoreHeadTemp";
    public const string HIGH_SCORE_TAIL_TEMP = "highScoreTailTemp";

    public const string MIG_PLAYER_LIFE = "playerLife";
}

public class GameManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft, IAfterSpawned, INetworkRunnerCallbacks
{
    public static GameManager Instance;

    public enum GameState
    {
        Ready = 0,
        ReadyMultiplay,
        Play,
        GameOverAll,
        Result,
        StateCount
    }

    [Networked] public GameState m_State { get; set; } = GameState.Ready;

    /// <summary>
    /// 1이 기본값
    /// </summary>
    [Networked] public float GameLevel { get; private set; } = 1;
    [Networked] public int ReadyPlayerCount { get; set; } = 0;
    [Networked] public int GameOverUserCount { get; set; } = 0;

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
    [SerializeField] private TextMeshProUGUI m_AllGameOverText;
    [SerializeField] private TextMeshProUGUI m_AllGameOverDetailText;

    [Space(5)]
    [SerializeField] private GameObject m_ObserverModeText;
    [Space(5)]
    [SerializeField] private TextMeshProUGUI m_ScoreHeadText;
    [SerializeField] private TextMeshProUGUI m_ScoreTailText;

    private const float m_LevelUpInterval = 30f;
    private float m_LevelUpTimeStamp = 0;

    private const float m_GameOverAllCheckInterval = 2f;
    private float m_GameOverAllCheckTimeStamp = 0;

    private const float m_DifficultyPlus = 0.3f;

    [Space(20)]
    [SerializeField] private SpawnManager m_SpawnManager;

    [SerializeField] private LevelContainerAsset m_LevelContainerAsset;

    [Space(20)]
    [SerializeField] private int m_ScoreHead = 0;
    [SerializeField] private int m_ScoreTail = 0;
    private const int SCORE_HEAD_MAX = 1000000;
    private const int SCORE_TAIL_MAX = 1000000;
    private const string SCORE_FORMAT = "{0:000000}";
    private const string READYCOUNT_FORMAT = "{0}/{1}";

    [Space(20)]
    [SerializeField] private NetworkObject m_MyPlayer;

    public Dictionary<PlayerRef, Transform> m_PlayerList = new();
    public Dictionary<PlayerRef, bool> m_ReadyPlayerList = new();
    public List<Transform> m_PlayerTransformList = new();

    [SerializeField] private bool m_NowPlaying = false;

    private readonly System.Random m_Rng = new();

    private ChangeDetector m_ChangeDetector;
    [Networked] private TickTimer m_SessionCloseTick { get; set; }
    private const float m_SessionCloseTime = 2.5f;

    private Dictionary<PlayerRef, Dictionary<string, object>> m_ResumeData = new();

    public override void Spawned()
    {
        base.Spawned();

        Runner.AddCallbacks(this);

        // *-------- Init -----------------------------
        m_ChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        InitScoreText();

        Debug.Log("GameManager Spawned()");
    }

    private void Start()
    {
        SoundManager.Instance.PreloadSound(SoundManager.SoundType.EFX_ExplosionSmall_000);
        SoundManager.Instance.PreloadSound(SoundManager.SoundType.EFX_ExplosionSmall_001);
        SoundManager.Instance.PreloadSound(SoundManager.SoundType.BGM_001);
    }
    public void AfterSpawned()
    {
        ShowActiveMultiplayWaitingUI(m_State);

        if (m_State == GameState.GameOverAll)
        {
            ShowAllGameOver();
        }
    }

    private void ShowActiveMultiplayWaitingUI(GameState _state)
    {
        if (m_NowPlaying) return;

        m_ReadyText.gameObject.SetActive(_state == GameState.Ready || _state == GameState.Play);
        m_ReadyMultiText.gameObject.SetActive(_state == GameState.ReadyMultiplay);
        m_ReadyMultiGetReadyText.gameObject.SetActive(_state == GameState.ReadyMultiplay);
        m_ReadyMultiPlayerCountText.gameObject.SetActive(_state == GameState.ReadyMultiplay);

        // 강제 비활성화 부분
        m_GameOverText.SetActive(false);
        m_GameOverDetailText.SetActive(false);
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
        Instance = this;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (m_SessionCloseTick.Expired(Runner) && Runner.IsServer)
        {
            Runner.SessionInfo.IsOpen = false;
            m_SessionCloseTick = default;
        }

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
                if (m_LevelUpTimeStamp > m_LevelUpInterval && Runner.IsServer)
                {
                    m_LevelUpTimeStamp = 0;
                    RpcUpdateGameLevel(Mathf.Clamp(GetGameLevel() + m_DifficultyPlus, 1, 5));
                }
                else m_LevelUpTimeStamp += Runner.DeltaTime;
                if (m_GameOverAllCheckTimeStamp > m_GameOverAllCheckInterval && Runner.IsServer)
                {
                    GameOverCheck();
                }
                else m_GameOverAllCheckTimeStamp += Runner.DeltaTime;
                break;
            case GameState.GameOverAll:
                break;
            case GameState.StateCount:
                break;
            default:
                break;
        }
    }

    private void GameOverCheck()
    {
        m_GameOverAllCheckTimeStamp = 0;
        if (GameOverUserCount >= Runner.ActivePlayers.Count())
        {
            RpcActiveAllGameOver();
            m_State = GameState.GameOverAll;
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
        SaveScore();
        //RpcPlayerGameOver(Runner.LocalPlayer);

        if (GameOverUserCount < Runner.ActivePlayers.Count())
        {
            m_GameOverText.SetActive(true);
            m_GameOverDetailText.SetActive(true);

            if (m_CoWaitObserber != null) StopCoroutine(m_CoWaitObserber);
            m_CoWaitObserber = StartCoroutine(CorWaitObserber());
        }
    }

    private void SaveScore(bool _isCrash = false)
    {
        // m_ScoreHeadText
        PlayerPrefs.SetInt(_isCrash ? Defines.HIGH_SCORE_HEAD_TEMP : Defines.HIGH_SCORE_HEAD, m_ScoreHead);
        PlayerPrefs.SetInt(_isCrash ? Defines.HIGH_SCORE_TAIL_TEMP : Defines.HIGH_SCORE_TAIL, m_ScoreTail);
    }

    public void LoadScore(bool _isCrash = false)
    {
        // m_ScoreHeadText
        m_ScoreHead = PlayerPrefs.GetInt(_isCrash ? Defines.HIGH_SCORE_HEAD_TEMP : Defines.HIGH_SCORE_HEAD);
        m_ScoreTail = PlayerPrefs.GetInt(_isCrash ? Defines.HIGH_SCORE_TAIL_TEMP : Defines.HIGH_SCORE_TAIL);
        PlayerPrefs.SetInt(_isCrash ? Defines.HIGH_SCORE_HEAD_TEMP : Defines.HIGH_SCORE_HEAD, 0);
        PlayerPrefs.SetInt(_isCrash ? Defines.HIGH_SCORE_TAIL_TEMP : Defines.HIGH_SCORE_TAIL, 0);
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
        m_SessionCloseTick = TickTimer.CreateFromSeconds(Runner, m_SessionCloseTime);
        RunLevelPatterns();

        // 테스트 패턴 시작
        //BulletSpawner.Instance.RunPattern(BulletPattern.Normal);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Spread);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Winder);
        //BulletSpawner.Instance.RunPattern(BulletPattern.Cage);
    }

    private void RunLevelPatterns()
    {
        var data = m_LevelContainerAsset.GetLevel(GameLevel);
        BulletSpawner.Instance.RunLevelPatterns(data);
    }


    public void ShowAllGameOver()
    {
        m_GameOverDetailText.SetActive(false);
        m_GameOverText.SetActive(false);
        m_ObserverModeText.SetActive(false);

        StartCoroutine(CorAllGameOver());
    }

    IEnumerator CorAllGameOver()
    {
        float timeStamp = 0;
        float animTime = 1.5f;
        Vector2 startRectPos = new Vector2(0, -Screen.height);

        m_AllGameOverText.gameObject.SetActive(true);
        SoundManager.Instance.PlayEfxSound(SoundManager.SoundType.EFX_GameOver);

        while (timeStamp < animTime)
        {
            float t = timeStamp / animTime;
            m_AllGameOverText.rectTransform.anchoredPosition = 
                Vector2.Lerp(startRectPos, Vector2.zero, t);
            timeStamp += Time.deltaTime;
            yield return null;
        }

        m_AllGameOverText.rectTransform.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(0.5f);
        float exitTimer = 5;
        int shownSec = -1;

        string format = m_AllGameOverDetailText.text;
        m_AllGameOverDetailText.SetText(format, exitTimer);
        m_AllGameOverDetailText.gameObject.SetActive(true);

        while (exitTimer > 0f)
        {
            exitTimer -= Time.deltaTime;

            // 현재 남은 시간을 올림(Ceil)해서 5,4,3,2,1 순으로 보이게
            int sec = Mathf.CeilToInt(exitTimer);

            // 값이 바뀔 때만 텍스트 갱신
            if (sec != shownSec)
            {
                shownSec = sec;
                // "5", "4" … 단순 숫자만 표시
                // 필요하면 포맷 문자열로 변경: $"{sec}초 남음" 등
                m_AllGameOverDetailText.SetText(format, sec);
            }

            yield return null;                  // 프레임 대기
        }

        if (Runner.IsServer)
            StartCoroutine(KickAllClients());
    }

    /// <summary>Host 가 호출 – 모든 게스트를 강제 퇴장시킨다.</summary>
    IEnumerator KickAllClients()
    {
        if (!Runner.IsServer)   // Host 판단 (Host ≡ StateAuthority=Server)
        {
            Debug.LogWarning("This is not Host; can't kick players.");
            yield break;
        }

        foreach (var player in Runner.ActivePlayers)
        {
            if (player == Runner.LocalPlayer) continue;        // Host 자신 제외
            Runner.Disconnect(player);
        }

        yield return new WaitUntil(()
            =>
        Runner.ActivePlayers.Count() == 1 && Runner.ActivePlayers.First() == Runner.LocalPlayer);

        Runner.Shutdown();
    }

    public void SetResumeData(Dictionary<PlayerRef, Dictionary<string, object>> _data)
    {
        m_ResumeData = new(_data);
    }

    public object GetResumeData(PlayerRef _player, string _defineKey)
    {
        object res = null;

        if (!m_ResumeData.ContainsKey(_player)) return null;
        if (!m_ResumeData[_player].ContainsKey(_defineKey)) return null;

        res = m_ResumeData[_player][_defineKey];
        return res;
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
            if (m_ResumeData.TryGetValue(Runner.LocalPlayer, out var innerDict) &&
                innerDict.TryGetValue(Defines.MIG_PLAYER_LIFE, out var lifeObj) &&
                lifeObj is int life && life == 0)
            {
                ActiveGameOverUI();
                return;
            }

            if (m_State == GameState.Play)
                m_CoLateJoin = StartCoroutine(CorLateJoin());
            else
                m_CoWaitforOthers = StartCoroutine(CorWaitForOthers());
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcPlayerGameOver(PlayerRef _player)
    {
        GameOverUserCount = Mathf.Clamp(++GameOverUserCount, 0, 4);
        Debug.Log($"{_player} - CALL GAMEOVER {GameOverUserCount}/{Runner.ActivePlayers.Count()}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcActiveAllGameOver()
    {
        ShowAllGameOver();
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
                    m_ReadyText.gameObject.SetActive(false);
                    m_MultiplayWaitForOthers.gameObject.SetActive(true);
                    m_ReadyMultiGetReadyText.gameObject.SetActive(false);
                }
                RpcNowPrepared(Runner.LocalPlayer);
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

        if (Runner.IsServer)
        {
            if (Runner.ActivePlayers.Count() > 1 && m_State == GameState.Ready)
            {
                m_State = GameState.ReadyMultiplay;
            }
            if (Runner.ActivePlayers.Count() >= Runner.SessionInfo.MaxPlayers)
            {
                Runner.SessionInfo.IsOpen = false;
            }
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

    public void AddGameUserCount()
    {
        GameOverUserCount = Mathf.Clamp(++GameOverUserCount, 0, Runner.ActivePlayers.Count());
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

    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
    }

    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.HostMigration)
        {
            SaveScore(true);
        }
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
    }

    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
    {
    }

    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    async void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
    {
    }

    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
    {
    }
    #endregion
}
