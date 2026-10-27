using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 玩家本体控制，数据由 Player_Control 注入
public class Player_Body : Character_Move
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    // 移动数据
    Vector3 direction = new Vector3(0, 0, 0);       // 目标移动方向
    Vector3 currentMoveDirection = Vector3.zero;    // 实际移动方向
    float currentForce = 0f;                        // 当前推力
    public float walkForce = 3000f;                 // 走路推力
    public float runForce = 9000f;                  // 跑步推力
    public float squatForce = 2400f;                // 蹲走推力
    float airForce = 0.1f;                  // 空中推力比例
    public float walkInterval = 0.2f;               // 走路施力间隔秒
    public float runInterval = 0.14f;               // 跑步施力间隔秒
    public float squatInterval = 0.2f;              // 蹲走施力间隔秒
    float moveTimer;                                // 施力计时
    float moveInterval;                             // 当前施力间隔
    bool hasInput;                                  // 本帧有移动输入

    // 跳跃与落地
    public float jumpForce = 250f;                  // 跳跃力
    public LayerMask groundMask = ~0;               // 地面检测层
    public float groundCheckDistance = 0.2f;        // 落地检测距离
    public float maxSlopeAngle = 50f;               // 最大可站立坡度
    public float groundStopAccel = 30f;             // 地面停止减速度
    public Rigidbody rb;                            // 刚体

    // 翻滚
    public float rollBrake = 400f;          // 翻滚起手减速冲量
    public float rollForce = 1500f;         // 翻滚爆发冲量
    public float rollSpeed = 15f;           // 翻滚最大速度
    public float rollDelay = 0.8991f;       // 翻滚施力延迟秒
    public float rollTime = 0.5f;           // 翻滚时长
    public float rollSquatScale = 0.5f;     // 翻滚碰撞体比例
    public float slideSpeed = 10f;          // 滑铲最大速度
    public float slideForce = 900f;         // 滑铲推力
    public float slideUphillLoss = 0.5f;    // 上坡速度损失
    public float slideDownhillBonus = 2.5f; // 下坡速度加成
    public float slideMaintainAccel = 30f;  // 滑铲维持加速度
    public float slideSquatScale = 0.5f;    // 滑铲碰撞体比例
    public float deadSquatScale = 0.23f;    // 死亡失重碰撞体比例
    Collider bodyCol;                       // 玩家碰撞体
    Vector3 colCenter;                      // 碰撞体原始中心
    float colHeight;                        // 碰撞体原始高度
    Vector3 groundNormal = Vector3.up;      // 当前地面法线
    Vector3 groundPoint;                    // 当前地面接触点
    bool jumpAirborne;                      // 是否处于起跳腾空
    bool sceneLift;                         // 上一帧场景托举标记

    public bool IsGrounded { get; private set; }    // 是否在地面
    public Vector3 GroundNormal => groundNormal;    // 地面法线

    // 场景托举时允许保留向上速度，标记在下一物理帧自动消费
    public void Body_Scene_Lift_Date() => sceneLift = true;

    // 初始化，刚体由外部注入
    public void Body_Init(Rigidbody rigidbody)
    {
        rb = rigidbody;
        if (rb != null)
        {
            rb.freezeRotation = true;    // 防碰撞翻滚
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            turnBody = rb;
        }

        // 缓存碰撞体原始尺寸
        bodyCol = GetComponent<Collider>();
        if (bodyCol is CapsuleCollider cap)
        {
            colCenter = cap.center;
            colHeight = cap.height;
        }

        currentMoveDirection = transform.forward;    // 平滑转向初值

        Debug.Log("Player_Body|Body_Init|完成初始化");
    }

    // 按输入轴与视角算移动，axis为输入轴
    public void Body_Move_Date(Vector2 axis, Vector3 viewDir, bool run, bool squat)
    {
        // 视角换算世界方向
        direction = GetMoveDir(axis, viewDir);

        // 输入边沿，供施力节奏使用
        bool wasInput = hasInput;
        hasInput = axis.sqrMagnitude > 0.01f;    //本帧有无移动输入

        // 方向即时对齐输入，不做转向滞后
        if (direction.sqrMagnitude > 0.001f) currentMoveDirection = direction;

        // 速度模式
        currentForce = walkForce;
        currentSpeed = walkSpeed;
        moveInterval = walkInterval;
        if (run && !squat)
        {
            currentForce = runForce;
            currentSpeed = runSpeed;
            moveInterval = runInterval;
        }
        if (squat)
        {
            currentForce = squatForce;
            currentSpeed = squatSpeed;
            moveInterval = squatInterval;
        }

        // 停止输入重置计时，重新输入立刻施力
        if (!hasInput) moveTimer = 0f;
        else if (!wasInput) moveTimer = moveInterval;
    }

    // 视角水平方向换算移动方向
    Vector3 GetMoveDir(Vector2 axis, Vector3 viewDir)
    {
        Vector3 forward = viewDir; forward.y = 0f; forward.Normalize();
        if (forward.sqrMagnitude <= 0.001f) forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return (axis.x * right + axis.y * forward).normalized;
    }

    // 物理帧逻辑，落地检测与限速
    public void Body_Fixed_Date(bool rolling, bool sliding, float speedOverride = 0f)
    {
        Body_Ground_Date();
        bool slopeGrounded = IsGrounded && groundNormal.y < 0.9995f;
        bool liftedByScene = sceneLift;
        sceneLift = false;

        if (!rolling && !sliding && !liftedByScene && !IsGrounded && !jumpAirborne && rb.velocity.y > 0f)
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        // 松开方向立刻刹停，翻滚与空中不刹
        if (!rolling && !sliding && !hasInput && IsGrounded && speedOverride <= 0f)
        {
            if (!slopeGrounded)
            {
                rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            }
            else
            {
                float normalSpeed = Vector3.Dot(rb.velocity, groundNormal);
                Vector3 tangentVelocity =
                    rb.velocity - groundNormal * normalSpeed;
                tangentVelocity = Vector3.MoveTowards(tangentVelocity,
                    Vector3.zero, groundStopAccel * Time.fixedDeltaTime);
                rb.velocity = tangentVelocity + groundNormal * normalSpeed;
            }
            return;
        }

        // 地面移动：速度严格沿移动方向，去掉侧向滑步并完成限速
        if (!rolling && !sliding && IsGrounded)
        {
            float limitSpeed = speedOverride > 0f ? speedOverride : currentSpeed;
            if (slopeGrounded)
            {
                float normalSpeed = Vector3.Dot(rb.velocity, groundNormal);
                if (normalSpeed > 0f)
                    rb.velocity -= groundNormal * normalSpeed;
                Limit_Slide_Speed(limitSpeed);
                return;
            }

            float along = Mathf.Clamp(Vector3.Dot(rb.velocity, currentMoveDirection), 0f, limitSpeed);
            rb.velocity = currentMoveDirection * along + Vector3.up * rb.velocity.y;
            return;
        }

        // 翻滚中限到翻滚速度
        if (rolling) Limit_Horizontal_Speed(rollSpeed);
        else if (sliding) Slope_Speed_Date(slideSpeed, slideUphillLoss,
            slideDownhillBonus, slideMaintainAccel);
    }

    // 当前移动方向投影到坡面
    Vector3 Ground_Move_Direction(Vector3 normal)
    {
        Vector3 moveDirection = Vector3.ProjectOnPlane(currentMoveDirection, normal);
        return moveDirection.sqrMagnitude > 0.001f
            ? moveDirection.normalized : currentMoveDirection;
    }

    // 限水平速度，limit为上限
    void Limit_Horizontal_Speed(float limit)
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (horizontalVelocity.magnitude <= limit) return;

        Vector3 limitedHorizontal = horizontalVelocity.normalized * limit;
        rb.velocity = new Vector3(limitedHorizontal.x, rb.velocity.y, limitedHorizontal.z);
    }

    // 限斜面切线速度，limit为上限
    void Limit_Slide_Speed(float limit)
    {
        if (!IsGrounded)
        {
            Limit_Horizontal_Speed(limit);
            return;
        }

        float normalSpeed = Vector3.Dot(rb.velocity, groundNormal);
        Vector3 tangentVelocity = rb.velocity - groundNormal * normalSpeed;
        if (tangentVelocity.magnitude <= limit) return;

        rb.velocity = tangentVelocity.normalized * limit + groundNormal * normalSpeed;
    }

    // 按坡向维持动作速度
    void Slope_Speed_Date(float baseSpeed, float uphillLoss,
        float downhillBonus, float maintainAccel)
    {
        if (!IsGrounded)
        {
            Limit_Horizontal_Speed(baseSpeed);
            return;
        }

        float normalSpeed = Vector3.Dot(rb.velocity, groundNormal);
        Vector3 tangentVelocity = rb.velocity - groundNormal * normalSpeed;
        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, groundNormal);
        Vector3 moveDirection = tangentVelocity.sqrMagnitude > 0.001f
            ? tangentVelocity.normalized : Ground_Move_Direction(groundNormal);

        float slopeAmount = downhill.sqrMagnitude > 0.001f
            ? Vector3.Dot(moveDirection, downhill.normalized) : 0f;
        float slopeSpeed = slopeAmount >= 0f
            ? slopeAmount * downhillBonus
            : slopeAmount * uphillLoss;
        float targetSpeed = Mathf.Max(0f, baseSpeed + slopeSpeed);
        float tangentSpeed = Vector3.Dot(tangentVelocity, moveDirection);

        if (tangentSpeed < targetSpeed)
        {
            float acceleration = Mathf.Min(maintainAccel,
                (targetSpeed - tangentSpeed) / Time.fixedDeltaTime);
            rb.AddForce(moveDirection * acceleration, ForceMode.Acceleration);
        }
        else if (tangentVelocity.magnitude > targetSpeed)
        {
            rb.velocity = tangentVelocity.normalized * targetSpeed
                + groundNormal * normalSpeed;
        }
    }

    // 检测并缓存地面，返回是否落地
    public bool Body_Ground_Date()
    {
        if (jumpAirborne)
        {
            if (rb != null && rb.velocity.y > 0.05f)
            {
                IsGrounded = false;
                return false;
            }
            jumpAirborne = false;
        }

        if (Body_Ground_Hit_Date(out RaycastHit hit))
        {
            groundNormal = hit.normal;
            groundPoint = hit.point;
            IsGrounded = true;
            return true;
        }

        IsGrounded = false;
        return false;
    }

    // 查询地面接触信息
    public bool Body_Ground_Hit_Date(out RaycastHit hit)
    {
        Collider col = bodyCol;
        if (col == null) col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            Debug.LogError("Player_Body|Body_Ground_Hit_Date|未找到 Collider");
            hit = default;
            return false;
        }

        return Ground_Ray_Date(col, groundMask, groundCheckDistance, maxSlopeAngle, out hit);
    }

    // 按胶囊底部球心检测坡面
    static bool Ground_Ray_Date(Collider col, LayerMask mask, float checkDistance,
        float maxAngle, out RaycastHit hit)
    {
        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + checkDistance;

        if (col is CapsuleCollider capsule && capsule.direction == 1)
        {
            Transform colTransform = capsule.transform;
            float radius = capsule.radius * Mathf.Max(
                Mathf.Abs(colTransform.lossyScale.x),
                Mathf.Abs(colTransform.lossyScale.z));
            radius = Mathf.Min(radius, col.bounds.extents.y);

            origin.y -= col.bounds.extents.y - radius;
            float slopeCos = Mathf.Max(Mathf.Cos(
                Mathf.Clamp(maxAngle, 0f, 75f) * Mathf.Deg2Rad), 0.01f);
            rayDistance = (radius + checkDistance) / slopeCos + 0.02f;
        }

        if (!Physics.Raycast(origin, Vector3.down, out hit, rayDistance,
            mask, QueryTriggerInteraction.Ignore))
            return false;

        return Vector3.Angle(Vector3.up, hit.normal) <= maxAngle + 0.01f;
    }

    // 跳跃逻辑，只在地面起跳
    public void Body_Jump_Date()
    {
        if (!IsGrounded) return;    //非地面不能起跳

        jumpAirborne = true;
        IsGrounded = false;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // 翻滚施力，负值减速
    public void Body_Roll_Date(Vector2 axis, Vector3 viewDir, float force)
    {
        // 无输入沿正前方翻
        Vector3 dir = axis.sqrMagnitude > 0.01f ? GetMoveDir(axis, viewDir) : transform.forward;

        if (force < 0f)
        {
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(horizontalVelocity, dir));
            force = -Mathf.Min(-force, forwardSpeed * rb.mass);
        }

        rb.AddForce(dir * force, ForceMode.Impulse);
    }

    // 滑铲，axis为输入轴，viewDir为视角方向
    public void Body_Slide_Date(Vector2 axis, Vector3 viewDir, Vector3 normal)
    {
        Vector3 dir = axis.sqrMagnitude > 0.01f ? GetMoveDir(axis, viewDir) : currentMoveDirection;
        dir = Vector3.ProjectOnPlane(dir, normal);
        if (dir.sqrMagnitude <= 0.001f)
            dir = Vector3.ProjectOnPlane(transform.forward, normal);
        if (dir.sqrMagnitude <= 0.001f) dir = currentMoveDirection;

        currentMoveDirection = dir.normalized;
        rb.AddForce(dir * slideForce, ForceMode.Impulse);
    }

    // 碰撞体高度缩放，scale为比例
    public void Body_Size_Date(float scale)
    {
        if (!(bodyCol is CapsuleCollider cap)) return;

        cap.height = colHeight * scale;
        cap.center = new Vector3(colCenter.x,
            colCenter.y - (colHeight - cap.height) * 0.5f, colCenter.z);
    }

    // 按节奏沿移动方向施加推力
    public void Body_Move()
    {
        moveTimer += Time.fixedDeltaTime;

        // 坡面逐物理帧补速，平面按原间隔施力
        bool slopeGrounded = IsGrounded && groundNormal.y < 0.9995f;
        if (!slopeGrounded && moveTimer < moveInterval) return;

        moveTimer = 0f;


        // 只补到目标速度，多给会被限速削掉
        Vector3 moveDirection = IsGrounded
            ? Ground_Move_Direction(groundNormal) : currentMoveDirection;
        float normalSpeed = IsGrounded ? Vector3.Dot(rb.velocity, groundNormal) : 0f;
        Vector3 tangentVelocity = IsGrounded
            ? rb.velocity - groundNormal * normalSpeed : rb.velocity;
        float lack = (currentSpeed - Vector3.Dot(tangentVelocity, moveDirection)) * rb.mass;
        if (lack <= 0f) return;

        // 单段冲量不超过持续力折算值
        float impulse = Mathf.Min(lack, currentForce * moveInterval);
        float factor = IsGrounded ? 1f : airForce;    // 空中只留少量操控

        rb.AddForce(moveDirection * impulse * factor, ForceMode.Impulse);
    }

    // 表现，转向移动方向
    public void Body_Rotation_Performance()
    {
        SmoothRotate(currentMoveDirection);
    }

    // 表现，转向视角方向
    public void Body_Aim_Performance(Vector3 viewDir)
    {
        SmoothRotate(viewDir);
    }
}
