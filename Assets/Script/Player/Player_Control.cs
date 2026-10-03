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
    public Object_System Con_ObjectSystem;          // 生命系统
    public Aim_Ring_UI Con_aimRing_UI;              // 本机瞄准圈显示

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
    [Header("复活点")]
    public int respawnIndex;         // 复活点编号

    //本地变量
    private bool IsComplete;            //组件层完善判断
    private bool isStarted;             //是否已初始化
    private bool isLocalStarted;        //本机表现层是否已初始化
    public bool IsActive => IsServer && IsComplete; //主机合法运行判断

    //本地状态中转
    public bool isOnGround;  //在地面
    public bool isJumpDown;    //跳跃空格
    bool jumpRequest;          //本物理帧请求起跳
    public bool isMouse1Down;   //左键输入
    public bool isMouse2Down;   //右键输入
    public bool isMouseDown => isMouse1Down || isMouse2Down;
    public bool isShoulderDown;   //肩射输入
    public bool isAdsDown;        //开镜输入
    public bool isAimDown => isMouse1Down || isMouse2Down || isShoulderDown || isAdsDown;   //举枪中
    public bool isWASDDowm;    //移动输入
    public bool isRuning;      //奔跑输入
    public bool isReload;       //换弹输入
    public bool isSquat;        //蹲下输入

    //同步表现状态
    NetworkVariable<byte> netShowState = new NetworkVariable<byte>();        //表现状态位
    NetworkVariable<Vector3> netAimPoint = new NetworkVariable<Vector3>();  //瞄准落点
    NetworkVariable<Vector3> netTargetPos = new NetworkVariable<Vector3>(); //锁定点
    NetworkVariable<bool> netHasTarget = new NetworkVariable<bool>();       //是否锁定
    NetworkVariable<float> netAimAngle = new NetworkVariable<float>();     //精度圈角度
    NetworkVariable<int> netHealth = new NetworkVariable<int>();            //玩家血量
    NetworkVariable<int> netAmmo = new NetworkVariable<int>();              //剩余弹药

    //本机表现状态
    float localFireTime;    //本机开火计时

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
        if (Con_ObjectSystem == null) Con_ObjectSystem = GetComponent<Object_System>();
        if (Con_ObjectSystem == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Object_System");
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

        // 注册生命事件
        if (Con_ObjectSystem != null) Con_ObjectSystem.HealthEnd += OnHealthEnd;

        // 初始化同步
        if (IsServer && Con_ObjectSystem != null) netHealth.Value = Con_ObjectSystem.HP;
        if (IsServer && Con_gun_Control != null) netAmmo.Value = Con_gun_Control.ammo;

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
        Con_localInputEvent.AimPose_event += OnLocalAimPose;
        Con_localInputEvent.Fire_event += OnLocalFire;

        Con_player_camera.Player_camera_Start(Head);    //相机跟随头部

        // 本机瞄准圈
        if (Player_Main.player_Main == null || Player_Main.player_Main.oUI_RectTransform == null)
        {
            Debug.LogError("Player_Control|Player_Control_Local_Init|准星UI 为空");
        }
        else
        {
            if (Con_aimRing_UI == null) Con_aimRing_UI = gameObject.AddComponent<Aim_Ring_UI>();
            Con_aimRing_UI.Aim_Ring_UI_Init(Con_gun_Control, Player_Main.player_Main.oUI_RectTransform, Con_camera);
        }

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Player_Control|Player_Control_Local_Init|完成初始化");
    }

    // 客户端每帧本机表现层更新
    public void Player_Control_LocalShow_Update()
    {
        if (!IsLocalPlayer) return;

        // 相机跟随
        if (Con_player_camera != null) Con_player_camera.Camera_Follow_Performance_Local();

        // 姿态本地预测，避免按键延迟
        if (Con_gun_Control != null && Con_input_Manage != null)
            Con_gun_Control.SetAimState(Con_input_Manage.ShoulderHeld, Con_input_Manage.AdsHeld);

        // 瞄准圈
        if (Con_aimRing_UI != null) Con_aimRing_UI.Aim_Ring_UI_Show();
    }

    // 客户端每帧各端表现层更新
    public void Player_Control_Show_Update()
    {
        byte state = IsOwner ? PackLocalShowState() : netShowState.Value;   //本机用本地输入预测

        // 动画
        if (Con_player_Animation != null) Con_player_Animation.Player_animation_Show(state);

        // 血量同步到本地
        if (!IsServer && Con_ObjectSystem != null) Con_ObjectSystem.HP = netHealth.Value;

        if (Con_gun_Control == null) return;

        // 弹药与瞄准数据同步到本地
        if (!IsServer)
        {
            Con_gun_Control.ammo = netAmmo.Value;
            Con_gun_Control.SetAimShow(netHasTarget.Value, netTargetPos.Value, netAimAngle.Value);
        }

        // 举枪状态
        if ((state & Player_animation.BitGun) != 0)
            Con_gun_Control.Gun_Aim_Performance(IsOwner ? Con_input_Manage.AimPoint : netAimPoint.Value);
        else Con_gun_Control.Gun_AimDown_Performance();

        Con_gun_Control.Gun_Fixed_Performance();
    }

    // 打包表现状态位
    byte PackShowState()
    {
        byte state = 0;
        if (isWASDDowm) state |= Player_animation.BitWalk;
        if (isRuning) state |= Player_animation.BitRun;
        if (isSquat) state |= Player_animation.BitSquat;
        if (isAimDown) state |= Player_animation.BitGun;
        if (isJumpDown) state |= Player_animation.BitJump;
        if (!isOnGround) state |= Player_animation.BitAir;
        return state;
    }

    // 打包本机表现状态位
    byte PackLocalShowState()
    {
        if (Con_input_Manage == null) return netShowState.Value;

        byte state = 0;
        if (Con_input_Manage.WASDHeld) state |= Player_animation.BitWalk;
        if (Con_input_Manage.RunHeld) state |= Player_animation.BitRun;
        if (Con_input_Manage.SquatHeld) state |= Player_animation.BitSquat;
        if (Con_input_Manage.MouseHeld || Con_input_Manage.ShoulderHeld || Con_input_Manage.AdsHeld) state |= Player_animation.BitGun;
        if (Con_input_Manage.JumpDownHeld) state |= Player_animation.BitJump;

        // 离地本机无权威检测，沿用同步值
        state |= (byte)(netShowState.Value & Player_animation.BitAir);
        return state;
    }

    // 重置位置与速度
    public void Player_Respawn_Date(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (rigidbody != null) rigidbody.velocity = Vector3.zero;
    }

    // 死亡事件，主机重置到复活点
    void OnHealthEnd()
    {
        if (!IsServer) return;      //主机处理

        if (Player_Main.player_Main == null)
        {
            Debug.LogError("Player_Control|OnHealthEnd|Player_Main 为空");
            return;
        }

        Player_Main.player_Main.Player_Respawn(this);
        Con_ObjectSystem.ResetHealth();
    }

    // 主机每帧各端逻辑更新
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



        // 同步表现状态
        netShowState.Value = PackShowState();

        // 传递枪械状态
        Con_gun_Control.SetAimState(isShoulderDown, isAdsDown);
        Con_gun_Control.SetMoveState(isSquat, isWASDDowm, isRuning);

        // 注入准星方向
        Vector3 aimDir = packet.aimPoint - Con_gun_Control.MuzzlePosition;
        Con_gun_Control.SetAimDirection(aimDir.sqrMagnitude > 0.001f ? aimDir : packet.viewDir);

        // 生命系统状态
        netHealth.Value = Con_ObjectSystem.HP;      //获取主机上各端玩家生命值
        netAmmo.Value = Con_gun_Control.ammo;       //获取主机上各端玩家弹药
        Con_ObjectSystem.Object_System_Update();

        // 锁敌
        UpdateAimTarget(aimDir);

        // 同步瞄准表现数据
        netHasTarget.Value = isAimDown && Con_gun_Control.HasTarget;   //举枪才显示锁圈
        netAimAngle.Value = Con_gun_Control.CurrentAngle;
        if (isAimDown)
        {
            netAimPoint.Value = packet.aimPoint;
            netTargetPos.Value = Con_gun_Control.TargetPos;
        }
    }
    // 主机每物理帧各端逻辑更新
    public void Player_Control_FixedUpdate()
    {
        if (!IsActive) return;

        InputPacket packet = Con_player_HostNetworkEvent.Packet;   //纯数据来源
        // 计算移动数据
        Con_body.Body_Move_Date(packet.move, packet.viewDir, isRuning, isSquat);

        Con_body.Body_Fixed_Date();
        Con_gun_Control.Gun_Fixed_Date();

        // 起跳，每物理帧最多一次
        if (jumpRequest)
        {
            jumpRequest = false;
            Con_body.Body_Jump_Date();
        }

        // 地面有输入才施推力
        if (isWASDDowm && Con_body.IsGrounded) Con_body.Body_Move();
    }


    // 本机肩射与开镜开关
    void OnLocalAimPose(bool shoulder, bool ads)
    {
        if (Con_player_camera != null) Con_player_camera.Camera_Aim_Performance_Local(shoulder, ads);
    }

    // 本机开火预测，按射速节流
    void OnLocalFire()
    {
        if (IsServer) return;               //主机已播放
        if (Con_gun_Control == null || Con_input_Manage == null) return;
        if (!Con_gun_Control.HasAmmo()) return;
        if (Time.time - localFireTime < 1f / Con_gun_Control.fireRate) return;   //未到射速

        localFireTime = Time.time;

        Con_gun_Control.Gun_Shoot_Local_Performance(Con_gun_Control.MuzzlePosition, Con_gun_Control.transform.forward);

        if (Con_player_camera != null) Con_player_camera.Camera_Shoot_Performance_Local();
    }

    // 开火，拼接逻辑与表现
    void Player_Fire()
    {
        if (Con_gun_Control == null || Con_player_HostNetworkEvent == null)
        {
            Debug.LogError("Player_Control|Player_Fire|gun 为空");
            return;
        }

        // 枪口瞄准方向由落点推算,落点-开火点
        Vector3 aimDir = Con_player_HostNetworkEvent.Packet.aimPoint - Con_gun_Control.MuzzlePosition;
        if (aimDir.sqrMagnitude <= 0.001f) aimDir = Con_player_HostNetworkEvent.Packet.viewDir;

        if (!Con_gun_Control.Gun_Shoot_Date(aimDir, out Vector3 origin, out Vector3 dir,
            out bool isHit, out Vector3 hitPoint, out Vector3 hitNormal)) return;

        Con_gun_Control.Gun_Shoot_Performance(origin, dir, isHit, hitPoint, hitNormal);

        // 本机开火震屏
        if (IsOwner && Con_player_camera != null) Con_player_camera.Camera_Shoot_Performance_Local();

        Gun_Shoot_ClientRpc(origin, dir, isHit, hitPoint, hitNormal);
    }

    // 客户端开火表现
    [ClientRpc]
    void Gun_Shoot_ClientRpc(Vector3 origin, Vector3 dir, bool isHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsServer) return;   //主机已播放

        if (Con_gun_Control == null) return;

        // 本机已预测枪口与震屏，只补弹道
        if (IsOwner)
        {
            Con_gun_Control.Gun_Shoot_Line_Performance(origin, dir, isHit, hitPoint, hitNormal);
            return;
        }

        Con_gun_Control.Gun_Shoot_Performance(origin, dir, isHit, hitPoint, hitNormal);
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
        Con_player_HostNetworkEvent.Shoulder_event += OnShoulder;         //肩射
        Con_player_HostNetworkEvent.Ads_event += OnAds;                   //开镜
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
        Con_player_HostNetworkEvent.Shoulder_event -= OnShoulder;
        Con_player_HostNetworkEvent.Ads_event -= OnAds;
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
            Con_localInputEvent.AimPose_event -= OnLocalAimPose;
            Con_localInputEvent.Fire_event -= OnLocalFire;
        }

        // 反注册生命事件
        if (Con_ObjectSystem != null) Con_ObjectSystem.HealthEnd -= OnHealthEnd;
    }



    // 输入事件包装-唯一行为方法

    // 移动，axis为输入轴
    void OnMove(Vector2 axis)
    {
        isWASDDowm = axis.sqrMagnitude >= 0.01f;

        if (Con_body == null)
        {
            Debug.LogError("Player_Control|OnMove|body 为空");
            return;
        }
        if (!isOnGround) return;

        // 无输入不转向
        if (!isWASDDowm) return;

        if (!isAimDown) Con_body.Body_Rotation_Performance();
    }

    // 跳跃，只记按下边沿
    void OnJumpDown(bool on)
    {
        if (on && !isJumpDown) jumpRequest = true;   //按下那一帧
        isJumpDown = on;
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

        Player_Fire();
    }

    // 右键只朝向
    void OnMouse2(bool on)
    {
        isMouse2Down = on;
    }

    // 肩射
    void OnShoulder(bool on)
    {
        isShoulderDown = on;
    }

    // 开镜
    void OnAds(bool on)
    {
        isAdsDown = on;
    }

    // 举枪瞄准
    void OnMouseHeld(bool on)
    {
        if (Con_body == null || Con_player_HostNetworkEvent == null) return;

        if (on) Con_body.Body_Aim_Performance(Con_player_HostNetworkEvent.Packet.viewDir);
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
        if (Con_gun_Control != null) Con_gun_Control.Gun_Reload_Date();
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

    // 找可瞄准的敌人，fwd为准星方向
    void UpdateAimTarget(Vector3 fwd)
    {
        Vector3 origin = Con_gun_Control.MuzzlePosition;
        Ray ray = new Ray(origin, fwd);

        // 准星射线上最近的命中
        RaycastHit[] hits = Physics.RaycastAll(ray, Con_gun_Control.range);

        float nearest = float.MaxValue;
        RaycastHit closest = default;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;   // 跳过自己
            if (h.distance >= nearest) continue;

            nearest = h.distance;
            closest = h;
        }

        // 最近命中的是敌人则锁定命中点
        if (nearest < float.MaxValue &&
            (enemyMask.value & (1 << closest.collider.gameObject.layer)) != 0)
        {
            Con_gun_Control.SetTarget(true, closest.point);
            return;
        }

        // 其次取外圈内可见敌人的最近点
        float outer = Con_gun_Control.OuterAngle * 0.5f;   //外圈按直径算

        Collider[] cols = Physics.OverlapSphere(origin, Con_gun_Control.range, enemyMask);

        float bestAngle = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(transform)) continue;

            // 准星射线上敌人所在深度处的点
            float depth = Vector3.Dot(col.bounds.center - origin, fwd);
            if (depth <= 0f) continue;

            // 敌人表面离准星最近的点
            Vector3 point = col.ClosestPoint(origin + fwd * depth);

            float angle = Vector3.Angle(fwd, point - origin);
            if (angle > outer || angle >= bestAngle) continue;
            if (!Visible(origin, point, col)) continue;

            bestAngle = angle;
            bestPoint = point;
        }

        Con_gun_Control.SetTarget(bestAngle < float.MaxValue, bestPoint);
    }

    // 判断目标点是否被挡
    bool Visible(Vector3 origin, Vector3 point, Collider target)
        => !Physics.Linecast(origin, point, out RaycastHit h) || h.collider == target;
}
