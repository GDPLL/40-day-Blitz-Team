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

    // 外部引用
    public NetworkObject Con_netObj;                    // 联网对象引用
    public Gun_Control Con_gun_Control;             // 玩家枪械组件
    public Player_Body Con_body;                        // 本地玩家控制引用
    public Input_Manage Con_input_Manage;           // 本地全局输入引用,网络固定时段发送
    public Player_animation Con_player_Animation;       // 本地动画引用
    public HostNetWorkInputEvent Con_player_HostNetworkEvent;   // 网络同步事件触发器
    public Local_InputEvent Con_localInputEvent;        // 本机表现层输入事件

    // 本地对外变量
    [Header("头部位置")]
    public Transform Head;                          // 头部位置
    [Header("本地相机")]
    public Camera Con_camera;                       // 本地相机引用
    public Player_camera Con_player_camera;         // 玩家相机控制组件
    [Header("本地刚体")]
    public new Rigidbody rigidbody;                 // 刚体
    [Header("落地检测")]
    public float groundCheckDistance = 0.2f;  // 落地检测距离
    [Header("目标检测")]
    public LayerMask enemyMask;      // 敌人层

    //本地变量
    private bool IsComplete;            //组件层完善判断
    private bool isStarted;             //是否已初始化
    private bool isLocalStarted;        //本机表现层是否已初始化
    public bool IsActive => IsServer && IsComplete; //主机合法运行判断

    //本地状态中转
    public bool isOnGround;  //在地面
    public bool isJumpDown;    //跳跃空格
    public bool isMouse1Down;   //左键输入
    public bool isMouse2Down;   //右键输入
    public bool isMouseDown => isMouse1Down || isMouse2Down;
    public bool isWASDDowm;    //移动输入
    public bool isRuning;      //奔跑输入
    public bool isReload;       //换弹输入
    public bool isSquat;        //蹲下输入

    //本机表现状态
    bool isLocalFireHeld;   //本机左键按住
    float localFireTimer;   //本机震动计时

    // 初始化组件与输入，服务器与客户端通用
    public void Player_Control_Start(Input_Manage inputManage)
    {
        if (isStarted) return;              //防重复初始化
        isStarted = true;

        Con_input_Manage = inputManage;     //全局输入单例

        //组件完整性判断
        IsComplete = true;
        if (Con_netObj == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Con_netObj");
            IsComplete = false;
        }
        if (Con_input_Manage == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Input_Manage");
            IsComplete = false;
        }
        if (Con_body == null) Con_body = GetComponent<Player_Body>();
        if (Con_body == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Player_Body");
            IsComplete = false;
        }
        if (Con_gun_Control == null)
        {
            Debug.LogError("Player_Control|Start|Con_gun_Control 为空");
            IsComplete = false;
        }
        if (Con_player_Animation == null)
        {
            Debug.LogError("Player_Control|Start|Con_player_Animation 为空");
            IsComplete = false;
        }
        if (Con_player_HostNetworkEvent == null)
        {
            Debug.LogError("Player_Control|Start|Con_player_HostNetworkEvent 为空");
            IsComplete = false;
        }
        if (rigidbody == null)
        {
            Debug.LogError("Player_Control|Start|rigidbody 为空");
            IsComplete = false;
        }

        // 注册输入包接收
        if (Con_player_HostNetworkEvent != null) Con_player_HostNetworkEvent.Host_Input_Init(Con_input_Manage);

        // 注册输入事件
        RegisterInputEvents();

        if (Con_body != null) Con_body.Body_Init(rigidbody);
        if (Con_gun_Control != null) Con_gun_Control.Gun_Control_Init();

        Debug.Log("Player_Control|Player_Control_Start|完成初始化");
    }

    // 本机初始化重载，带相机，只有本地玩家可调用
    public void Player_Control_Local_Init(Camera camera, Input_Manage inputManage, Player_camera playerCamera)
    {
        if (!IsLocalPlayer) return;         //非本机不跑表现层
        if (isLocalStarted) return;         //防重复初始化
        isLocalStarted = true;

        Con_camera = camera;                //本地相机
        Con_player_camera = playerCamera;   //相机控制组件

        Player_Control_Start(inputManage);  //通用初始化

        if (Con_camera == null)
        {
            Debug.LogError("Player_Control|Player_Control_Local_Init|Con_camera 为空");
            return;
        }
        if (Con_player_camera == null)
        {
            Debug.LogError("Player_Control|Player_Control_Local_Init|Con_player_camera 为空");
            return;
        }

        // 本机表现层事件分发
        if (Con_localInputEvent == null) Con_localInputEvent = gameObject.AddComponent<Local_InputEvent>();
        Con_localInputEvent.Local_Input_Init(Con_input_Manage);
        Con_localInputEvent.ShoulderAim_event += OnLocalShoulderAim;
        Con_localInputEvent.FireHeld_event += OnLocalFireHeld;

        Con_player_camera.Player_camera_Start(Head);    //相机跟随头部

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Player_Control|Player_Control_Local_Init|完成初始化");
    }

    // 本机表现层每帧更新
    public void Player_Control_Local_Update()
    {
        if (!IsLocalPlayer) return;

        // 相机跟随
        if (Con_player_camera != null) Con_player_camera.Player_camera_Update();

        // 开火震动，按射速逐发
        if (Con_player_camera == null || Con_gun_Control == null) return;

        if (!isLocalFireHeld)
        {
            localFireTimer = 0f;    //松开归零
            return;
        }

        localFireTimer -= Time.deltaTime;
        if (localFireTimer > 0f) return;

        localFireTimer = 1f / Con_gun_Control.fireRate;
        if (Con_gun_Control.HasAmmo()) Con_player_camera.Shake();
    }

    // 本机肩射开关
    void OnLocalShoulderAim(bool on)
    {
        if (Con_player_camera != null) Con_player_camera.SetShoulderAim(on);
    }

    // 本机左键按住
    void OnLocalFireHeld(bool on)
    {
        isLocalFireHeld = on;
    }

    // 每帧更新本地控制
    public void Player_Control_Update()
    {
        if (!IsActive) return;   // 非法不运行

        if (Con_body == null || Con_gun_Control == null || Con_player_Animation == null || Con_player_HostNetworkEvent == null)
        {
            Debug.LogError("Player_Control|Update|body/gun/Animation/HostNetworkEvent 为空");
            return;
        }

        InputPacket packet = Con_player_HostNetworkEvent.Packet;   //纯数据来源

        // 落地检测
        isOnGround = IsGrounded();

        // 计算移动数据
        Con_body.Player_Body_Update(packet.move, packet.viewDir, isRuning, isSquat);

        // 动画更新
        Con_player_Animation.Player_animation_Update(this);

        // 传递枪械状态
        Con_gun_Control.SetRecoilReduction(isMouse2Down);
        Con_gun_Control.SetAimState(isMouse2Down, false);          // 开镜未接入，传 false
        Con_gun_Control.SetMoveState(isSquat, isWASDDowm, isRuning);
        UpdateAimTarget();

    }
    // 物理帧更新
    public void Player_Control_FixedUpdate()
    {
        if (!IsActive) return;

        Con_body.Local_FixedUpdate();
        Con_gun_Control.Gun_Control_FixedUpdate();
    }


    // 注册输入事件
    void RegisterInputEvents()
    {
        if (Con_player_HostNetworkEvent == null)
        {
            Debug.LogError("Player_Control|RegisterInputEvents|HostNetWorkInputEvent 为空");
            return;
        }

        Con_player_HostNetworkEvent.Move_event += OnMove;                 //移动
        Con_player_HostNetworkEvent.JumpDown_event += OnJumpDown;         //跳跃
        Con_player_HostNetworkEvent.Mouse1_event += OnMouse1;             //左键
        Con_player_HostNetworkEvent.Mouse2_event += OnMouse2;             //右键
        Con_player_HostNetworkEvent.MouseHeld_event += OnMouseHeld;       //鼠标按住
        Con_player_HostNetworkEvent.Run_event += OnRun;                   //奔跑
        Con_player_HostNetworkEvent.ReloadHeld_event += OnReloadHeld;     //换弹
        Con_player_HostNetworkEvent.Squat_event += OnSquat;               //蹲下
    }

    // 反注册输入事件
    void UnregisterInputEvents()
    {
        if (Con_player_HostNetworkEvent == null) return;

        Con_player_HostNetworkEvent.Move_event -= OnMove;
        Con_player_HostNetworkEvent.JumpDown_event -= OnJumpDown;
        Con_player_HostNetworkEvent.Mouse1_event -= OnMouse1;
        Con_player_HostNetworkEvent.Mouse2_event -= OnMouse2;
        Con_player_HostNetworkEvent.MouseHeld_event -= OnMouseHeld;
        Con_player_HostNetworkEvent.Run_event -= OnRun;
        Con_player_HostNetworkEvent.ReloadHeld_event -= OnReloadHeld;
        Con_player_HostNetworkEvent.Squat_event -= OnSquat;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        UnregisterInputEvents();

        // 反注册本机表现事件
        if (Con_localInputEvent != null)
        {
            Con_localInputEvent.ShoulderAim_event -= OnLocalShoulderAim;
            Con_localInputEvent.FireHeld_event -= OnLocalFireHeld;
        }
    }

    // 移动，axis为输入轴
    void OnMove(Vector2 axis)
    {
        isWASDDowm = axis.sqrMagnitude >= 0.01f;

        if (Con_body == null || Con_player_HostNetworkEvent == null)
        {
            Debug.LogError("Player_Control|OnMove|body 为空");
            return;
        }
        if (!isOnGround) return;

        // 无输入不施推力
        if (!isWASDDowm) return;

        Con_body.Body_calculateVectorMove(axis, Con_player_HostNetworkEvent.Packet.viewDir);
        Con_body.Body_Move();
        if (!isMouseDown) Con_body.Body_rotation();
    }

    // 跳跃
    void OnJumpDown(bool on)
    {
        isJumpDown = on;
        if (on && Con_body != null) Con_body.Body_Jump();
    }

    // 左键开火
    void OnMouse1(bool on)
    {
        isMouse1Down = on;
        if (Con_gun_Control == null)
        {
            Debug.LogError("Player_Control|OnMouse1|gun_Control 为空");
            return;
        }
        if (!on) return;

        Con_gun_Control.SetRecoilReduction(false);
        Con_gun_Control.Shoot();
    }

    // 右键肩射
    void OnMouse2(bool on)
    {
        isMouse2Down = on;
        if (Con_gun_Control != null) Con_gun_Control.SetRecoilReduction(isMouse2Down);
    }

    // 举枪瞄准
    void OnMouseHeld(bool on)
    {
        if (Con_body == null || Con_gun_Control == null || Con_player_HostNetworkEvent == null) return;

        if (on)
        {
            InputPacket packet = Con_player_HostNetworkEvent.Packet;   //纯数据来源
            Con_body.Body_rotationWithFocus(packet.viewDir);
            Con_gun_Control.AimAt(packet.aimPoint);
        }
        else
        {
            Con_gun_Control.AimDown();
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
        isReload = true;
        if (Con_gun_Control != null) Con_gun_Control.Reload();
    }

    // 蹲下
    void OnSquat(bool on)
    {
        isSquat = on;
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

    // 找射程内最接近枪口方向的敌人
    void UpdateAimTarget()
    {
        Vector3 origin = Con_gun_Control.MuzzlePosition;
        Collider[] cols = Physics.OverlapSphere(origin, Con_gun_Control.range, enemyMask);

        float bestAngle = float.MaxValue;
        Vector3 bestPos = Vector3.zero;
        bool found = false;

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(transform)) continue;   // 跳过自身

            Vector3 center = col.bounds.center;                 // 取包围盒中心
            float angle = Vector3.Angle(Con_gun_Control.transform.forward, center - origin);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestPos = center;
                found = true;
            }
        }

        Con_gun_Control.SetTarget(found, bestPos);
    }
}
