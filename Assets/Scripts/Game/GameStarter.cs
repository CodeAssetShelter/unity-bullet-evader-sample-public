using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;

public class GameStarter : MonoBehaviour
{
    [SerializeField] private List<NetworkObject> m_Objs = new();    
    public void GameStart(NetworkRunner _runner)
    {
        if (_runner.IsServer)
        {
            foreach (NetworkObject obj in m_Objs)
            {
                _runner.Spawn(obj);
            }
        }
    }
}
