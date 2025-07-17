using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class PoolKey
{
    public const string BULLET = "bullet";
    public const string EXPLOSION = "explosion";
}

public class SpawnManager : NetworkBehaviour
{
    public GameObject m_PlayerPrefab;

    public List<Sprite> m_AircraftSprites;
    private int m_AircraftIdx = 0;

    [Header("- EFX")]
    public GameObject m_ExplosionPrefab;


    // Game Session SPECIFIC Settings
    [Networked] private NetworkButtons m_ButtonsPrevious { get; set; }

    private void Start()
    {
        LocalObjectPool.Instance.RegisterPrefab(PoolKey.EXPLOSION, m_ExplosionPrefab);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestSpawnPlayer(PlayerRef _playerRef)
    {
        SpawnPlayer(_playerRef);
    }

    public void SpawnPlayer(PlayerRef _playerRef)
    {
        var playerObject = Runner.Spawn(m_PlayerPrefab, Vector2.zero, Quaternion.identity, _playerRef,
        (runner, obj) =>
        {
            obj.GetComponent<SpaceshipController>().SetAircraft(m_AircraftIdx++ % m_AircraftSprites.Count);
            Debug.Log($"{obj.name} // {_playerRef} is spawned.");
        });

        Runner.SetPlayerObject(_playerRef, playerObject.GetComponent<NetworkObject>());
        GameManager.Instance.AddPlayer(_playerRef, playerObject.transform);
    }
}
