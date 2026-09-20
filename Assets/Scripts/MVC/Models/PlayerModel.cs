using System;
using UnityEngine;

/// Pure domain state. No MonoBehaviour, Transform, Input or physics dependencies.
[Serializable]
public sealed class PlayerModel
{
    public bool IsGrounded { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsAiming { get; private set; }
    public bool IsFiring { get; private set; }
    public bool IsMoving { get; private set; }
    public bool JumpRequested { get; private set; }
    public bool ReloadRequested { get; private set; }
    public float MoveSpeed { get; private set; }
    public float MoveForce { get; private set; }

    public void Apply(PlayerInput input, bool grounded, float walkSpeed, float runSpeed, float crouchSpeed, float walkForce)
    {
        IsGrounded = grounded;
        IsCrouching = input.Crouch;
        IsFiring = input.Fire;
        IsAiming = input.Fire || input.Aim;
        IsMoving = input.Move.sqrMagnitude > .001f;
        IsRunning = input.Run && !IsCrouching;
        JumpRequested = input.Jump && grounded;
        ReloadRequested = input.Reload;
        MoveSpeed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
        MoveForce = IsCrouching ? walkForce * .8f : IsRunning ? walkForce * 3f : walkForce;
    }

    public void ConsumeFrameActions() { JumpRequested = false; ReloadRequested = false; }
}

public struct PlayerInput
{
    public Vector2 Move;
    public bool Fire, Aim, Jump, Reload, Run, Crouch;
}
