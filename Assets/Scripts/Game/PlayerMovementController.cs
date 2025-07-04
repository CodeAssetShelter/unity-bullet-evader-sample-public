using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// The class is dedicated to controlling the Spaceship's movement
public class PlayerMovementController : NetworkBehaviour, IGamePlayerMove
{
    // Game Session AGNOSTIC Settings
    [SerializeField] private float m_RotationSpeed = 10f;
    [SerializeField] private float m_MovementSpeed = 1.5f;
    [SerializeField] private float m_MaxSpeed = 6.0f;
    [SerializeField] private SpaceshipController m_MainController;

    // Local Runtime references
    private Rigidbody2D
        m_Rigidbody =
            null; // The Unity Rigidbody (RB) is automatically synchronised across the network thanks to the NetworkRigidbody (NRB) component.

    //private SpaceshipController _spaceshipController = null;

    bool m_IsStart = false;

    public override void Spawned()
    {
        // --- Host & Client
        // Set the local runtime references.
        m_Rigidbody = GetComponent<Rigidbody2D>();
        //_spaceshipController = GetComponent<SpaceshipController>();

        // --- Host
        // The Game Session SPECIFIC settings are initialized
        if (Object.HasStateAuthority == false) return;
    }

    //public override void FixedUpdateNetwork()
    //{
        // Bail out of FUN() if this spaceship does not currently accept input
        //if (_spaceshipController.AcceptInput == false) return;

        // GetInput() can only be called from NetworkBehaviours.
        // In SimulationBehaviours, either TryGetInputForPlayer<T>() or GetInputForPlayer<T>() has to be called.
        // This will only return true on the Client with InputAuthority for this Object and the Host.
        //if (Runner.TryGetInputForPlayer<PlayerInputBase>(Object.InputAuthority, out var input))

        //if (!m_MainController.m_IsAlive) return;
        // GetInput() 은 다른 유저가 아닌 내 입력권한만 검사
        //if (m_MainController.m_IsAlive && GetInput<PlayerInputBase>(out var input))
        //{
        //    Move(input);
        //}
    //}

    // Moves the spaceship RB using the input for the client with InputAuthority over the object
    public void Move(PlayerInputBase input)
    {
        if (!m_MainController.m_IsAlive)
        {
            return;
        }

        float dx = input.x * m_MovementSpeed;
        float dy = input.y * m_MovementSpeed;

        Vector3 nextPos = transform.position + new Vector3(dx, dy) * Runner.DeltaTime;
        Vector3 view = Camera.main.WorldToViewportPoint(nextPos);

        if (view.x < 0f || view.x > 1f) dx = 0f;
        if (view.y < 0f || view.y > 1f) dy = 0f;

        m_Rigidbody.linearVelocityX = dx;
        m_Rigidbody.linearVelocityY = dy;
    }
}
