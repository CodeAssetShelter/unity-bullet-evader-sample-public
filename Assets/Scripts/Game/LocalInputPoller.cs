using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;


public struct PlayerInputBase : INetworkInput
{
    public enum GameButtons
    {
        Spacebar = 0,
    }

    public float x;
    public float y;
    public NetworkButtons buttons;
}

public class LocalInputPoller : MonoBehaviour, INetworkRunnerCallbacks
{
    private const string AXIS_HORIZONTAL = "Horizontal";
    private const string AXIS_VERTICAL = "Vertical";
    private const string BUTTON_FIRE1 = "Fire1";
    private const string BUTTON_JUMP = "Jump"; // Can be used as an alternative fire button to shoot with SPACE

    // [Main] It's always called by runner
    // The INetworkRunnerCallbacks of this LocalInputPoller are automatically detected
    // because the script is located on the same object as the NetworkRunner and
    // NetworkRunnerCallbacks scripts.

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerInputBase localInput = new PlayerInputBase();

        localInput.x = Input.GetAxisRaw(AXIS_HORIZONTAL);
        localInput.y = Input.GetAxisRaw(AXIS_VERTICAL);
        localInput.buttons.Set(PlayerInputBase.GameButtons.Spacebar, Input.GetKey(KeyCode.Space));

        input.Set(localInput);
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }
}