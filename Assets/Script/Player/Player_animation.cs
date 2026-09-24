using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_animation : MonoBehaviour
{
    public Player_Control player_Control;

    public Animator animation;

    void FixedUpdate()
    {
        animation.SetBool("run",player_Control.isRuning);
        animation.SetBool("walk",player_Control.isWASDDowm);
        animation.SetBool("raiseGun",player_Control.isMouseDown);
        animation.SetBool("jump",player_Control.isJumpDown);
        animation.SetBool("suspended",!player_Control.isOnGround);
        animation.SetBool("squat",player_Control.isSquat);

    }
}
