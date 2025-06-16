using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class GameButtonController : NetworkBehaviour
{
    // Game Session SPECIFIC Settings
    [Networked] private NetworkButtons m_ButtonsPrevious { get; set; }

    public override void Spawned()
    {
        Debug.Log("Button Controller is activated");
    }

    public override void FixedUpdateNetwork()
    {
        // GetInput() �� �ٸ� ������ �ƴ� �� �Է±��Ѹ� �˻�
        if (GetInput<PlayerInputBase>(out var input))
        {
            Button(input);
        }
    }

    private void Button(PlayerInputBase input)
    {
        if (input.buttons.WasReleased(m_ButtonsPrevious, PlayerInputBase.GameButtons.Spacebar))
        {
            GetComponent<CapsuleCollider2D>().enabled = true;
        }

        m_ButtonsPrevious = input.buttons;
    }
}
