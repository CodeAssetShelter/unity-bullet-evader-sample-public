using Fusion;
using System;
using UnityEngine;

public class MasterInputController : NetworkBehaviour
{
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        OnDirectionKeyDown();
        OnSpaceKeyDown();
    }
    [Networked] private NetworkButtons m_ButtonPrev { get; set; }

    Action<PlayerInputBase> m_MoveActions = null;
    private void OnDirectionKeyDown()
    {
        //GetInput() 은 다른 유저가 아닌 내 입력권한만 검사
        bool inputs = GetInput<PlayerInputBase>(out var input);
        if (!inputs) return;

        if (input.buttons.WasPressed(m_ButtonPrev, PlayerInputBase.GameButtons.Spacebar))
        {
            var o = GameManager.Instance.SpawnPlayer(ref m_MoveActions);
        }
        m_ButtonPrev = input.buttons;

        m_MoveActions?.Invoke(input);
    }

    private void OnSpaceKeyDown()
    {
        bool inputs = GetInput<PlayerInputBase>(out var input);
        if (!inputs) return;

        m_MoveActions?.Invoke(input);
    }
}
