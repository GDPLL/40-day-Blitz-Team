using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Unity.Netcode;
using Unity.VisualScripting;
using System;

// 玩家行为控制，持有各组件引用
public class Player_Control : Character_Move
{
    NetworkObject netObj;   // 联网对象

    // 外部引用
    public Transform Head;                       // 头部
    public RectTransform UIFocus;                // 准星UI
    public new Rigidbody rigidbody;              // 刚体，需在 Inspector 赋值
    public Camera Player_camera;                 // 相机
    public Gun_Control gun_Control;              // 枪械
    public Player_camera camShake;               // 相机震动
    public Player_Body body;                     // 本体
    public Input_Manage input;                   // 全局输入
    public Player_animation player_Animation;    // 动画

    [Header("落地检测")]
    public float groundCheckDistance = 0.2f;  // 落地检测距离

    [Header("精度：目标检测")]
    public LayerMask enemyMask;      // 敌人层

    //本地状态
    public bool isOnGround;  //在地面
    public bool isJumpDown;    //跳跃空格
    public bool isMouse1Down;   //左键输入
    public bool isMouse2Down;   //右键输入
    public bool isMouseDown => isMouse1Down || isMouse2Down;
    public bool isWASDDowm;    //移动输入
    public bool isRuning;      //奔跑输入
    public bool isReload;       //换弹输入
    public bool isSquat;        //蹲下输入


    // 取缺失的组件引用
    void Awake()
    {
        if (body == null) body = GetComponent<Player_Body>();
    }

    // 初始化组件与输入
    void Start()
    {
        netObj = GetComponent<NetworkObject>();

        if (input == null) input = Input_Manage.Instance;   // 全局输入单例
        if (input == null) Debug.LogError("Player_Control|Start|未找到 Input_Manage");

        UIFocus = Player_Main.UI_RectTransform;

    
        if (Player_camera == null)
        {
            Debug.LogError("Player_Control|Start|Player_camera 为空");
        }
        if (UIFocus == null)
        {
            Debug.LogError("Player_Control|Start|UIFocus 为空");
        }
        if (rigidbody == null)
        {
            Debug.LogError("Player_Control|Start|rigidbody 为空");
        }

        // 非本地玩家不控制

        if (netObj != null && !netObj.IsOwner)
        {
            Debug.Log("非本地玩家，已禁用本地控制");
            return;
        }

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 注册输入事件
        RegisterInputEvents();

        body.Body_Init(Player_camera, UIFocus, rigidbody);
        gun_Control.Gun_Control_Init(UIFocus);
    }

    // 注册输入事件
    void RegisterInputEvents()
    {
        if (input == null)
        {
            Debug.LogError("Player_Control|RegisterInputEvents|input 为空");
            return;
        }

        input.Move_event += OnMove;                 //移动
        input.JumpDown_event += OnJumpDown;         //跳跃
        input.Mouse1_event += OnMouse1;             //左键
        input.Mouse2_event += OnMouse2;             //右键
        input.MouseHeld_event += OnMouseHeld;       //鼠标按住
        input.Run_event += OnRun;                   //奔跑
        input.ReloadHeld_event += OnReloadHeld;     //换弹
        input.Squat_event += OnSquat;               //蹲下
    }

    // 反注册输入事件
    void UnregisterInputEvents()
    {
        if (input == null) return;

        input.Move_event -= OnMove;
        input.JumpDown_event -= OnJumpDown;
        input.Mouse1_event -= OnMouse1;
        input.Mouse2_event -= OnMouse2;
        input.MouseHeld_event -= OnMouseHeld;
        input.Run_event -= OnRun;
        input.ReloadHeld_event -= OnReloadHeld;
        input.Squat_event -= OnSquat;
    }

    void OnDestroy()
    {
        UnregisterInputEvents();
    }

    // 移动，axis为输入轴
    void OnMove(Vector2 axis)
    {
        try
        {
            isWASDDowm = input.WASDHeld;
        }
        catch (Exception e)
        {
            Debug.LogError($"Player_Control|OnMove|{e}");
        }
        if (body == null)
        {
            Debug.LogError("Player_Control|OnMove|body 为空");
            return;
        }
        if (isOnGround)
        {
            body.Body_calculateVectorMove(axis);
            body.Body_Move();
            if (!isMouseDown) body.Body_rotation();
        }
    }

    // 跳跃
    void OnJumpDown(bool on)
    {
        isJumpDown = on;
        if (on)
        {
            if (body != null) body.Body_Jump();
        }

    }

    // 左键开火
    void OnMouse1(bool on)
    {
        isMouse1Down = on;
        if (gun_Control == null)
        {
            Debug.LogError("Player_Control|OnMouse1|gun_Control 为空");
            return;
        }

        if (isMouse1Down)
        {
            gun_Control.SetRecoilReduction(false);
            if (gun_Control != null && gun_Control.Shoot())
            {
                if (camShake != null) camShake.Shake();
            }
        }
        else
        {

        }


    }

    // 右键肩射
    void OnMouse2(bool on)
    {
        isMouse2Down = on;
        if (camShake != null) camShake.SetShoulderAim(isMouse2Down);
        if (gun_Control != null)
        {
            gun_Control.SetRecoilReduction(isMouse2Down);
        }
    }

    // 举枪瞄准
    void OnMouseHeld(bool on)
    {
        if (on)
        {
            body.Body_calculateVectorCamera();
            body.Body_rotationWithFocus();
            gun_Control.AimAt();
        }
        else
        {
            gun_Control.AimDown();
        }

    }

    // 奔跑
    void OnRun(bool on)
    {
        isRuning = on;
    }

    // 换弹
    void OnReloadHeld()
    {
        if (input != null) isReload = input.ReloadHeld;
        if (isReload && gun_Control != null) gun_Control.Reload();
    }

    // 蹲下
    void OnSquat(bool on)
    {
        isSquat = on;

    }

    // 空方法，待实现
    void ApplyAimState(bool on)
    {
    }

    // 每帧更新本地控制
    void Update()
    {
        if (netObj != null && !netObj.IsOwner) return;   // 非本地对象不更新

        // 等相机绑定后再控制
        if (Player_camera == null)
        {
            Debug.LogError("Player_Control|Update|Player_camera 为空");
            return;
        }

        // 落地检测
        isOnGround = IsGrounded();

        if (input == null || body == null || gun_Control == null || player_Animation == null)
        {
            Debug.LogError("Player_Control|Update|input/body/gun_Control/player_Animation 为空");
            return;
        }

        // 计算移动数据
        body.Player_Body_Update(input.MoveAxis, isRuning, isSquat);

        // 动画更新
        player_Animation.Player_animation_Update(this);

        // 传递枪械状态
        gun_Control.SetRecoilReduction(isMouse2Down);
        gun_Control.SetAimState(isMouse2Down, false);          // 开镜未接入，传 false
        gun_Control.SetMoveState(isSquat, isWASDDowm, isRuning);
        UpdateAimTarget();

    }

    // 只负责读输入
    void ReadInput(out float X, out float Y)
    {
        X = 0f;
        Y = 0f;

        if (input == null) return;

        X = input.MoveAxis.x;
        Y = input.MoveAxis.y;
        isMouse1Down = input.Mouse1Held;
        isMouse2Down = input.Mouse2Held;
        isJumpDown = input.JumpDownHeld;
        isReload = input.ReloadHeld;
        isWASDDowm = input.WASDHeld;
        isRuning = input.RunHeld && (isMouse2Down == false);
        isSquat = input.SquatHeld;
    }
    // 物理帧更新
    void FixedUpdate()
    {
        body.Local_FixedUpdate();

        gun_Control.Gun_Control_FixedUpdate();
    }

    // 落地检测
    bool IsGrounded()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            Debug.LogError("Player_Control|IsGrounded|未找到 Collider");
            return false;
        }

        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, rayDistance);
    }


    // 绑定本地相机
    public void SetupLocal(Camera cam, Player_camera camRig)
    {
        Player_camera = cam;
        camShake = camRig;

        if (camRig == null)
        {
            Debug.LogError("Player_Control|SetupLocal|camRig 为空");
            return;
        }

        if (Head == null) Debug.LogError("Player_Control|SetupLocal|Head 为空，相机无法跟随");

        camRig.PlayerHeadTransform = Head;   // 相机跟随头部
        Debug.Log($"{camRig.PlayerHeadTransform}完成绑定");
    }

    // 找射程内最接近枪口方向的敌人
    void UpdateAimTarget()
    {
        Vector3 origin = gun_Control.MuzzlePosition;
        Collider[] cols = Physics.OverlapSphere(origin, gun_Control.range, enemyMask);

        float bestAngle = float.MaxValue;
        Vector3 bestPos = Vector3.zero;
        bool found = false;

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(transform)) continue;   // 跳过自身

            Vector3 center = col.bounds.center;                 // 取包围盒中心
            float angle = Vector3.Angle(gun_Control.transform.forward, center - origin);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestPos = center;
                found = true;
            }
        }

        gun_Control.SetTarget(found, bestPos);
    }
}
