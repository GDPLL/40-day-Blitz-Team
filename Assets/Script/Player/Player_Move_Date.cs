using UnityEngine;

// 玩家移动驱动，整合位移、跳跃、翻滚与传送
public class Player_Move_Date : MonoBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control _Control) => Own_Control = _Control;

    // 移动数据
    [Header("复活倒地")]
    public float respawnSquatTime = 0.23f;      //复活倒地碰撞体保持秒
    [Header("位置纠偏")]
    public float reconcileSpeed = 5f;           //纠偏拉回速度,米每秒
    [Header("体力")]
    public Player_Stamina_Date staminaDate = new Player_Stamina_Date(); //体力数据

    public bool isOnGround;     //在地面
    public bool isWeightLess;   //失重中
    public bool isWASDDowm;     //移动输入
    public bool isRuning;       //奔跑输入
    public bool isSquat;        //蹲下输入
    public bool isJumpDown;     //跳跃空格
    public bool isRollDown;     //翻滚输入
    public float rollTimer;     //翻滚剩余时间
    public bool isPeekLeft;     //左探头
    public bool isPeekRight;    //右探头
    public float slideTimer;    //滑铲剩余时间
    public float slideTime = 1.77f;    //滑铲时长
    public float slideExitTime = 0.5f; //滑铲起身过渡
    public float slideJumpWindow = 0.22f; //滑铲末段跳跃窗口
    public float slideJumpBufferTime = 0.12f; //滑铲跳跃输入缓存

    bool jumpRequest;           //本物理帧请求起跳
    bool rollRequest;           //本物理帧请求翻滚
    bool rollRequestAir;        //翻滚请求时空中
    bool slideRequest;          //本物理帧请求滑铲
    bool slideRequestAir;       //滑铲请求时空中
    bool slideBefore;           //滑铲按键上一帧状态
    float rollDelay;            //翻滚施力延迟
    float rollPhysicsTimer;     //翻滚物理剩余时间
    Vector2 rollDir;            //翻滚方向
    bool rollPending;           //空中翻滚待落地
    bool rollHalf;              //翻滚后半段
    bool slidePending;          //空中滑铲待落地
    float bodyScale = 1f;       //碰撞体当前比例
    float respawnSquatTimer;    //复活倒地保持计时
    float slideExitTimer;       //起身过渡剩余时间
    float slideExitStartSpeed;  //起身初始速度
    float slideExitTargetSpeed; //起身目标速度
    float slideJumpBufferTimer; //滑铲跳跃缓存剩余时间

    // 瞬移优先级
    public const int Priority_Reconcile = 0;   //纠偏
    public const int Priority_Scene = 1;       //场景
    public const int Priority_Respawn = 2;     //复活

    // 瞬移申请
    Vector3 requestPos;         //申请位置
    Quaternion requestRot;      //申请旋转
    int requestPriority;        //申请优先级
    bool hasRequest;            //有待处理申请

    public bool Teleported { get; private set; }   //本物理帧是否瞬移
    public bool Is_Slide => slideTimer > 0f || slideRequest; //是否滑铲
    public bool Is_Roll_Active => rollTimer > 0f || rollPending; //是否翻滚占用
    public bool Is_Roll_Push => rollTimer > 0f && rollDelay <= 0f; //是否二段翻滚
    public bool Is_Roll_Half => rollTimer > 0f && rollHalf; //是否后半段
    public bool Is_Stamina_Action =>
        Is_Roll_Active || slideTimer > 0f || slideExitTimer > 0f || slidePending; //是否动作消耗

    // 瞬移申请，高优先级覆盖低优先级
    public void Teleport_Request(Vector3 position, Quaternion rotation, int priority)
    {
        if (hasRequest && priority < requestPriority) return;   //低优先级不覆盖

        requestPos = position;
        requestRot = rotation;
        requestPriority = priority;
        hasRequest = true;
    }

    // 帧内仲裁并执行申请，返回本物理帧是否瞬移
    public bool Teleport_Resolve()
    {
        Teleported = hasRequest;

        if (!hasRequest) return false;

        hasRequest = false;
        Apply_Teleport(requestPos, requestRot);
        return true;
    }

    // 执行瞬移，刚体一起搬，防物理回写
    void Apply_Teleport(Vector3 position, Quaternion rotation)
    {
        Player_Control c = Own_Control;

        c.transform.position = position;
        c.transform.rotation = rotation;

        if (c.rigidbody != null)
        {
            c.rigidbody.position = position;      //非运动学刚体归物理管
            c.rigidbody.rotation = rotation;
            c.rigidbody.velocity = Vector3.zero;
            c.rigidbody.angularVelocity = Vector3.zero;
        }

        // 瞬移后清坠落基准，避免误判坠落伤害
        if (c.Con_ObjectSystem != null) c.Con_ObjectSystem.Fall_Speed_Clear();
    }

    // 平滑移动到指定位置，走物理接口
    public void Body_Position_Date(Vector3 position)
    {
        Player_Control c = Own_Control;

        if (c.rigidbody != null) c.rigidbody.MovePosition(position);
        else c.transform.position = position;
    }

    // 执行纠偏，只负责平滑计算
    public void Reconcile_Move_Date(Vector3 target)
    {
        Player_Control c = Own_Control;

        Vector3 current = c.transform.position;
        Vector3 offset = target - current;

        Vector3 next = current + offset.normalized
            * Mathf.Min(reconcileSpeed * Time.fixedDeltaTime, offset.magnitude);

        Body_Position_Date(next);
    }

    // 复活重置，位置走瞬移申请
    public void Reset_State(Vector3 position, Quaternion rotation)
    {
        // 复活重置翻滚，碰撞体保持倒地
        rollTimer = 0f;
        rollDelay = 0f;
        rollPhysicsTimer = 0f;
        rollPending = false;
        rollHalf = false;
        rollRequestAir = false;
        slideTimer = 0f;
        slideExitTimer = 0f;
        slideRequest = false;
        slideRequestAir = false;
        slidePending = false;
        slideBefore = false;
        slideJumpBufferTimer = 0f;
        staminaDate.Reset_Date();
        isPeekLeft = false;
        isPeekRight = false;
        respawnSquatTimer = respawnSquatTime;

        Player_Body body = Own_Control.Con_body;
        if (body != null)
        {
            bodyScale = body.deadSquatScale;
            body.Body_Size_Date(bodyScale);
        }

        // 申请瞬移，最高优先级
        Teleport_Request(position, rotation, Priority_Respawn);
    }

    // 落地检测并缓存
    public void Ground_Date()
    {
        Player_Body body = Own_Control != null ? Own_Control.Con_body : null;
        if (body == null)
        {
            Debug.LogError("Player_Move_Date|Ground_Date|未找到 Player_Body");
            isOnGround = false;
            return;
        }

        isOnGround = body.Body_Ground_Date();
    }

    // 落地检测
    public bool IsGrounded()
    {
        Player_Body body = Own_Control != null ? Own_Control.Con_body : null;
        if (body == null)
        {
            Debug.LogError("Player_Move_Date|IsGrounded|未找到 Player_Body");
            return false;
        }

        return body.Body_Ground_Date();
    }

    // 主机每物理帧移动驱动
    public void Move_Host_Fixed(InputPacket packet, bool reload)
    {
        Move_Core(packet, reload);

        Player_Control c = Own_Control;
        Player_Body body = c.Con_body;
        Rigidbody rb = c.rigidbody;
        if (body == null || rb == null || c.Con_ObjectSystem == null) return;

        // 坠落伤害，按空中最大下落速度算
        c.Con_ObjectSystem.Fall_Damage_Date(rb.velocity.y, body.IsGrounded,
            Time.fixedDeltaTime, Is_Roll_Active);
    }

    // 客机本机物理帧移动驱动
    public void Move_ClientOwn_Fixed_Predict(InputPacket packet, bool reload)
    {
        Move_Core(packet, reload);

        Player_Control c = Own_Control;
        Player_Body body = c.Con_body;
        if (body == null) return;

        // 客机落地取执行器结果
        isOnGround = body.IsGrounded;

        Rigidbody rb = c.rigidbody;
        if (rb == null) return;

        // 空中超致死速度算失重
        if (c.Con_ObjectSystem != null)
            isWeightLess = c.Con_ObjectSystem.WeightLess_Date(rb.velocity.magnitude, isOnGround);

        // 本机坠地音效
        if (c.Con_player_Audio != null)
            c.Con_player_Audio.Land_Check(rb.velocity.y, isOnGround);
    }

    // 移动核心，主机与预测共用
    void Move_Core(InputPacket packet, bool reload)
    {
        Player_Control c = Own_Control;
        Player_Body body = c.Con_body;
        if (body == null) return;

        body.Body_Move_Date(packet.move, packet.viewDir, isRuning && !reload, isSquat);   //换弹只能走路

        float speedOverride = 0f;
        if (slideExitTimer > 0f && slideExitTime > 0f)
        {
            float progress = 1f - slideExitTimer / slideExitTime;
            speedOverride = Mathf.Lerp(slideExitStartSpeed, slideExitTargetSpeed,
                Mathf.Clamp01(progress));
        }

        body.Body_Fixed_Date(rollPhysicsTimer > 0f, slideTimer > 0f, speedOverride);   //动作期间保留速度

        if (slideJumpBufferTimer > 0f
            && slideTimer > slideJumpWindow + Time.fixedDeltaTime)
        {
            slideJumpBufferTimer -= Time.fixedDeltaTime;
            if (slideJumpBufferTimer <= 0f) slideJumpBufferTimer = 0f;
        }

        // 地面动作离地结束
        if (!body.IsGrounded && !rollPending && !slidePending
            && (rollTimer > 0f || slideTimer > 0f || slideExitTimer > 0f))
        {
            rollDelay = 0f;
            rollPhysicsTimer = 0f;
            rollTimer = 0f;
            rollHalf = false;
            slideTimer = 0f;
            slideExitTimer = 0f;
            slideJumpBufferTimer = 0f;
            body.Body_Air_Action_Exit_Date(body.runSpeed);
        }

        // 滑铲末段起跳
        if ((slideJumpBufferTimer > 0f || jumpRequest) && slideTimer > 0f
            && slideJumpWindow > 0f && slideTimer <= slideJumpWindow
            && body.IsGrounded && !Is_Roll_Active && !slidePending)
        {
            jumpRequest = false;
            slideJumpBufferTimer = 0f;
            slideTimer = 0f;
            slideExitTimer = 0f;
            slideRequest = false;
            slideRequestAir = false;
            slidePending = false;
            bodyScale = 1f;
            body.Body_Size_Date(bodyScale);
            body.Body_Jump_Date();
            if (c.IsOwner && c.Con_player_Audio != null) c.Con_player_Audio.Jump_Show();
        }
        // 起跳，每物理帧最多一次
        else if (jumpRequest)
        {
            jumpRequest = false;
            if (slideTimer <= 0f
                && !Is_Roll_Active && !slidePending)
            {
                body.Body_Jump_Date();
                if (c.IsOwner && c.Con_player_Audio != null) c.Con_player_Audio.Jump_Show();
            }
        }

        // 翻滚，每物理帧最多一次，翻滚中不重复
        if (rollRequest)
        {
            rollRequest = false;

            if (!Is_Roll_Active && slideTimer <= 0f && !slidePending
                && staminaDate.Try_Spend(staminaDate.rollStaminaCost))
            {
                bool requestAir = rollRequestAir || !body.IsGrounded;
                rollRequestAir = false;
                slideRequest = false;
                slideRequestAir = false;
                slidePending = false;
                rollDir = packet.move;                      //记下方向，延迟后用
                rollDelay = 0f;
                rollHalf = false;

                if (!requestAir)
                {
                    rollDelay = body.rollDelay;             //第二段延迟
                    rollTimer = body.rollTime + rollDelay;
                    rollPhysicsTimer = rollTimer;
                    body.Body_Roll_Date(rollDir, packet.viewDir, -body.rollBrake);   //起手减速
                    if (c.IsOwner && c.Con_player_Audio != null) c.Con_player_Audio.Roll_Show();
                }
                else rollPending = true;
            }
            else rollRequestAir = false;
        }

        // 空中翻滚落地启动
        if (rollPending && body.IsGrounded)
        {
            rollPending = false;
            rollHalf = true;
            rollTimer = c.Con_player_Animation != null
                ? c.Con_player_Animation.Roll_Half_Time : body.rollTime;
            rollPhysicsTimer = body.rollTime;
            body.Body_Roll_Date(rollDir, packet.viewDir, body.rollForce);
            if (c.IsOwner && c.Con_player_Audio != null) c.Con_player_Audio.Roll_Show();
        }

        // 翻滚优先执行滑铲
        if (slideRequest)
        {
            slideRequest = false;
            bool requestAir = slideRequestAir || !body.IsGrounded;
            slideRequestAir = false;
            bool canStart = slideTimer <= 0f && !Is_Roll_Active
                && (requestAir || body.IsGrounded);

            if (canStart && staminaDate.Try_Spend(staminaDate.slideStaminaCost))
            {
                if (requestAir) slidePending = true;
                else
                {
                    slidePending = false;
                    slideTimer = slideTime;
                    slideExitTimer = 0f;
                    slideJumpBufferTimer = 0f;
                    body.Body_Slide_Date(packet.move, packet.viewDir, body.GroundNormal);
                }
            }
        }

        // 空中滑铲落地启动
        if (slidePending && body.IsGrounded)
        {
            slidePending = false;
            if (slideTimer <= 0f && !Is_Roll_Active)
            {
                slideTimer = slideTime;
                slideExitTimer = 0f;
                slideJumpBufferTimer = 0f;
                body.Body_Slide_Date(packet.move, packet.viewDir, body.GroundNormal);
            }
        }

        // 延迟到点再施一次力
        if (rollDelay > 0f)
        {
            rollDelay -= Time.fixedDeltaTime;
            if (rollDelay <= 0f)
                body.Body_Roll_Date(rollDir, packet.viewDir, body.rollForce);   //第二段爆发
        }

        // 翻滚计时
        if (rollTimer > 0f)
        {
            rollTimer -= Time.fixedDeltaTime;
            if (rollTimer <= 0f)
            {
                rollTimer = 0f;
                rollHalf = false;
            }
        }

        // 翻滚物理计时
        if (rollPhysicsTimer > 0f)
        {
            rollPhysicsTimer -= Time.fixedDeltaTime;
            if (rollPhysicsTimer <= 0f) rollPhysicsTimer = 0f;
        }

        // 滑铲计时
        if (slideTimer > 0f)
        {
            slideTimer -= Time.fixedDeltaTime;
            if (slideTimer <= 0f)
            {
                slideTimer = 0f;
                Slide_Exit_Date(packet);
            }
        }

        if (slideExitTimer > 0f)
        {
            slideExitTimer -= Time.fixedDeltaTime;
            if (slideExitTimer <= 0f) slideExitTimer = 0f;
        }
        // 输入施推力
        if (isWASDDowm && rollPhysicsTimer <= 0f && slideTimer <= 0f)
            body.Body_Move();

        staminaDate.Recover_Date(Time.fixedDeltaTime, Is_Stamina_Action,
            body.IsGrounded, isRuning);
    }

    // 计算滑铲起身惯性
    void Slide_Exit_Date(InputPacket packet)
    {
        Player_Control c = Own_Control;
        Player_Body body = c.Con_body;
        Rigidbody rb = c.rigidbody;
        if (body == null || rb == null || slideExitTime <= 0f) return;

        Vector3 normal = body.IsGrounded ? body.GroundNormal : Vector3.up;
        float normalSpeed = Vector3.Dot(rb.velocity, normal);
        float tangentSpeed = (rb.velocity - normal * normalSpeed).magnitude;
        bool moveHeld = packet.move.sqrMagnitude >= 0.01f;
        bool runExit = moveHeld && isRuning && !isSquat;

        slideExitStartSpeed = tangentSpeed;
        if (!moveHeld) slideExitTargetSpeed = 0f;
        else if (runExit) slideExitTargetSpeed = body.runSpeed;
        else if (isSquat) slideExitTargetSpeed = body.squatSpeed;
        else slideExitTargetSpeed = body.walkSpeed;
        slideExitTimer = slideExitTime;
    }

    // 翻滚死亡失重共用碰撞体缩放
    public void Body_Size_Date(bool dead)
    {
        Player_Body body = Own_Control.Con_body;
        if (body == null) return;

        // 翻滚用翻滚比例，死亡失重倒地取更小比例
        float scale = 1f;
        if (slideTimer > 0f) scale = body.slideSquatScale;
        if (rollTimer > 0f) scale = body.rollSquatScale;
        if (dead || isWeightLess || respawnSquatTimer > 0f) scale = body.deadSquatScale;

        // 复活倒地保持计时
        if (respawnSquatTimer > 0f) respawnSquatTimer -= Time.fixedDeltaTime;

        if (bodyScale == scale) return;    //比例没变

        bodyScale = scale;
        body.Body_Size_Date(scale);
    }

    // 输入轮询，由Control每帧驱动
    public void Move_Update()
    {
        Player_Control c = Own_Control;
        if (c == null) return;
        if (!c.IsServer && !c.IsOwner) return;      //客机只看别人表现
        if (c.Con_player_HostNetworkEvent == null) return;

        // 主机取网络包，客机本机取本地包
        InputPacket packet = c.IsServer
            ? c.Con_player_HostNetworkEvent.Packet
            : (c.Con_input_Manage != null ? c.Con_input_Manage.LastPacket : default);

        // 举枪状态，只看本帧输入
        bool aimDown = packet.mouse1 || packet.mouse2 || packet.shoulder || packet.ads;

        // 移动输入
        isWASDDowm = packet.move.sqrMagnitude >= 0.01f;

        // 举枪朝视角转，否则移动转向
        if (aimDown)
        {
            if (c.Con_body != null) c.Con_body.Body_Aim_Performance(packet.viewDir);
        }
        else if (isWASDDowm && isOnGround && c.Con_body != null)
            c.Con_body.Body_Rotation_Performance();

        // 翻滚动画结束前屏蔽蹲下
        bool rolling = Is_Roll_Active
            || (c.Con_player_Animation != null && c.Con_player_Animation.Is_Rolling);

        // 奔跑与蹲下
        isRuning = packet.run;
        isSquat = packet.squat && !rolling;

        // 滑铲按键边沿
        bool slideKeys = isRuning && packet.squat && isWASDDowm;
        if (slideKeys && !slideBefore && !rolling)
        {
            slideRequest = true;
            slideRequestAir = !isOnGround;
        }
        slideBefore = slideKeys;

        // 探头互斥
        isPeekLeft = packet.peekLeft && !packet.peekRight && slideTimer <= 0f;
        isPeekRight = packet.peekRight && !packet.peekLeft && slideTimer <= 0f;

        // 跳跃，只记按下边沿
        if (packet.jump && !isJumpDown)
        {
            jumpRequest = true;
            if (slideTimer > 0f && slideJumpWindow > 0f
                && slideTimer <= slideJumpWindow + slideJumpBufferTime)
                slideJumpBufferTimer = slideJumpBufferTime;
        }
        isJumpDown = packet.jump;

        // 翻滚，只记按下边沿
        if (packet.roll && !isRollDown)
        {
            rollRequest = true;
            rollRequestAir = !isOnGround;
        }
        isRollDown = packet.roll;
    }
}
