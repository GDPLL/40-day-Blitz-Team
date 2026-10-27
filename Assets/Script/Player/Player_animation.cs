using UnityEngine;

// 玩家动画表现，状态位驱动
public class Player_animation : MonoBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control)
    {
        if (control == null)
        {
            Debug.LogError("Player_animation|Owner_Init|玩家控制为空");
            return;
        }

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
        float rollClipLength = 0f;
        // 蹲走动画强制循环
        foreach (AnimationClip clip in animation.runtimeAnimatorController.animationClips)
        {
            if (clip.name.Contains("squat"))
                clip.wrapMode = WrapMode.Loop;
            else if (clip.name == "crouch2")
                clip.wrapMode = WrapMode.ClampForever;
            if (clip.name == "Armature|fangun")
                rollClipLength = clip.length;
        }

        Player_Body body = control.Con_body;
        if (rollClipLength <= 0f || body == null)
        {
            Debug.LogError("Player_animation|Owner_Init|翻滚动画数据为空");
            return;
        }
        rollHalfStart = Mathf.Clamp01(body.rollDelay / rollClipLength);
        rollHalfTime = Mathf.Max(0f, rollClipLength - body.rollDelay);
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
    public const uint BitRollHalf = 1u << 17;    //翻滚后半段
    public const uint BitWeightLess = 1u << 12;  //失重
    public const uint BitReload = 1u << 13;      //换弹
    public const uint BitSlide = 1u << 14;       //滑铲
    public const uint BitPeekLeft = 1u << 15;    //左探头
    public const uint BitPeekRight = 1u << 16;   //右探头

    const int MoveXShift = 8;                   //方向X位偏移
    const int MoveYShift = 10;                  //方向Y位偏移
    const float RollFootBlendDistance = 0.3f;   //翻滚脚底渐出距离

    public new Animator animation;   // 动画组件
    public float crouchModelLift = 0.19f;   // 蹲姿模型抬升
    public float crouchLiftRiseTime = 0.82f;   // 蹲姿抬升时长
    public float crouchLiftFallTime = 0.15f;   // 蹲姿恢复时长
    public float crouchHoldOffset = 2.44f;    // 蹲姿末帧时间
    public float crouchActionSmoothTime = 0.32f; // 蹲走停步平滑
    public float slideTurnTime = 0.12f;       // 滑铲转身时长
    public float footGroundHeight = 0.02f;    // 脚底离角色根高度
    public float footGroundSpeed = 12f;       // 脚底修正速度
    public float slideModelLift = -0.6f;       // 滑铲模型高度
    public float slideLiftTime = 0.2f;         // 滑铲高度调整时长
    public float slideGroundSmoothTime = 0.12f; // 滑铲贴地校准平滑
    public float slideSlopeSmoothTime = 0.08f; // 滑铲坡面法线平滑
    public AnimationCurve rollLiftCurve = new AnimationCurve(
        new Keyframe(0f, -0.2f),
        new Keyframe(0.15f, -0.35f),
        new Keyframe(0.5f, -0.8f),
        new Keyframe(0.85f, -0.35f),
        new Keyframe(1f, -0.2f));          // 翻滚模型高度
    public float rollSlopeSmoothTime = 0.08f; // 翻滚坡面平滑
    int actionState = -1;           // 当前动作层状态
    Vector3 modelBasePosition;      // 模型基础位置
    Quaternion modelBaseRotation;   // 模型基础旋转
    float crouchLift;               // 当前模型抬升
    float crouchActionSpeed;        // 当前蹲走速度
    float crouchActionVelocity;     // 蹲走速度缓存
    float slideTurn;                // 当前滑铲转身进度
    Transform leftToe;              // 左脚趾骨骼
    Transform rightToe;             // 右脚趾骨骼
    bool groundActive;              // 是否需要脚底校正
    bool slideActive;               // 上一帧是否滑铲
    bool slideGrounded;             // 滑铲时是否在地面
    bool rollActive;                // 翻滚动作是否播放
    bool rollGrounded;              // 翻滚时是否在地面
    bool rollHalfShown;             // 是否已定位后半段
    float rollHalfStart;            // 翻滚后半起点
    float rollHalfTime;             // 翻滚后半时长
    float slideGroundOffset;        // 滑铲贴地偏移
    Vector3 slideGroundNormal = Vector3.up; // 滑铲地面法线
    Vector3 slideGroundPoint;       // 滑铲地面接触点
    Vector3 rollGroundNormal = Vector3.up; // 翻滚地面法线
    Vector3 rollGroundPoint;        // 翻滚地面接触点

    // 翻滚动画是否仍在播放
    public bool Is_Rolling
    {
        get
        {
            int stateHash = animation.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return stateHash == Animator.StringToHash("翻滚")
                || stateHash == Animator.StringToHash("翻滚后半段");
        }
    }

    public float Roll_Half_Time => rollHalfTime; //翻滚后半时长

    // 按动画进度取翻滚高度
    float Roll_Lift_Date(float progress)
    {
        if (rollLiftCurve == null)
        {
            Debug.LogError("Player_animation|Roll_Lift_Date|翻滚高度曲线为空");
            return 0f;
        }
        return rollLiftCurve.Evaluate(Mathf.Clamp01(progress));
    }

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
        bool roll = (state & BitRoll) != 0;
        bool rollHalf = (state & BitRollHalf) != 0;
        bool rolling = roll || Is_Rolling;
        bool squat = (state & BitSquat) != 0 && !rolling;
        bool slide = (state & BitSlide) != 0 && !rolling;
        bool peek = (state & (BitPeekLeft | BitPeekRight)) != 0;
        bool grounded = (state & BitGround) != 0;

        animation.SetBool("walk", walk);
        animation.SetBool("raiseGun", (state & BitGun) != 0);
        animation.SetBool("jump", (state & BitJump) != 0);
        animation.SetBool("IsGround", (state & BitGround) != 0);
        animation.SetBool("IsDeath", (state & BitDeath) != 0);
        animation.SetBool("IsRoll", roll && !rollHalf);
        animation.SetBool("IsRollHalf", rollHalf);
        if (rollHalf && !rollHalfShown)
            animation.Play("翻滚后半段", 0, rollHalfStart);
        rollHalfShown = rollHalf;
        animation.SetBool("IsWeightLess", (state & BitWeightLess) != 0);
        animation.SetBool("IsReload", reload);
        animation.SetBool("Squat", squat);
        animation.SetBool("IsSlide", slide);
        animation.SetBool("PeekLeft", (state & BitPeekLeft) != 0);
        animation.SetBool("PeekRight", (state & BitPeekRight) != 0);
        float crouchTarget = squat && walk ? 1f : 0f;
        crouchActionSpeed = crouchActionSmoothTime > 0f
            ? Mathf.SmoothDamp(crouchActionSpeed, crouchTarget,
                ref crouchActionVelocity, crouchActionSmoothTime)
            : crouchTarget;
        animation.SetFloat("CrouchActionSpeed", crouchActionSpeed);

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

        RaycastHit rollHit = default;
        bool hasRollGround = rolling && grounded && Own_Control != null
            && Own_Control.Con_body != null
            && Own_Control.Con_body.Body_Ground_Hit_Date(out rollHit);
        float rollBlend = rollSlopeSmoothTime > 0f
            ? 1f - Mathf.Exp(-Time.deltaTime / rollSlopeSmoothTime)
            : 1f;
        Vector3 rollTargetNormal = hasRollGround ? rollHit.normal : Vector3.up;
        rollGroundNormal = Vector3.Slerp(rollGroundNormal,
            rollTargetNormal, rollBlend).normalized;
        if (hasRollGround) rollGroundPoint = rollHit.point;
        // 滑铲使用固定高度，蹲姿保留脚底校正
        Vector3 modelPosition = modelBasePosition;
        groundActive = grounded && squat && !slide;
        rollActive = rolling;
        rollGrounded = rolling && grounded && hasRollGround;
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
        if (rollActive)
        {
            Vector3 localGroundNormal = animation.transform.parent != null
                ? animation.transform.parent.InverseTransformDirection(rollGroundNormal)
                : rollGroundNormal;
            float rollProgress = animation.GetCurrentAnimatorStateInfo(0).normalizedTime;
            modelPosition += localGroundNormal * Roll_Lift_Date(rollProgress);
        }
        else if (slide)
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
        if (rollActive && rollGrounded)
        {
            Vector3 localNormal = animation.transform.parent != null
                ? animation.transform.parent.InverseTransformDirection(rollGroundNormal)
                : rollGroundNormal;
            Quaternion slopeRotation = Quaternion.FromToRotation(
                slideRotation * Vector3.up, localNormal);
            slideRotation = slopeRotation * slideRotation;
        }
        else if (slide && slideGrounded)
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

        if (rollActive)
        {
            Vector3 localGroundNormal = animation.transform.parent != null
                ? animation.transform.parent.InverseTransformDirection(rollGroundNormal)
                : rollGroundNormal;
            float rollProgress = animation.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Vector3 rollPosition =
                modelBasePosition + localGroundNormal * Roll_Lift_Date(rollProgress);

            if (rollGrounded)
            {
                float leftFootGap = Vector3.Dot(
                    leftToe.position - rollGroundPoint, rollGroundNormal);
                float rightFootGap = Vector3.Dot(
                    rightToe.position - rollGroundPoint, rollGroundNormal);
                float minFootGap = Mathf.Min(leftFootGap, rightFootGap);
                float footBlend = 1f - Mathf.InverseLerp(
                    footGroundHeight + 0.08f,
                    footGroundHeight + RollFootBlendDistance,
                    minFootGap);
                float rollFootError = footGroundHeight - minFootGap;
                rollPosition += localGroundNormal * rollFootError * footBlend;
            }

            animation.transform.localPosition = rollPosition;

            Quaternion rollRotation = modelBaseRotation;
            if (rollGrounded)
            {
                Quaternion slopeRotation = Quaternion.FromToRotation(
                    rollRotation * Vector3.up, localGroundNormal);
                rollRotation = slopeRotation * rollRotation;
            }
            animation.transform.localRotation = rollRotation;
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
