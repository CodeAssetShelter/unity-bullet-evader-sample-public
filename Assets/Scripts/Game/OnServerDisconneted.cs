using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnServerDisconneted : MonoBehaviour, INetworkRunnerCallbacks
{
    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogError($"Disconnect By {reason}");
        if (reason == NetDisconnectReason.Requested)
            SceneManager.LoadScene("Lobby");
    }

    public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.LogWarning($"OnHostMigration! {runner.LocalPlayer} - {hostMigrationToken}");
        if (hostMigrationToken == null)
        {
            Debug.LogWarning("No HostMigrationToken. Normal shutdown or rejoin flow.");
            await runner.Shutdown();
            return;
        }

        // 1) 기존 Runner 종료
        await runner.Shutdown(true, ShutdownReason.HostMigration);

        // 2) 새 Runner 생성
        var newRunner = Instantiate(Resources.Load<NetworkRunner>("NetworkRunner"));

        // 3) HostMigrationToken 과 Resume 콜백을 전달해 시작
        var result = await newRunner.StartGame(new StartGameArgs
        {
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
        });

        if (!result.Ok)
            Debug.LogWarning(result.ShutdownReason);
    }

    // Step 3.
    // Resume Simulation on the new Runner
    void HostMigrationResume(NetworkRunner runner)
    {
        Dictionary<PlayerRef, Dictionary<string, object>> resume_data = new();

        // Get a temporary reference for each NO from the old Host
        foreach (var snapObj in runner.GetResumeSnapshotNetworkObjects())
        {
            Debug.Log($"old snap - {snapObj.name} - {snapObj.InputAuthority}");
            if (snapObj.TryGetComponent<SpaceshipController>(out var sc))
            {
                Debug.Log("SpaceShip is not respawn target, but data will copy");

                var player = snapObj.InputAuthority;

                // 1) 바깥 딕셔너리 보장
                if (!resume_data.TryGetValue(player, out var dict))
                {
                    dict = new Dictionary<string, object>();
                    resume_data[player] = dict;
                }

                // 2) 값 기록
                dict[Defines.MIG_PLAYER_LIFE] = sc.Life;

                continue;
            }
        }

        foreach (var snapObj in runner.GetResumeSnapshotNetworkObjects())
        {
            if (snapObj.TryGetComponent<SpaceshipController>(out var sc))
            {
                continue;
            }

            runner.Spawn(snapObj, onBeforeSpawned: (r, newObj) =>
            {
                newObj.CopyStateFrom(snapObj); // 모든 NetworkBehaviour 상태 통째 복원
                if (newObj.TryGetComponent(out GameManager gameManager))
                {
                    var oldGM = snapObj.GetComponent<GameManager>();
                    bool wasGameOver = oldGM.GameOverUserCount >= oldGM.ReadyPlayerCount - 1;
                    gameManager.m_State = wasGameOver ? GameManager.GameState.GameOverAll : GameManager.GameState.Ready;
                    gameManager.LoadScore(true);
                    gameManager.SetResumeData(resume_data);
                }
            });
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Left - {player}");
        if (runner.IsServer)
        {
            runner.Despawn(runner.GetPlayerObject(player));
        }
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.IsServer)
            FindAnyObjectByType<GameStarter>().GameStart(runner);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // 임시로 여기서 돌아감
        if (shutdownReason != ShutdownReason.HostMigration)
            SceneManager.LoadScene("Lobby");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }
}
