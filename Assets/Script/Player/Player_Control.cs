using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Unity.Netcode;
using Unity.VisualScripting;
using System;

/// <summary>
/// 提供玩家对象相关组件引用，提供行为方法作为最终描述环节
/// </summary>
public class Player_Control : Character_Move
{
    NetworkObject netObj;   // 联网对象（用于判断是否本地玩家）

    public Transform Head;
    public RectTransform UIFocus;

    public Camera Player_camera;

    [Header("落地检测")]
    public float groundCheckDistance = 0.2f;  // 落地检测距离

    public Gun_Control gun_Control;

    [Header("精度：目标检测")]
    public LayerMask enemyMask;      // 哪些层算敌人（给瞄准精度用）
    
    public Player_camera camShake;   // 相机震动脚本（开火时触发）
    public Player_Body body;
    public Player_Input input;
    public Player_animation player_Animation;

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


    void Awake()
    {
        if (body == null) body = GetComponent<Player_Body>();
        if (input == null) input = GetComponent<Player_Input>();
    }

    void Start()
    {
        netObj = GetComponent<NetworkObject>();


        // 非本地玩家：不做本地控制/不绑相机，物理交给网络同步

        if (netObj != null && !netObj.IsOwner)
        {
            //rb.isKinematic = true;
            Debug.Log("非本地玩家，已禁用本地控制");
            return;
        }

        // 隐藏并锁定鼠标到屏幕中心，不让其移出屏幕
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 输入事件注册（只有本地玩家注册）
        RegisterInputEvents();


    }

    void RegisterInputEvents()
    {
        if (input == null) return;

        input.Move_event += OnMove;                 //移动
        input.JumpDown_event += OnJumpDown;         //跳跃
        input.Mouse1_event += OnMouse1;             //左键（开火）
        input.Mouse2_event += OnMouse2;             //右键（肩射）
        input.MouseHeld_event += OnMouseHeld;       //鼠标按住（举枪/瞄准）
        input.Run_event += OnRun;                   //奔跑
        input.ReloadHeld_event += OnReloadHeld;     //换弹
        input.Squat_event += OnSquat;               //蹲下
    }

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

    /// <summary>
    /// 保持奔跑并传入移动方向
    /// </summary>
    /// <param name="axis"></param>
    void OnMove(Vector2 axis)
    {
        try
        {
            isWASDDowm = input.WASDHeld;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        if (body == null)
        {
            Debug.LogError("body == null");
            return;
        }
        if (isOnGround)
        {
            body.Body_calculateVectorMove(axis);
            body.Body_Move();
            if (!isMouseDown) body.Body_rotation();
        }
    }

    /// <summary>
    /// 施加向上力
    /// </summary>
    void OnJumpDown(bool on)
    {
        isJumpDown = on;
        if (on)
        {
            if (body != null) body.Body_Jump();
        }

    }

    /// <summary>
    /// 左键：按下时每帧开火（射速由 Gun_Control 内部限流），松开立即停止开火
    /// </summary>
    void OnMouse1(bool on)
    {
        isMouse1Down = on;
        if (gun_Control == null) return;

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

    /// <summary>
    /// 右键：按下进入肩射（相机肩射 + 后坐力减少 70%），松开恢复
    /// </summary>
    void OnMouse2(bool on)
    {
        isMouse2Down = on;
        if (camShake != null) camShake.SetShoulderAim(isMouse2Down);
        if (gun_Control != null)
        {
            gun_Control.SetRecoilReduction(isMouse2Down);
        }
    }

    /// <summary>
    /// 鼠标按住（左键或右键）：按下举枪并朝向准星，松开放下枪
    /// </summary>
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

    /// <summary>
    ///  奔跑（状态每帧上报，松开即为 false）
    /// </summary>
    void OnRun(bool on)
    {
        isRuning = on;
    }

    /// <summary>
    ///  换弹
    /// </summary>
    void OnReloadHeld()
    {
        if (input != null) isReload = input.ReloadHeld;
        if (isReload && gun_Control != null) gun_Control.Reload();
    }

    /// <summary>
    ///  蹲下（状态每帧上报，松开即为 false）
    /// </summary>
    void OnSquat(bool on)
    {
        isSquat = on;

    }

    // 右键状态应用：相机肩射 + 枪械后坐力减免
    void ApplyAimState(bool on)
    {


    }

    void Update()
    {
        if (netObj != null && !netObj.IsOwner) return;   // 非本地对象不更新
        if (Player_camera == null) return;               // 等关卡管理器绑定本地相机后再控制

        //键盘输入状态检测
        isOnGround = IsGrounded();

        if (input == null || body == null || gun_Control == null) return;

        // 本地向量数据计算
        body.Player_Body_Update(input.MoveAxis, isRuning, isSquat);

        // 动画更新
        player_Animation.Player_animation_Update(this);

        //把姿态 / 移动状态 / 目标坐标喂给枪

            gun_Control.SetRecoilReduction(isMouse2Down);
            gun_Control.SetAimState(isMouse2Down, false);          // 右键=据枪；开镜还没输入，先传 false
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
        isJumpDown   = input.JumpDownHeld;
        isReload     = input.ReloadHeld;
        isWASDDowm   = input.WASDHeld;
        isRuning     = input.RunHeld && (isMouse2Down == false);
        isSquat      = input.SquatHeld;
    }
    void FixedUpdate()
    {
        body.Local_FixedUpdate();

        gun_Control.Gun_Control_FixedUpdate();
    }

    // 检测角色是否站在地面上
    bool IsGrounded()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col == null) return false;

        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, rayDistance);
    }


    // 由关卡管理器统一调用：给本地玩家绑定场景相机/UI/相机跟随（替代分散的 Player_Main 初始化）
    public void SetupLocal(Camera cam, Player_camera camRig, RectTransform ui)
    {
        Player_camera = cam;
        camShake = camRig;
        if (camRig != null) camRig.Player = Head;   // 第三人称相机跟随本地玩家头部
    }

    // 【新增】找瞄准范围内"最靠近枪口方向"的目标，把它的坐标交给枪
    // 这里是"外界"，负责去场景里找；Gun 不参与查找，只负责判定和存数据
    void UpdateAimTarget()
    {
        Vector3 origin = gun_Control.MuzzlePosition;
        Collider[] cols = Physics.OverlapSphere(origin, gun_Control.range, enemyMask);

        float bestAngle = float.MaxValue;
        Vector3 bestPos = Vector3.zero;
        bool found = false;

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(transform)) continue;   // 跳过自己身上的碰撞体

            Vector3 center = col.bounds.center;                 // 用包围盒中心，比 pivot 稳
            float angle = Vector3.Angle(gun_Control.transform.forward, center - origin);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestPos   = center;
                found     = true;
            }
        }

        gun_Control.SetTarget(found, bestPos);
    }

    void ForwardInputToBody(float X, float Y)
    {
        body.SetMoveInput(new Vector2(X, Y));
        body.SetMoveHeld(isWASDDowm);
        body.SetRun(isRuning);
        body.SetSquat(isSquat);
        body.SetJumpHeld(isJumpDown);
        body.SetAim(isMouseDown);
    }
}
