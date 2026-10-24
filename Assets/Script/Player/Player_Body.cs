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
    float groundCheckDistance = 0.2f;        // 落地检测距离
    public Rigidbody rb;                            // 刚体

    // 翻滚
    public float rollForce = 750f;          // 翻滚推力
    public float rollSpeed = 9f;            // 翻滚最大速度
    public float rollDelay = 0.8991f;       // 翻滚施力延迟秒
    public float rollTime = 0.5f;           // 翻滚时长
    public float rollSquatScale = 0.5f;     // 翻滚碰撞体比例
    public float slideSpeed = 8f;           // 滑铲最大速度
    public float slideForce = 900f;         // 滑铲推力
    public float slideSquatScale = 0.5f;    // 滑铲碰撞体比例
    public float deadSquatScale = 0.23f;    // 死亡失重碰撞体比例
    Collider bodyCol;                       // 玩家碰撞体
    Vector3 colCenter;                      // 碰撞体原始中心
    float colHeight;                        // 碰撞体原始高度

    public bool IsGrounded { get; private set; }    // 是否在地面

    // 初始化，刚体由外部注入
    public void Body_Init(Rigidbody rigidbody)
    {
        rb = rigidbody;
        if (rb != null)
        {
            rb.freezeRotation = true;    // 防碰撞翻滚
            rb.interpolation = RigidbodyInterpolation.Interpolate;
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
    public void Body_Fixed_Date(bool rolling, bool sliding)
    {
        IsGrounded = isGrounded();

        // 松开方向立刻刹停，翻滚与空中不刹
        if (!rolling && !sliding && !hasInput && IsGrounded)
        {
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            return;
        }

        // 地面移动：速度严格沿移动方向，去掉侧向滑步并完成限速
        if (!rolling && !sliding && IsGrounded)
        {
            float along = Mathf.Clamp(Vector3.Dot(rb.velocity, currentMoveDirection), 0f, currentSpeed);
            rb.velocity = currentMoveDirection * along + Vector3.up * rb.velocity.y;
            return;
        }

        // 翻滚中限到翻滚速度
        if (rolling) Limit_Speed(rollSpeed);
        else if (sliding) Limit_Speed(slideSpeed);
    }

    // 限水平速度，limit为上限
    void Limit_Speed(float limit)
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (horizontalVelocity.magnitude <= limit) return;

        Vector3 limitedHorizontal = horizontalVelocity.normalized * limit;
        rb.velocity = new Vector3(limitedHorizontal.x, rb.velocity.y, limitedHorizontal.z);
    }

    // 落地检测
    bool isGrounded()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            Debug.LogError("Player_Body|isGrounded|未找到 Collider");
            return false;
        }

        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, rayDistance);
    }

    // 跳跃逻辑，只在地面起跳
    public void Body_Jump_Date()
    {
        if (!IsGrounded) return;    //非地面不能起跳

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // 翻滚，axis为输入轴，viewDir为视角方向
    public void Body_Roll_Date(Vector2 axis, Vector3 viewDir)
    {
        // 无输入沿正前方翻
        Vector3 dir = axis.sqrMagnitude > 0.01f ? GetMoveDir(axis, viewDir) : transform.forward;

        rb.AddForce(dir * rollForce, ForceMode.Impulse);
    }

    // 滑铲，axis为输入轴，viewDir为视角方向
    public void Body_Slide_Date(Vector2 axis, Vector3 viewDir)
    {
        Vector3 dir = axis.sqrMagnitude > 0.01f ? GetMoveDir(axis, viewDir) : currentMoveDirection;
        currentMoveDirection = dir;
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
        // 未到间隔不施力
        moveTimer += Time.fixedDeltaTime;
        if (moveTimer < moveInterval) return;

        moveTimer = 0f;


        // 只补到目标速度，多给会被限速削掉
        Vector3 flat = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float lack = (currentSpeed - Vector3.Dot(flat, currentMoveDirection)) * rb.mass;
        if (lack <= 0f) return;

        // 单段冲量不超过持续力折算值
        float impulse = Mathf.Min(lack, currentForce * moveInterval);
        float factor = IsGrounded ? 1f : airForce;    // 空中只留少量操控

        rb.AddForce(currentMoveDirection * impulse * factor, ForceMode.Impulse);
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
