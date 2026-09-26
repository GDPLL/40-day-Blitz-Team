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

    bool mouseHeld;                                 // 是否举枪

    // 外部引用
    Camera PlayerCamera;                            // 相机
    public RectTransform uiFocuspos;                // 准星UI
    Vector3 focusPoint;                             // 相机聚焦方向
    public bool IsGrounded { get; private set; }    // 是否在地面

    // 初始化，相机、准星UI、刚体由外部注入
    public void Body_Init(Camera camera, RectTransform uiFocus, Rigidbody rigidbody)
    {
        PlayerCamera = camera;
        uiFocuspos = uiFocus;

        rb = rigidbody;
        if (rb != null) rb.freezeRotation = true;    // 防碰撞翻滚

        currentMoveDirection = transform.forward;    // 平滑转向初值
    }

    // 按输入轴计算移动方向，axis为移动输入轴
    public void Player_Body_Update(Vector2 axis, bool run, bool squat)
    {
        if (PlayerCamera == null) return;               // 等相机绑定后再控制

        // 相机水平前向与右向
        Vector3 forward = PlayerCamera.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = PlayerCamera.transform.right; right.y = 0f; right.Normalize();

        // 输入轴换算世界方向
        direction = (axis.x * right + axis.y * forward).normalized;

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

    // 物理帧更新，落地检测与限速
    public void Local_FixedUpdate()
    {
        if (PlayerCamera == null) return;   // 等相机绑定后再执行

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

    // 取射线命中点，跳过自身
    bool TryGetAimPoint(Ray ray, out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player")) continue;   // 跳过自身
            aimPoint = hit.point;
            return true;
        }
        return false;
    }

    // 落地检测
    bool isGrounded()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null) return false;

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

    // 转向相机聚焦方向
    public void Body_rotationWithFocus()
    {
        SmoothRotate(focusPoint);
    }

    // 按输入轴计算移动方向
    public void Body_calculateVectorMove(Vector2 axis)
    {
        // 相机水平前向与右向
        Vector3 forward = PlayerCamera.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = PlayerCamera.transform.right; right.y = 0f; right.Normalize();

        // 输入轴换算世界方向
        direction = (axis.x * right + axis.y * forward).normalized;

        // 平滑转向
        if (direction.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = Vector3.RotateTowards(
                currentMoveDirection, direction,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 1f);
        }
    }

    // 计算相机聚焦方向
    public void Body_calculateVectorCamera()
    {
        focusPoint = Camera.main.ScreenPointToRay(uiFocuspos.position).direction;
    }
}
