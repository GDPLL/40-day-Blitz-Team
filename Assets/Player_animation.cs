using UnityEngine;

/// View: converts PlayerModel state into Animator parameters; it contains no gameplay decisions.
public sealed class Player_animation : MonoBehaviour
{
    public Animator animation;

    public void Render(PlayerModel model)
    {
        if (animation == null || model == null) return;
        animation.SetBool("run", model.IsRunning);
        animation.SetBool("walk", model.IsMoving);
        animation.SetBool("raiseGun", model.IsAiming);
        animation.SetBool("jump", model.JumpRequested);
        animation.SetBool("suspended", !model.IsGrounded);
        animation.SetBool("squat", model.IsCrouching);
    }

}
