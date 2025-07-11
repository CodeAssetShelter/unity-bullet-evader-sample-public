using Fusion;
using System;
using UnityEngine;

//public class MasterInputController : NetworkBehaviour, IGameRegisterPlayer
//{
//    public SpaceshipController Player { get; private set; }
//    IGamePlayerMove m_IGameMove;

//    public override void Spawned()
//    {
//        base.Spawned();
//        if (Object.HasInputAuthority)
//        {
//        }
//    }

//    public override void FixedUpdateNetwork()
//    {
//        base.FixedUpdateNetwork();
//        //OnDirectionKeyDown();
//        OnSpaceKeyDown();
//    }

//    private void OnDirectionKeyDown()
//    {
//        bool inputs = GetInput<PlayerInputBase>(out var input);
//        if (!inputs) return;
//        m_IGameMove?.Move(input);
//    }

//    [Networked] private NetworkButtons m_ButtonPrev { get; set; }
//    private void OnSpaceKeyDown()
//    {
//        if (!Object.HasInputAuthority) return;

//        //GetInput() 은 다른 유저가 아닌 내 입력권한만 검사
//        bool inputs = GetInput<PlayerInputBase>(out var input);
//        if (!inputs) return;

//        if (input.buttons.WasPressed(m_ButtonPrev, PlayerInputBase.GameButtons.Spacebar))
//        {
//            GameManager.Instance.SpawnPlayer();
//        }
//        m_ButtonPrev = input.buttons;
//    }

//    public void RegisterPlayer(GameObject _obj)
//    {
//        if (!Object.HasInputAuthority) return;

//        _obj.TryGetComponent<IGamePlayerMove>(out var _script);
//        if (_script == null) return;

//        Player = _obj.GetComponent<SpaceshipController>();
//        m_IGameMove = _script;
//    }
//}
