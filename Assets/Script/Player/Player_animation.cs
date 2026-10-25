using UnityEngine;

// 玩家动画表现，状态位驱动
public class Player_animation : MonoBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control)
    {
        Own_Control = control;
        if (animation == null || animation.runtimeAnimatorController == null)
        {
            Debug.LogError("Player_animation|Owner_Init|动画组件为空");
            return;
        }
        modelBasePosition = animation.transform.localPosition;
        modelBaseRotation = animation.transform.localRotation;
        leftToe = animation.GetBoneTransform(HumanBodyBones.LeftToes);
        rightToe = animation.GetBoneTransform(HumanBodyBones.RightToes);

        // 蹲走动画强制循环
        foreach (AnimationClip clip in animation.runtimeAnimatorController.animationClips)
        {
            if (clip.name.Contains("squat"))
                clip.wrapMode = WrapMode.Loop;
            else if (clip.name == "crouch2")
                clip.wrapMode = WrapMode.ClampForever;
        }
    }

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
    public const uint BitCrouchEnter = 1u << 17; //蹲下过渡

    const int MoveXShift = 8;                   //方向X位偏移
    const int MoveYShift = 10;                  //方向Y位偏移

    public new Animator animation;   // 动画组件
    public float crouchModelLift = 0.19f;   // 蹲姿模型抬升
    public float crouchLiftRiseTime = 0.82f;   // 蹲姿抬升时长
    public float crouchLiftFallTime = 0.15f;   // 蹲姿恢复时长
    public float crouchHoldOffset = 2.44f;    // 蹲姿末帧时间
    public float slideTurnTime = 0.12f;       // 滑铲转身时长
    public float footGroundHeight = 0.02f;    // 脚底离角色根高度
    public float footGroundSpeed = 12f;       // 脚底修正速度
    public float slideModelLift = -0.6f;       // 滑铲模型高度
    public float slideLiftTime = 0.2f;         // 滑铲高度调整时长
    public float slideGroundSmoothTime = 0.12f; // 滑铲贴地校准平滑
    public float slideSlopeSmoothTime = 0.08f; // 滑铲坡面法线平滑
    int actionState = -1;           // 当前动作层状态
    Vector3 modelBasePosition;      // 模型基础位置
    Quaternion modelBaseRotation;   // 模型基础旋转
    float crouchLift;               // 当前模型抬升
    float slideTurn;                // 当前滑铲转身进度
    Transform leftToe;              // 左脚趾骨骼
    Transform rightToe;             // 右脚趾骨骼
    bool groundActive;              // 是否需要脚底校正
    bool slideActive;               // 上一帧是否滑铲
    bool slideGrounded;             // 滑铲时是否在地面
    float slideGroundOffset;        // 滑铲贴地偏移
    Vector3 slideGroundNormal = Vector3.up; // 滑铲地面法线
    Vector3 slideGroundPoint;       // 滑铲地面接触点

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
        bool crouchEnter = (state & BitCrouchEnter) != 0;
        bool peek = (state & (BitPeekLeft | BitPeekRight)) != 0;
        bool grounded = (state & BitGround) != 0;

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
        animation.SetFloat("CrouchActionSpeed", squat && walk ? 1f : 0f);

        // 奔跑满速，走路半速，静止为零，换弹只能走路
        bool run = (state & BitRun) != 0 && !reload;
        animation.SetFloat("Speed", run ? 1f : walk ? 0.5f : 0f);

        // 举枪或蹲下时启用四方向
        Vector2 move = ((state & BitGun) != 0 && !reload) || squat
            ? UnPack_Move(state) : new Vector2(0f, 1f);
        animation.SetFloat("MoveX", move.x);
        animation.SetFloat("MoveY", move.y);

        // 蹲姿和滑铲使用独立全身层
        int nextAction;
        if (slide) nextAction = 3;
        else if (crouchEnter) nextAction = 4;
        else nextAction = squat ? 2 : 0;
        if (animation.layerCount > 1)
        {
            float actionTarget = nextAction > 0 ? 1f : 0f;
            float actionTime = actionTarget > animation.GetLayerWeight(1) ? 0.18f : 0.15f;
            animation.SetLayerWeight(1, Smooth_Weight(1, actionTarget, actionTime));
            if (nextAction != actionState)
            {
                string stateName = nextAction == 3 ? "Slide"
                    : nextAction == 2 ? "CrouchMove"
                    : nextAction == 4 ? "CrouchIdle"
                    : nextAction == 1 ? "CrouchHold" : "ActionEmpty";
                float stateOffset = nextAction == 1 ? crouchHoldOffset : 0f;
                animation.CrossFadeInFixedTime(stateName, 0.12f, 1, stateOffset);
                actionState = nextAction;
            }
        }

        // 滑铲使用整身手臂动作
        if (animation.layerCount > 2)
        {
            float handTarget = slide ? 0f : 1f;
            float handTime = slide ? 0.08f : 0.15f;
            animation.SetLayerWeight(2, Smooth_Weight(2, handTarget, handTime));
        }

        // 探头层平滑淡入淡出
        if (animation.layerCount > 4)
        {
            float peekTarget = peek ? 1f : 0f;
            animation.SetLayerWeight(4, Smooth_Weight(4, peekTarget, 0.2f));
        }

        RaycastHit slideHit = default;
        bool hasSlideGround = slide && grounded && Own_Control != null
            && Own_Control.Con_body != null
            && Own_Control.Con_body.Body_Ground_Hit_Date(out slideHit);
        float slopeBlend = slideSlopeSmoothTime > 0f
            ? 1f - Mathf.Exp(-Time.deltaTime / slideSlopeSmoothTime)
            : 1f;
        Vector3 targetNormal = hasSlideGround ? slideHit.normal : Vector3.up;
        slideGroundNormal = Vector3.Slerp(slideGroundNormal, targetNormal, slopeBlend).normalized;
        if (hasSlideGround) slideGroundPoint = slideHit.point;

        // 滑铲使用固定高度，蹲姿保留脚底校正
        Vector3 modelPosition = modelBasePosition;
        groundActive = grounded && squat && !slide;
        slideGrounded = slide && grounded && hasSlideGround;
        if (slide && !slideActive) slideGroundOffset = 0f;
        if (slide)
        {
            crouchLift = Mathf.MoveTowards(crouchLift, slideModelLift + slideGroundOffset,
                Mathf.Abs(slideModelLift) * Time.deltaTime / slideLiftTime);
        }
        if (!groundActive && !slide) crouchLift = Mathf.MoveTowards(crouchLift, 0f,
            crouchModelLift * Time.deltaTime / crouchLiftFallTime);
        slideActive = slide;
        if (slide)
        {
            Vector3 localGroundNormal = animation.transform.parent != null
                ? animation.transform.parent.InverseTransformDirection(slideGroundNormal)
                : slideGroundNormal;
            modelPosition += localGroundNormal * crouchLift;
        }
        else modelPosition.y += crouchLift;
        animation.transform.localPosition = modelPosition;

        // 滑铲时脚部朝前
        float slideTurnTarget = slide ? 1f : 0f;
        if (slideTurnTime > 0f)
            slideTurn = Mathf.MoveTowards(slideTurn, slideTurnTarget,
                Time.deltaTime / slideTurnTime);
        else slideTurn = slideTurnTarget;
        Quaternion slideRotation = modelBaseRotation
            * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0f, -45f, 0f), slideTurn);
        if (slide && slideGrounded)
        {
            Vector3 localNormal = animation.transform.parent != null
                ? animation.transform.parent.InverseTransformDirection(slideGroundNormal)
                : slideGroundNormal;
            Quaternion slopeRotation = Quaternion.FromToRotation(
                slideRotation * Vector3.up, localNormal);
            slideRotation = slopeRotation * slideRotation;
        }
        animation.transform.localRotation = slideRotation;

        // 垂直速度，正升负降
        animation.SetFloat("VerticalSpeed", verticalSpeed);
    }

    // Animator更新后校正脚底
    public void Player_animation_Ground_Performance()
    {
        if (leftToe == null || rightToe == null)
        {
            Debug.LogError("Player_animation|Player_animation_Ground_Performance|脚趾骨骼为空");
            return;
        }

        // 滑铲只做低频贴地校准，避免脚部曲线逐帧反向拉扯模型
        if (slideActive && slideGrounded)
        {
            float leftFootGap = Vector3.Dot(
                leftToe.position - slideGroundPoint, slideGroundNormal);
            float rightFootGap = Vector3.Dot(
                rightToe.position - slideGroundPoint, slideGroundNormal);
            float slideFootGap = Mathf.Min(leftFootGap, rightFootGap);
            float slideError = footGroundHeight - slideFootGap;
            slideError = Mathf.Clamp(slideError, -0.25f, 0.25f);
            float blend = slideGroundSmoothTime > 0f
                ? 1f - Mathf.Exp(-Time.deltaTime / slideGroundSmoothTime)
                : 1f;
            slideGroundOffset = Mathf.Lerp(slideGroundOffset, slideError, blend);

            crouchLift = Mathf.MoveTowards(crouchLift,
                slideModelLift + slideGroundOffset, footGroundSpeed * Time.deltaTime);
            Vector3 localGroundNormal = animation.transform.parent != null
                ? animation.transform.parent.InverseTransformDirection(slideGroundNormal)
                : slideGroundNormal;
            Vector3 slidePosition = modelBasePosition + localGroundNormal * crouchLift;
            animation.transform.localPosition = slidePosition;
            return;
        }

        if (!groundActive) return;

        float minFootY = Mathf.Min(leftToe.position.y, rightToe.position.y);
        float footError = transform.root.position.y + footGroundHeight - minFootY;
        crouchLift = Mathf.Clamp(crouchLift + footError, -1.5f, 1f);

        Vector3 modelPosition = modelBasePosition;
        modelPosition.y += crouchLift;
        animation.transform.localPosition = modelPosition;
    }

    // 平滑图层权重
    float Smooth_Weight(int layer, float target, float duration)
    {
        if (duration <= 0f) return target;
        float step = Time.deltaTime / duration;
        return Mathf.MoveTowards(animation.GetLayerWeight(layer), target, step);
    }
}
