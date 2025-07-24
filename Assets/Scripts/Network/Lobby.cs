using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;
using NUnit.Framework;
using static Fusion.Sockets.NetBitBuffer;
using static UnityEngine.GraphicsBuffer;
using JetBrains.Annotations;


// A utility class which defines the behaviour of the various buttons and input fields found in the Menu scene
public class StartMenu : MonoBehaviour
{
    public enum LobbyState
    {
        Intro,
        Lobby,
        Settings,
        StateCount
    }

    // *-------- Lobby UI ----------------------------------------------
    [Header("- UI")]
    public RectTransform m_Title;
    [SerializeField] private LobbyState m_LobbyState = LobbyState.Intro;

    [Space(10)]
    [SerializeField] private GameObject m_HighScore;
    [SerializeField] private TextMeshProUGUI m_HighScoreHeadText;
    [SerializeField] private TextMeshProUGUI m_HighScoreTailText;
    [SerializeField] private GameObject m_Block;

    [Space(5)]
    [SerializeField] private GameSettings m_GameSettings;
    [SerializeField] private RectTransform m_Arrow;
    [SerializeField] private List<Button> m_MainMenuBtns;
    [SerializeField] int m_BtnIdx = 0;    // 현재 선택된 버튼 인덱스 (0 = 맨 위)
    [SerializeField] float m_Offset = 45f;   // TMP 왼쪽 – 화살표 오른쪽 간격(px)

    // *-------- Runner ------------------------------------------------
    [Header("- Runner")]
    [SerializeField] private NetworkRunner m_NetworkRunnerPrefab = null;

    [Space(10)]
    [SerializeField] private TMP_InputField m_RoomName = null;
    [SerializeField] private string m_GameSceneName = null;

    [Space(10)]
    [SerializeField] private SoundManager m_SoundManagerPrefab;
    private NetworkRunner m_RunnerInstance = null;

    private void Awake()
    {
        if (SoundManager.Instance == null)
        {
            Instantiate(m_SoundManagerPrefab);
        }
        else
        {
            SoundManager.Instance.StopAllSound();
        }

        Application.targetFrameRate = 60;
    }

    public void OnEnable()
    {
        ShowLobbyMainUI(false);
        UpdateArrowPos();          // 처음 화살표 위치 결정
        LoadHighScore();
        //m_MainMenuBtns[m_BtnIdx].Select();

        switch (m_LobbyState)
        {
            case LobbyState.Intro:
                StartCoroutine(CorLobbyIntro());
                break;
            case LobbyState.Lobby:
                ShowLobbyMainUI(true);
                ActiveLobby();
                break;
            case LobbyState.Settings:
                break;
            default:
                break;
        }
    }

    void Start()
    {
        m_GameSettings.InitSettings();
    }

    #region -------- UI -------------------------------------------------
    IEnumerator CorLobbyIntro()
    { 
        while (!Input.GetMouseButtonDown(0) && !Input.GetKeyUp(KeyCode.Space))
        {
            Vector2 current = m_Title.anchoredPosition;

                  // 1) 목표까지의 벡터
            Vector2 toTarget = Vector2.zero - current;

                  // 2) 현재 프레임에 이동할 최대 거리
            float maxStep = 60 * Time.unscaledDeltaTime;

                  // 3) 남은 거리보다 작거나 같으면 한 번에 도착 → 정확히 (0,0)에 정지
            if (toTarget.sqrMagnitude <= maxStep * maxStep)
            {
                m_Title.anchoredPosition = Vector2.zero;
                break;
            }

                  // 4) 일정 속도로 이동
            m_Title.anchoredPosition = current + toTarget.normalized * maxStep;

            yield return null;
        }

        try
        {
            m_Title.anchoredPosition = Vector2.zero;
        }
        catch(System.NullReferenceException e)
        {
            Debug.LogException(e);
        }
        finally
        {
            m_LobbyState = LobbyState.Lobby;
        }

        m_MainMenuBtns.ForEach(x => x.enabled = false);
        ShowLobbyMainUI(true);

        // 연속 클릭 방지를 위함
        yield return new WaitForSeconds(0.5f);
        m_MainMenuBtns.ForEach(x => x.enabled = true);

        ActiveLobby();
    }

    private void ActiveLobby()
    {
        if (m_LobbyState != LobbyState.Lobby) return;
        if (m_CoLobby != null) StopCoroutine(m_CoLobby);
        StartCoroutine(CorLobby());
    }

    Coroutine m_CoLobby;
    IEnumerator CorLobby()
    {
        yield return new WaitForSeconds(0.2f);
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                Move(1);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                Move(-1);
            }
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F))
            {
                Interaction();
                yield break;
            }
            yield return null;
        }
    }
    void Move(int dir) // dir = ±1
    {
        m_BtnIdx = (m_BtnIdx + dir + m_MainMenuBtns.Count) % m_MainMenuBtns.Count;
        UpdateArrowPos();

        // Select() 는 Space Sumbit 을 대기함
        // SpaceBar 만 쓸 거라면 주석해제하고 코드 변경할 것
        //m_MainMenuBtns[m_BtnIdx].Select();          // 선택 상태(하이라이트) 갱신
    }
    bool Interaction()
    {
        var btn = m_MainMenuBtns[m_BtnIdx];
        bool hasEvent = btn.onClick.GetPersistentEventCount() > 0;
        if (hasEvent)
            m_MainMenuBtns[m_BtnIdx].onClick.Invoke();

        return hasEvent;
    }
    void UpdateArrowPos()
    {
        var targetRT = m_MainMenuBtns[m_BtnIdx].GetComponent<RectTransform>();
        CommonUtil.PositionArrowLeftOfTMP(targetRT, m_Arrow, m_Offset);
    }
    private void ShowLobbyMainUI(bool _show)
    {
        m_MainMenuBtns.ForEach(x => x.gameObject.SetActive(_show));
        m_Arrow.gameObject.SetActive(_show);
        m_HighScore.SetActive(_show);

        if (_show)
        {
            Canvas.ForceUpdateCanvases(); // 레이아웃 즉시 갱신
            UpdateArrowPos();             // 갱신 후 화살표 스냅
        }
    }

    private void LoadHighScore()
    {
        int highScoreHead = 0;
        int highScoreTail = 1000;
        if (PlayerPrefs.HasKey(Defines.HIGH_SCORE_HEAD))
            highScoreHead = PlayerPrefs.GetInt(Defines.HIGH_SCORE_HEAD);
        
        if (PlayerPrefs.HasKey(Defines.HIGH_SCORE_TAIL))
            highScoreTail = PlayerPrefs.GetInt(Defines.HIGH_SCORE_TAIL);

        m_HighScoreHeadText.text = highScoreHead > 0 ? highScoreHead.ToString() : "";
        m_HighScoreTailText.text = highScoreTail.ToString();
    }
    #endregion

    #region -------- Runner & Game Start Logic --------------------------
    // Attempts to start a new game session 
    public void StartHost()
    {
        StartGame(GameMode.AutoHostOrClient, "", m_GameSceneName);
    }

    public void StartSingle()
    {
        StartGame(GameMode.Single, "", m_GameSceneName);
    }

    bool m_Starting = false;
    private async void StartGame(GameMode mode, string roomName, string sceneName)
    {
        m_Block.SetActive(true);
        m_RunnerInstance = FindFirstObjectByType<NetworkRunner>();
        if (m_RunnerInstance == null)
        {
            m_RunnerInstance = Instantiate(m_NetworkRunnerPrefab);
        }

        // Let the Fusion Runner know that we will be providing user input
        m_RunnerInstance.ProvideInput = true;

        var startGameArgs = new StartGameArgs()
        {
            GameMode = mode,
            //GameMode =  GameMode.Single,
            //SessionName = roomName,
            PlayerCount = 4, // 최대 4명
            ObjectProvider = m_RunnerInstance.GetComponent<NetworkObjectPoolDefault>(),
        };

        // GameMode.Host = Start a session with a specific name
        // GameMode.Client = Join a session with a specific name
        var res = await m_RunnerInstance.StartGame(startGameArgs);

        if (res.Ok)
        {
            if (m_RunnerInstance.IsServer)
            {
                m_RunnerInstance.LoadScene(sceneName);
            }            
        }
        else
        {
            Debug.LogError(res.ShutdownReason);
            m_Block.SetActive(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    #endregion
}