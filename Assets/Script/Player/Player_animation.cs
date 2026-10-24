using UnityEngine;

// 玩家动画表现，状态位驱动
public class Player_animation : MonoBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    // 状态位取值定义
    public const uint BitWalk = 1u << 0;         //移动
    public const uint BitRun = 1u << 1;          //奔跑
    public const uint BitSquat = 1u << 2;        //蹲下
    public const uint BitGun = 1u << 3;          //举枪
    public const uint BitJump = 1u << 4;         //跳跃
    public const uint BitGround = 1u << 5;       //在地面
    public const uint BitDeath = 1u << 6;        //死亡
    public const uint BitRoll = 1u << 7;         //翻滚
    public const uint BitWeightLess = 1u << 12;  //失重
    public const uint BitReload = 1u << 13;      //换弹
    public const uint BitSlide = 1u << 14;       //滑铲
    public const uint BitPeekLeft = 1u << 15;    //左探头
    public const uint BitPeekRight = 1u << 16;   //右探头

    const int MoveXShift = 8;                   //方向X位偏移
    const int MoveYShift = 10;                  //方向Y位偏移

    public new Animator animation;   // 动画组件
    float peekWeightTimer;          // 探头层保留时间

    // 打包方向轴，每轴2位
    public static uint Pack_Move(Vector2 axis)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(axis.x), -1, 1) + 1;
        int y = Mathf.Clamp(Mathf.RoundToInt(axis.y), -1, 1) + 1;
        return (uint)((x << MoveXShift) | (y << MoveYShift));
    }

    // 解包方向轴
    public static Vector2 UnPack_Move(uint state)
    {
        int x = (int)((state >> MoveXShift) & 3u) - 1;
        int y = (int)((state >> MoveYShift) & 3u) - 1;
        return new Vector2(x, y);
    }

    // 打包探头方向
    public static uint Pack_Peek(bool left, bool right)
    {
        if (left == right) return 0u;
        return left ? BitPeekLeft : BitPeekRight;
    }

    // 按状态位同步动画参数，verticalSpeed为垂直速度
    public void Player_animation_Show(uint state, float verticalSpeed)
    {
        bool walk = (state & BitWalk) != 0;
        bool reload = (state & BitReload) != 0;
        bool squat = (state & BitSquat) != 0;
        bool slide = (state & BitSlide) != 0;
        bool peek = (state & (BitPeekLeft | BitPeekRight)) != 0;

        animation.SetBool("walk", walk);
        animation.SetBool("raiseGun", (state & BitGun) != 0);
        animation.SetBool("jump", (state & BitJump) != 0);
        animation.SetBool("IsGround", (state & BitGround) != 0);
        animation.SetBool("IsDeath", (state & BitDeath) != 0);
        animation.SetBool("IsRoll", (state & BitRoll) != 0);
        animation.SetBool("IsWeightLess", (state & BitWeightLess) != 0);
        animation.SetBool("IsReload", reload);
        animation.SetBool("Squat", squat);
        animation.SetBool("IsSlide", slide);
        animation.SetBool("PeekLeft", (state & BitPeekLeft) != 0);
        animation.SetBool("PeekRight", (state & BitPeekRight) != 0);

        // 奔跑满速，走路半速，静止为零，换弹只能走路
        bool run = (state & BitRun) != 0 && !reload;
        animation.SetFloat("Speed", run ? 1f : walk ? 0.5f : 0f);

        // 举枪或蹲下时启用四方向
        Vector2 move = ((state & BitGun) != 0 && !reload) || squat
            ? UnPack_Move(state) : new Vector2(0f, 1f);
        animation.SetFloat("MoveX", move.x);
        animation.SetFloat("MoveY", move.y);

        // 蹲姿和滑铲使用独立全身层
        if (animation.layerCount > 1)
            animation.SetLayerWeight(1, squat || slide ? 1f : 0f);

        // 滑铲使用整身手臂动作
        if (animation.layerCount > 2) animation.SetLayerWeight(2, slide ? 0f : 1f);

        // 探头层只在探头和回正期间生效
        if (peek) peekWeightTimer = 0.65f;
        else if (peekWeightTimer > 0f) peekWeightTimer -= Time.deltaTime;
        if (animation.layerCount > 4)
            animation.SetLayerWeight(4, peek || peekWeightTimer > 0f ? 1f : 0f);

        // 垂直速度，正升负降
        animation.SetFloat("VerticalSpeed", verticalSpeed);
    }
}
