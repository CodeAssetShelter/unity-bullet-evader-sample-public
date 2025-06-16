using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static SpawnManager Instance;

    public GameObject m_PlayerPrefab;
    public GameObject m_PressSpace;

    public List<Sprite> m_AircraftSprites;
    private int m_AircraftIdx = 0;

    [Header("- EFX")]
    public GameObject m_ExplosionPrefab;

    public Dictionary<PlayerRef, Transform> m_PlayerList = new();

    // Game Session SPECIFIC Settings
    [Networked] private NetworkButtons m_ButtonsPrevious { get; set; }

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        LocalObjectPool.Instance.RegisterPrefab(m_ExplosionPrefab);
    }
    public void SpawnPlayer(PlayerRef _playerRef)
    {
        var playerObject = Runner.Spawn(m_PlayerPrefab, Vector2.zero, Quaternion.identity, _playerRef, 
            (runner, obj) => 
            {
                obj.GetComponent<SpaceshipController>().SetAircraft(m_AircraftSprites[m_AircraftIdx++ % m_AircraftSprites.Count]);
                Debug.Log($"{obj.name} // {runner.LocalPlayer.PlayerId} is spawned.");
            });

        Runner.SetPlayerObject(_playerRef, playerObject);
        m_PlayerList.Add(_playerRef, playerObject.transform);
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

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_DestroyAnim(PlayerRef _playerRef)
    {
        StartCoroutine(CorPlayDestroyAnim(Runner.GetPlayerObject(_playerRef).transform));
    }

    IEnumerator CorPlayDestroyAnim(Transform _target)
    {
        if (_target == null) yield break;

        float timeStamp = 0;
        Vector2 pos = _target.position;

        _target.GetComponent<SpaceshipController>().PlayDestroyAnim();
        
        while (timeStamp  <= 2.0f)
        {
            if (_target != null)
            {
                pos = _target.position;
            }

            var explosion = LocalObjectPool.Instance.Get(m_ExplosionPrefab.name, pos, quaternion.identity);
            explosion.SetActive(true);

            float interval = Random.Range(0, 0.2f);
            timeStamp += interval;
            yield return new WaitForSeconds(interval);
        }
    }
}
