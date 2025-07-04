using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnManager : NetworkBehaviour
{
    public GameObject m_PlayerPrefab;
    public MasterInputController m_MasterInputManagerPrefab;

    public List<Sprite> m_AircraftSprites;
    private int m_AircraftIdx = 0;

    [Header("- EFX")]
    public GameObject m_ExplosionPrefab;


    // Game Session SPECIFIC Settings
    [Networked] private NetworkButtons m_ButtonsPrevious { get; set; }

    private void Start()
    {
        LocalObjectPool.Instance.RegisterPrefab(m_ExplosionPrefab);
    }


    public void RequestSpawnPlayer(PlayerRef _playerRef)
    {
        if (Runner.IsServer)
        {
            SpawnPlayer(_playerRef);
        }
        else
        {
            RpcRequestSpawnPlayer(_playerRef);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRequestSpawnPlayer(PlayerRef _playerRef)
    {
        SpawnPlayer(_playerRef);
    }

    private void SpawnPlayer(PlayerRef _playerRef)
    {
        var playerObject = Runner.Spawn(m_PlayerPrefab, Vector2.zero, Quaternion.identity, _playerRef,
        (runner, obj) =>
        {
            obj.GetComponent<SpaceshipController>().SetAircraft(m_AircraftSprites[m_AircraftIdx++ % m_AircraftSprites.Count]);
            Debug.Log($"{obj.name} // {runner.LocalPlayer.PlayerId} is spawned.");
        });
    }

    public NetworkObject SpawnPlayerMasterController(PlayerRef player)
    {
        // 테스트용
        //SpawnPlayer(player);
        var playerObject = Runner.Spawn(m_MasterInputManagerPrefab, Vector2.zero, Quaternion.identity, player,
        (runner, obj) =>
        {
            Debug.Log($"{obj.name} // {runner.LocalPlayer.PlayerId} is spawned.");
        });

        var no = playerObject.GetComponent<NetworkObject>();
        Runner.SetPlayerObject(player, no);
        return no;
    }


    public void PlayDestroyAnim(PlayerRef _playerRef)
    {
        var po = Runner.GetPlayerObject(_playerRef);
        StartCoroutine(CorPlayDestroyAnim(po.transform));
    }

    IEnumerator CorPlayDestroyAnim(Transform _target)
    {
        if (_target == null) yield break;

        float timeStamp = 0;
        Vector2 pos = _target.position;

        // 기존
        //_target.GetComponent<SpaceshipController>().PlayDestroyAnim();

        // MIC -> m_Player -> DestroyAnim();
        Transform p = _target.GetComponent<MasterInputController>().Player.transform;

        while (timeStamp  <= 2.0f)
        {
            if (p != null)
            {
                pos = p.position;
            }

            var explosion = LocalObjectPool.Instance.Get(m_ExplosionPrefab.name, pos, quaternion.identity);
            explosion.SetActive(true);

            float interval = Random.Range(0, 0.2f);
            timeStamp += interval;
            yield return new WaitForSeconds(interval);
        }
    }
}
