using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Body : Character_Move
{
    [Header("朝向焦点")]
    public float focusDistance = 10f;   // 按住左键时，人物朝向相机射线前方 focusDistance 处的焦点

    public RectTransform uiFocuspos;      // 中心UI

    public GameObject Gun;//枪械对象

    Quaternion originalGunRot;//开始时记录的枪械原始局部旋转

    public void SetupContext(Camera camera, RectTransform uiFocus)
    {
        PlayerCamera = camera;
        uiFocuspos   = uiFocus;
    }

    Vector3 direction = new Vector3(0, 0, 0);
    // 实际移动方向
    Vector3 currentMoveDirection = Vector3.zero;
    float currentForce = 0f;
    public float moveForce = 700f;
    
    public float jumpForce = 100f;
    public float groundCheckDistance = 0.2f;
    public Rigidbody rb;
    
    Vector2 moveAxis;
    bool runHeld, squatHeld, wasdHeld, jumpHeld,mouseHeld;

    Camera PlayerCamera;

    public void SetMoveInput(Vector2 axis) => moveAxis = axis;
    public void SetRun(bool on) => runHeld = on;
    public void SetSquat(bool on) => squatHeld = on;
    public void SetMoveHeld(bool on) => wasdHeld = on;
    public void SetJumpHeld(bool on) => jumpHeld = on;
    public void SetAim(bool on) => mouseHeld = on;
    public bool IsGrounded { get; private set; }
    public bool IsMoving => wasdHeld;


    // Start is called before the first frame update
    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null) rb.freezeRotation = true;    // 防碰撞翻滚
        currentMoveDirection = transform.forward;    // 平滑转向的初值
        if (Gun != null) originalGunRot = Gun.transform.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (PlayerCamera == null) return;               // 等关卡管理器绑定本地相机后再控制

        //鼠标输入方向计算
        Vector3 forward = PlayerCamera.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right   = PlayerCamera.transform.right;   right.y = 0; right.Normalize();
        direction = (moveAxis.x * right + moveAxis.y * forward).normalized;
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
        if (runHeld && !squatHeld)
        {
            currentForce *= 3f;
            currentSpeed = runSpeed;
        }
        if (squatHeld)
        {
            currentForce *= 0.8f;
            currentSpeed = squatSpeed;
        }
    }

    void FixedUpdate()
    {
         if (PlayerCamera == null) return;   // 等关卡管理器初始化后再执行

        IsGrounded = isGrounded();
         //地面移动逻辑
        if (IsGrounded)
        {

            //移动（使用平滑后的移动方向，转向时不会瞬间改变方向）
            if (wasdHeld)
            {
                rb.AddForce(currentMoveDirection * currentForce, ForceMode.Force);
                UnityEngine.Debug.DrawRay(transform.position, currentMoveDirection * 2f, Color.green);
            }

            //最大速度（上限 = 父类速度模式的当前速度）
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVelocity.magnitude > currentSpeed)
            {
                Vector3 limitedHorizontal = horizontalVelocity.normalized * currentSpeed;
                rb.velocity = new Vector3(limitedHorizontal.x, rb.velocity.y, limitedHorizontal.z);
            }

            //移动模型朝向（平滑转向）
            if (!mouseHeld && wasdHeld)
            {
                SmoothRotate(currentMoveDirection);
            }

            //按住键（左/右键）：人物朝向相机UI 前方（平滑转向）
            if (mouseHeld && uiFocuspos != null)
            {
                Vector3 focusPoint = Camera.main.ScreenPointToRay(uiFocuspos.position).direction;
                SmoothRotate(focusPoint);
            }

            //跳跃
            if (jumpHeld)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }


        }
        //人物-枪械姿态逻辑（需要准星 UI，没绑就跳过，不影响移动）
            if (Gun != null && uiFocuspos != null)
            {
                // 由主摄像机与 UI 焦点发出的射线：命中对象时用碰撞落点作为瞄准点
                Ray ray = Camera.main.ScreenPointToRay(uiFocuspos.position);
                Vector3 aimPoint;
                if (TryGetAimPoint(ray, out Vector3 hitPoint))
                    aimPoint = hitPoint;                    // 命中对象：瞄准碰撞落点
                else
                    aimPoint = ray.GetPoint(focusDistance); // 未命中：回到固定焦点距离
                AimGun(Gun.transform, originalGunRot, aimPoint, mouseHeld);   // 左/右键都举枪
            }

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
}
