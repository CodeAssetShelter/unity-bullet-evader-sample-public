using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// The class is dedicated to controlling the Spaceship's movement
public class PlayerMovementController : NetworkBehaviour
{
    // Game Session AGNOSTIC Settings
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _movementSpeed = 1.5f;
    [SerializeField] private float _maxSpeed = 6.0f;
    [SerializeField] private SpaceshipController m_MainController;

    // Local Runtime references
    private Rigidbody2D
        _rigidbody =
            null; // The Unity Rigidbody (RB) is automatically synchronised across the network thanks to the NetworkRigidbody (NRB) component.

    //private SpaceshipController _spaceshipController = null;

    // Game Session SPECIFIC Settings
    [Networked] private float _screenBoundaryX { get; set; }
    [Networked] private float _screenBoundaryY { get; set; }

    bool m_IsStart = false;

    public override void Spawned()
    {
        // --- Host & Client
        // Set the local runtime references.
        _rigidbody = GetComponent<Rigidbody2D>();
        //_spaceshipController = GetComponent<SpaceshipController>();

        // --- Host
        // The Game Session SPECIFIC settings are initialized
        if (Object.HasStateAuthority == false) return;

        _screenBoundaryX = Camera.main.orthographicSize * Camera.main.aspect;
        _screenBoundaryY = Camera.main.orthographicSize;
    }

    public override void FixedUpdateNetwork()
    {
        // Bail out of FUN() if this spaceship does not currently accept input
        //if (_spaceshipController.AcceptInput == false) return;

        // GetInput() can only be called from NetworkBehaviours.
        // In SimulationBehaviours, either TryGetInputForPlayer<T>() or GetInputForPlayer<T>() has to be called.
        // This will only return true on the Client with InputAuthority for this Object and the Host.
        //if (Runner.TryGetInputForPlayer<PlayerInputBase>(Object.InputAuthority, out var input))

        // GetInput() 은 다른 유저가 아닌 내 입력권한만 검사
        //if (m_MainController.m_IsAlive && GetInput<PlayerInputBase>(out var input))
        //{
        //    Move(input);
        //}
    }

    // Moves the spaceship RB using the input for the client with InputAuthority over the object
    public void Move(PlayerInputBase input)
    {
        Debug.Log("GOGOGO");
        if (!m_MainController.m_IsAlive) return;

        float dx = input.x * _movementSpeed;
        float dy = input.y * _movementSpeed;
        Debug.Log($"{dx}, {dy}");

        Vector3 nextPos = transform.position + new Vector3(dx, dy) * Runner.DeltaTime;
        Vector3 view = Camera.main.WorldToViewportPoint(nextPos);

        if (view.x < 0f || view.x > 1f) dx = 0f;
        if (view.y < 0f || view.y > 1f) dy = 0f;

        _rigidbody.linearVelocityX = dx;
        _rigidbody.linearVelocityY = dy;
    }
}
