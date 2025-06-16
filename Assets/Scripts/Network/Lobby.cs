using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.SceneManagement;


// A utility class which defines the behaviour of the various buttons and input fields found in the Menu scene
public class StartMenu : MonoBehaviour
{
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
    }

    // Attempts to start a new game session 
    public void StartHost()
    {
        StartGame(GameMode.AutoHostOrClient, "TEST", m_GameSceneName);
    }

    public void StartClient()
    {
        StartGame(GameMode.Client, "TEST", m_GameSceneName);
    }

    private async void StartGame(GameMode mode, string roomName, string sceneName)
    {
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
            SessionName = roomName,
            PlayerCount = 4, // √÷¥Î 4∏Ì
            ObjectProvider = m_RunnerInstance.GetComponent<NetworkObjectPoolDefault>(),
        };

        // GameMode.Host = Start a session with a specific name
        // GameMode.Client = Join a session with a specific name
        await m_RunnerInstance.StartGame(startGameArgs);

        if (m_RunnerInstance.IsServer)
        {
            m_RunnerInstance.LoadScene(sceneName);
        }
    }
}