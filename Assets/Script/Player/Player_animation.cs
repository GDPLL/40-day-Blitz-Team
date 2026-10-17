using UnityEngine;

// 玩家动画表现，状态位驱动
public class Player_animation : MonoBehaviour
{
    // 状态位取值定义
    public const ushort BitWalk = 1 << 0;       //移动
    public const ushort BitRun = 1 << 1;        //奔跑
    public const ushort BitSquat = 1 << 2;      //蹲下
    public const ushort BitGun = 1 << 3;        //举枪
    public const ushort BitJump = 1 << 4;       //跳跃
    public const ushort BitGround = 1 << 5;     //在地面
    public const ushort BitDeath = 1 << 6;      //死亡
    public const ushort BitRoll = 1 << 7;       //翻滚
    public const ushort BitWeightLess = 1 << 12;    //失重
    public const ushort BitReload = 1 << 13;    //换弹

    const int MoveXShift = 8;                   //方向X位偏移
    const int MoveYShift = 10;                  //方向Y位偏移

    public new Animator animation;   // 动画组件

    // 打包方向轴，每轴2位
    public static ushort Pack_Move(Vector2 axis)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(axis.x), -1, 1) + 1;
        int y = Mathf.Clamp(Mathf.RoundToInt(axis.y), -1, 1) + 1;
        return (ushort)((x << MoveXShift) | (y << MoveYShift));
    }

    // 解包方向轴
    public static Vector2 UnPack_Move(ushort state)
    {
        int x = ((state >> MoveXShift) & 3) - 1;
        int y = ((state >> MoveYShift) & 3) - 1;
        return new Vector2(x, y);
    }

    // 按状态位同步动画参数，verticalSpeed为垂直速度
    public void Player_animation_Show(ushort state, float verticalSpeed)
    {
        bool walk = (state & BitWalk) != 0;
        bool reload = (state & BitReload) != 0;

        animation.SetBool("walk", walk);
        animation.SetBool("raiseGun", (state & BitGun) != 0);
        animation.SetBool("jump", (state & BitJump) != 0);
        animation.SetBool("IsGround", (state & BitGround) != 0);
        animation.SetBool("IsDeath", (state & BitDeath) != 0);
        animation.SetBool("IsRoll", (state & BitRoll) != 0);
        animation.SetBool("IsWeightLess", (state & BitWeightLess) != 0);
        animation.SetBool("IsReload", reload);

        // 奔跑满速，走路半速，静止为零，换弹只能走路
        bool run = (state & BitRun) != 0 && !reload;
        animation.SetFloat("Speed", run ? 1f : walk ? 0.5f : 0f);

        // 换弹或未举枪一律朝前，举枪才启用四方向
        Vector2 move = (state & BitGun) != 0 && !reload ? UnPack_Move(state) : new Vector2(0f, 1f);
        animation.SetFloat("MoveX", move.x);
        animation.SetFloat("MoveY", move.y);

        // 垂直速度，正升负降
        animation.SetFloat("VerticalSpeed", verticalSpeed);
    }
}
