using Fusion;
using UnityEngine;

public class RPCManager : NetworkBehaviour
{
    public override void Spawned()
    {
        base.Spawned();
        Debug.Log($"{Runner.UserId} RPC Manager is spawned");
    }



}
