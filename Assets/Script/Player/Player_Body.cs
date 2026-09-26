using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 提供对玩家本体控制的所有方法与解释
/// </summary>
public class Player_Body : Character_Move
{


    public RectTransform uiFocuspos;      // 中心UI

    Vector3 direction = new Vector3(0, 0, 0);
    // 实际移动方向
    Vector3 currentMoveDirection = Vector3.zero;
    float currentForce = 0f;
    public float moveForce = 700f;

    public float jumpForce = 100f;
    public float groundCheckDistance = 0.2f;
    public Rigidbody rb;

    bool  mouseHeld;

    Camera PlayerCamera;

    Vector3 focusPoint; //摄像机聚焦方向
    public bool IsGrounded { get; private set; }

    /// <summary>
    /// 初始化（由 Player_Control 调用，Player_Body 不自己查控件）
    /// 相机 / 准星 UI / 刚体 全部由外部注入
    /// </summary>
    public void Body_Init(Camera camera, RectTransform uiFocus, Rigidbody rigidbody)
    {
        PlayerCamera = camera;
        uiFocuspos = uiFocus;

        rb = rigidbody;
        if (rb != null) rb.freezeRotation = true;    // 防碰撞翻滚

        currentMoveDirection = transform.forward;    // 平滑转向的初值


    }

    /// <summary>
    /// 本地向量计算（由 Player_Control 每帧驱动）
    /// 外部传入：axis = 移动输入轴，run / squat = 速度模式状态
    /// 内部完成：相机水平基向量 -> 输入轴映射为世界移动方向 -> 平滑过渡 -> 当前力/速度
    /// </summary>
    public void Player_Body_Update(Vector2 axis, bool run, bool squat)
    {
        if (PlayerCamera == null) return;               // 等关卡管理器绑定本地相机后再控制

        // 本地基向量：相机水平前向 / 水平右向（去掉 y，爬坡时方向不会被拉伸）
        Vector3 forward = PlayerCamera.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = PlayerCamera.transform.right; right.y = 0f; right.Normalize();

        // 输入轴 -> 世界移动方向
        direction = (axis.x * right + axis.y * forward).normalized;

        // 平滑过渡移动方向
        if (direction.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = Vector3.RotateTowards(
                currentMoveDirection, direction,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 1f);
        }

        //状态应用
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

    public void Local_FixedUpdate()
    {
        if (PlayerCamera == null) return;   // 等关卡管理器初始化后再执行

        IsGrounded = isGrounded();
        //地面移动逻辑
        if (IsGrounded)
        {
            //最大速度（上限 = 父类速度模式的当前速度）
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVelocity.magnitude > currentSpeed)
            {
                Vector3 limitedHorizontal = horizontalVelocity.normalized * currentSpeed;
                rb.velocity = new Vector3(limitedHorizontal.x, rb.velocity.y, limitedHorizontal.z);
            }
        }

        //人物-枪械姿态逻辑
        /*
        if (Tran_Gun != null && uiFocuspos != null)
        {
            if (mouseHeld == true)
            {
                // 由主摄像机与 UI 焦点发出的射线：命中对象时用碰撞落点作为瞄准点
                Ray ray = Camera.main.ScreenPointToRay(uiFocuspos.position);
                Vector3 aimPoint;
                if (TryGetAimPoint(ray, out Vector3 hitPoint))
                    aimPoint = hitPoint;                    // 命中对象：瞄准碰撞落点
                else
                    aimPoint = ray.GetPoint(focusDistance); // 未命中：回到固定焦点距离
                AimGun(Tran_Gun.transform, originalGunRot, aimPoint);   // 左/右键都举枪
            }
            if (mouseHeld == false)
            {
                AimGunDown(Tran_Gun.transform, originalGunRot);
            }
        }
        */
        

    }

    // 射线检测瞄准点：忽略 Player 标签下的物体（如玩家自身身体/枪械）
    bool TryGetAimPoint(Ray ray, out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player")) continue;   // 跳过玩家自身
            aimPoint = hit.point;
            return true;
        }
        return false;
    }
    bool isGrounded()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null) return false;

        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, rayDistance);
    }

    public void Body_Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    public void Body_Move()
    {
        rb.AddForce(currentMoveDirection * currentForce, ForceMode.Force);
    }

    public void Body_rotation()
    {
        SmoothRotate(currentMoveDirection);
    }

    public void Body_rotationWithFocus()
    {
        SmoothRotate(focusPoint);
    }

    public void Body_calculateVectorMove(Vector2 axis)
    {
        // 本地基向量：相机水平前向 / 水平右向（去掉 y，爬坡时方向不会被拉伸）
        Vector3 forward = PlayerCamera.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = PlayerCamera.transform.right; right.y = 0f; right.Normalize();

        // 输入轴 -> 世界移动方向
        direction = (axis.x * right + axis.y * forward).normalized;

        // 平滑过渡移动方向
        if (direction.sqrMagnitude > 0.001f)
        {
            currentMoveDirection = Vector3.RotateTowards(
                currentMoveDirection, direction,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 1f);
        }
    }
    
    public void Body_calculateVectorCamera()
    {
        focusPoint = Camera.main.ScreenPointToRay(uiFocuspos.position).direction;
    }
}
