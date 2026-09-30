using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 玩家本体控制，数据由 Player_Control 注入
public class Player_Body : Character_Move
{
    // 移动数据
    Vector3 direction = new Vector3(0, 0, 0);       // 目标移动方向
    Vector3 currentMoveDirection = Vector3.zero;    // 实际移动方向
    float currentForce = 0f;                        // 当前推力
    public float moveForce = 700f;                  // 移动推力

    // 跳跃与落地
    public float jumpForce = 100f;                  // 跳跃力
    public float groundCheckDistance = 0.2f;        // 落地检测距离
    public Rigidbody rb;                            // 刚体

    public bool IsGrounded { get; private set; }    // 是否在地面

    // 初始化，刚体由外部注入
    public void Body_Init(Rigidbody rigidbody)
    {
        rb = rigidbody;
        if (rb != null) rb.freezeRotation = true;    // 防碰撞翻滚

        currentMoveDirection = transform.forward;    // 平滑转向初值

        Debug.Log("Player_Body|Body_Init|完成初始化");
    }

    // 按输入轴与视角计算移动，axis为输入轴
    public void Player_Body_Update(Vector2 axis, Vector3 viewDir, bool run, bool squat)
    {
        // 视角换算世界方向
        direction = GetMoveDir(axis, viewDir);

        // 平滑转向
        if (direction.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = Vector3.RotateTowards(
                currentMoveDirection, direction,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 1f);
        }

        // 速度模式
        currentForce = moveForce;
        currentSpeed = walkSpeed;
        if (run && !squat)
        {
            currentForce *= 3f;
            currentSpeed = runSpeed;
        }
        if (squat)
        {
            currentForce *= 0.8f;
            currentSpeed = squatSpeed;
        }
    }

    // 视角水平方向换算移动方向
    Vector3 GetMoveDir(Vector2 axis, Vector3 viewDir)
    {
        Vector3 forward = viewDir; forward.y = 0f; forward.Normalize();
        if (forward.sqrMagnitude <= 0.001f) forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return (axis.x * right + axis.y * forward).normalized;
    }

    // 物理帧更新，落地检测与限速
    public void Local_FixedUpdate()
    {
        IsGrounded = isGrounded();
        //地面移动
        if (IsGrounded)
        {
            // 限制水平速度
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVelocity.magnitude > currentSpeed)
            {
                Vector3 limitedHorizontal = horizontalVelocity.normalized * currentSpeed;
                rb.velocity = new Vector3(limitedHorizontal.x, rb.velocity.y, limitedHorizontal.z);
            }
        }
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

    // 施加跳跃力
    public void Body_Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    // 沿当前移动方向施加推力
    public void Body_Move()
    {
        rb.AddForce(currentMoveDirection * currentForce, ForceMode.Force);
    }

    // 转向移动方向
    public void Body_rotation()
    {
        SmoothRotate(currentMoveDirection);
    }

    // 转向视角方向
    public void Body_rotationWithFocus(Vector3 viewDir)
    {
        SmoothRotate(viewDir);
    }

    // 按输入轴与视角换算移动方向
    public void Body_calculateVectorMove(Vector2 axis, Vector3 viewDir)
    {
        direction = GetMoveDir(axis, viewDir);

        // 平滑转向
        if (direction.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = Vector3.RotateTowards(
                currentMoveDirection, direction,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 1f);
        }
    }
}
