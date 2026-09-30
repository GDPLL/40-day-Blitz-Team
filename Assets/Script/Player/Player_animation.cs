using UnityEngine;

// 玩家动画表现，状态位驱动
public class Player_animation : MonoBehaviour
{
    // 状态位取值定义
    public const byte BitWalk = 1 << 0;     //移动
    public const byte BitRun = 1 << 1;      //奔跑
    public const byte BitSquat = 1 << 2;    //蹲下
    public const byte BitGun = 1 << 3;      //举枪
    public const byte BitJump = 1 << 4;     //跳跃
    public const byte BitAir = 1 << 5;      //离地

    public new Animator animation;   // 动画组件

    // 按状态位同步动画参数
    public void Player_animation_Show(byte state)
    {
        animation.SetBool("walk", (state & BitWalk) != 0);
        animation.SetBool("run", (state & BitRun) != 0);
        animation.SetBool("squat", (state & BitSquat) != 0);
        animation.SetBool("raiseGun", (state & BitGun) != 0);
        animation.SetBool("jump", (state & BitJump) != 0);
        animation.SetBool("suspended", (state & BitAir) != 0);
    }
}
