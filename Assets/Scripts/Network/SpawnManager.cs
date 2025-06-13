using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class SpawnManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static SpawnManager Instance;

    public GameObject m_PlayerPrefab;
    public GameObject m_PressSpace;

    public Dictionary<PlayerRef, Transform> m_PlayerList = new();

    // Game Session SPECIFIC Settings
    [Networked] private NetworkButtons m_ButtonsPrevious { get; set; }

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnPlayer(PlayerRef _playerRef)
    {
        var playerObject = Runner.Spawn(m_PlayerPrefab, Vector2.zero, Quaternion.identity, _playerRef, 
            (runner, obj) => 
            {
                Debug.Log($"{obj.name} // {runner.LocalPlayer.PlayerId} is spawned.");
            });

        Runner.SetPlayerObject(_playerRef, playerObject);
        m_PlayerList.Add(_playerRef, playerObject.transform);
        playerObject.GetComponent<CapsuleCollider2D>().enabled = false;
    }

    public void PlayerJoined(PlayerRef player)
    {
        SpawnPlayer(player);
    }

    public void PlayerLeft(PlayerRef player)
    {
        m_PlayerList.Remove(player);
    }

    public Transform GetRandomPlayerTransform()
    {
        if (m_PlayerList.Count == 0) return null;
        var res = m_PlayerList.FirstOrDefault();
        return res.Value != null ? res.Value : null;
    }
}
