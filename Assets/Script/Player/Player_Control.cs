using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Unity.Netcode;
using Unity.Netcode.Components;
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

    //复活点编号，各端一致
    public int RespawnIndex => IsServer ? respawnIndex : netRespawnIndex.Value;

    //玩家编号，界面显示用
    public int Player_Number => RespawnIndex % 10 + 1;

    //本地变量
    private bool IsComplete;            //组件层完善判断
    private bool isStarted;             //是否已初始化
    private bool isLocalStarted;        //本机表现层是否已初始化
    public bool IsActive => IsServer && IsComplete; //主机合法运行判断

    //对外读取弹药与血量
    public int Ammo => Con_gun_Control != null ? Con_gun_Control.ammo : 0;       //剩余弹药
    public int MaxAmmo => Con_gun_Control != null ? Con_gun_Control.maxAmmo : 0; //弹匣容量
    public int HP => Con_ObjectSystem != null ? Con_ObjectSystem.HP : 0;         //当前血量
    public int MaxHp => Con_ObjectSystem != null ? Con_ObjectSystem.MaxHp : 0;   //血量上限

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
    NetworkVariable<int> netRespawnIndex = new NetworkVariable<int>();      //复活点编号

    //本机表现状态
    float localFireTime;    //本机开火计时

    //毒气
    float gasTimer;         //扣血计时

    //死亡与复活
    public int deathCount;                          //死亡次数
    bool isDead;                                    //等待复活中
    float respawnDelay;                             //本次等待秒数
    float respawnTimer;                             //等待计时
    NetworkVariable<float> netRespawnWait = new NetworkVariable<float>();   //等待剩余
    public float RespawnWait => netRespawnWait.Value;                       //对外读取

    //本机预测纠偏
     float reconcileRadius = 0.5f;    //误差阈值,米
     float reconcileSpeed = 5f;       //拉回速度,米每秒
     float reconcileSnap = 5f;        //超过此偏差直接归位,米
    Vector3 authPos;                        //主机权威位置
    int reconcileFrame;                     //回传计时

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
        if (IsServer) netRespawnIndex.Value = respawnIndex;     //复活点各端一致

        if (Con_body != null) Con_body.Body_Init(rigidbody);
        if (Con_gun_Control != null)
        {
            Con_gun_Control.Gun_Control_Init(OwnerClientId);//持有者编号
        }

        Debug.Log("Player_Control|Player_Control_Start|完成初始化");
    }

    // 本机初始化重载，带相机，只有本地玩家可调用
    public void Player_Control_Start_Own(Camera camera, Input_Manage inputManage, Player_camera playerCamera)
    {
        if (!IsLocalPlayer) return;         //非本机不跑表现层
        if (isLocalStarted) return;         //防重复初始化
        isLocalStarted = true;

        Con_camera = camera;                //本地相机
        Con_player_camera = playerCamera;   //相机控制组件

        Player_Control_Start(inputManage);  //通用初始化

        if (Con_camera == null)
        {
            Debug.LogError("Player_Control|Player_Control_Start_Own|Con_camera 为空");
            return;
        }
        if (Con_player_camera == null)
        {
            Debug.LogError("Player_Control|Player_Control_Start_Own|Con_player_camera 为空");
            return;
        }

        // 本机表现层事件分发
        if (Con_localInputEvent == null) Con_localInputEvent = gameObject.AddComponent<Local_InputEvent>();
        Con_localInputEvent.Local_Input_Init(Con_input_Manage);
        Con_localInputEvent.AimPose_event += OnLocalAimPose;

        // 本机预测事件
        RegisterPredictEvents();

        Con_player_camera.Player_camera_Start(Head);    //相机跟随头部

        // 本机瞄准圈
        if (Player_Main.player_Main == null || Player_Main.player_Main.oUI_RectTransform == null)
        {
            Debug.LogError("Player_Control|Player_Control_Start_Own|准星UI 为空");
        }
        else
        {
            if (Con_aimRing_UI == null) Con_aimRing_UI = gameObject.AddComponent<Aim_Ring_UI>();
            Con_aimRing_UI.Aim_Ring_UI_Init(Con_gun_Control, Player_Main.player_Main.oUI_RectTransform, Con_camera);
        }

        // 客机本地预测，位置自己算
        if (!IsServer && rigidbody != null)
        {
            NetworkTransform netTrans = GetComponent<NetworkTransform>();
            if (netTrans != null) netTrans.enabled = false;

            rigidbody.isKinematic = false;    // 本地非运动学控制
        }

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Player_Control|Player_Control_Start_Own|完成初始化");
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

    // 瞬移，刚体一起搬，防物理回写
    public void Player_Teleport_Date(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (rigidbody == null) return;

        rigidbody.position = position;      //非运动学刚体归物理管
        rigidbody.rotation = rotation;
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
    }

    // 重置位置与速度
    public void Player_Respawn_Date(Vector3 position, Quaternion rotation)
    {
        Player_Teleport_Date(position, rotation);

        if (!IsServer) return;      //只有主机下发

        // 客机强制瞬移
        ClientRpcParams ps = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        Player_TeleportClientRpc(position, rotation, ps);
    }


    // 死亡事件，主机开始等待复活
    void OnHealthEnd()
    {
        if (!IsServer) return;      //主机处理
        if (isDead) return;         //已在等待

        if (Player_Main.player_Main == null)
        {
            Debug.LogError("Player_Control|OnHealthEnd|Player_Main 为空");
            return;
        }

        // 击杀者死亡计数归零
        Player_Main.player_Main.Player_Kill(Con_ObjectSystem.KillerId);

        // 等待时长随死亡次数翻倍，上限8秒
        deathCount++;
        respawnDelay = deathCount <= 1 ? 0f : Mathf.Min(Mathf.Pow(2f, deathCount - 1), 8f);
        respawnTimer = 0f;
        isDead = true;
        netRespawnWait.Value = respawnDelay;
    }

    // 击杀敌人，清空死亡计数
    public void Player_Death_Reset()
    {
        deathCount = 0;
    }

    // 主机每帧各端逻辑更新
    public void Player_Control_Date_HostUpdate()
    {
        if (!IsActive) return;   // 非法不运行

        if (Con_body == null || Con_gun_Control == null || Con_player_Animation == null || Con_player_HostNetworkEvent == null)
        {
            Debug.LogError("Player_Control|Update|body/gun/Animation/HostNetworkEvent 为空");
            return;
        }

        // 死亡等待复活，9分钟后不再复活
        if (isDead)
        {
            if (Player_level.Instance != null && Player_level.Instance.Respawn_Off) return;

            respawnTimer += Time.deltaTime;
            netRespawnWait.Value = Mathf.Max(respawnDelay - respawnTimer, 0f);
            if (respawnTimer < respawnDelay) return;

            isDead = false;
            netRespawnWait.Value = 0f;
            Con_ObjectSystem.ResetHealth();
            Player_Main.player_Main.Player_Respawn(this);
            return;
        }

        // 毒气，低于毒气高度每秒扣血
        if (Player_level.Instance != null && transform.position.y < Player_level.Instance.Gas_Height)
        {
            gasTimer += Time.deltaTime;
            if (gasTimer >= 1f)
            {
                gasTimer = 0f;
                Con_ObjectSystem.Takedamage(Player_level.Instance.gasDamage, 0);
            }
        }
        else gasTimer = 0f;

        InputPacket packet = Con_player_HostNetworkEvent.Packet;   //纯数据来源

        // 落地检测
        isOnGround = IsGrounded();

        // 同步表现状态
        netShowState.Value = PackShowState();

        // 传递枪械状态
        Con_gun_Control.SetAimState(isShoulderDown, isAdsDown);
        Con_gun_Control.SetMoveState(isSquat, isWASDDowm, isRuning);

        // 锁定基准取相机
        Vector3 origin = packet.viewPos != Vector3.zero ? packet.viewPos : Con_gun_Control.MuzzlePosition;

        // 准星方向取相机到落点
        Vector3 aimDir = packet.aimPoint - origin;
        if (aimDir.sqrMagnitude <= 0.001f) aimDir = packet.viewDir;

        Con_gun_Control.SetAimDirection(aimDir);
        Con_gun_Control.SetAimOrigin(origin);

        // 复活点随楼层上移
        if (Player_level.Instance != null)
        {
            respawnIndex = respawnIndex % 10 + Player_level.Instance.Respawn_Floor * 10;
            netRespawnIndex.Value = respawnIndex;
        }

        // 生命系统状态
        netHealth.Value = Con_ObjectSystem.HP;      //获取主机上各端玩家生命值
        netAmmo.Value = Con_gun_Control.ammo;       //获取主机上各端玩家弹药
        Con_ObjectSystem.Object_System_Update();

        // 锁敌
        UpdateAimTarget(origin, aimDir);

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
    public void Player_Control_Date_HostFixedUpdate()
    {
        // 客机本机预测
        if (IsOwner && !IsServer && Con_input_Manage != null)
            Player_Control_Date_ClientFixedUpdate_Own(Con_input_Manage.LastPacket);

        if (!IsActive) return;
        if (isDead) return;     //等待复活不驱动

        // 回传权威位置，10Hz
        if (++reconcileFrame >= 5)
        {
            reconcileFrame = 0;

            ClientRpcParams ps = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            };
            Player_ReconcileClientRpc(transform.position, ps);
        }

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

        // 输入施推力
        if (isWASDDowm) Con_body.Body_Move();

        // 场景交互检测
        Player_Scene_Interaction_Date(GetComponent<Collider>());
    }

    // 场景交互检测，col为玩家碰撞体
    void Player_Scene_Interaction_Date(Collider col)
    {
        if (col == null)
        {
            Debug.LogError("Player_Control|Player_Scene_Interaction_Date|Collider 为空");
            return;
        }

        // 获取玩家对象碰撞并调用对应方法
        Collider[] hits = Physics.OverlapBox(col.bounds.center, col.bounds.extents, Quaternion.identity);
        foreach (Collider hit in hits)
        {
            IScene_Interaction interaction = hit.GetComponent<IScene_Interaction>();
            if (interaction != null) interaction.Scene_Interaction(this);
        }
    }

    // 客户端每帧本机表现层更新
    public void Player_Control_Show_ClientUpdate_Own()
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
    public void Player_Control_Show_ClientUpdate()
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

        // 举枪，锁定时指锁定点
        if ((state & Player_animation.BitGun) != 0)
        {
            Vector3 aimPoint = IsOwner ? Con_input_Manage.AimPoint : netAimPoint.Value;

            // 锁定点须在枪前方
            Vector3 muzzle = Con_gun_Control.MuzzlePosition;
            if (Con_gun_Control.HasTarget &&
                Vector3.Dot(Con_gun_Control.TargetPos - muzzle, aimPoint - muzzle) > 0f)
                aimPoint = Con_gun_Control.TargetPos;

            Con_gun_Control.Gun_Aim_Performance(aimPoint);
        }
        else Con_gun_Control.Gun_AimDown_Performance();

        Con_gun_Control.Gun_Fixed_Performance();
    }

    // 客机只本玩家物理帧更新，packet为本机输入
    public void Player_Control_Date_ClientFixedUpdate_Own(InputPacket packet)
    {
        if (Con_body == null)
        {
            Debug.LogError("Player_Control|Player_Control_Date_ClientFixedUpdate_Own|body 为空");
            return;
        }
        if (netRespawnWait.Value > 0f) return;    //等待复活不驱动

        // 移动，与主机同一套逻辑
        Con_body.Body_Move_Date(packet.move, packet.viewDir, isRuning, isSquat);
        Con_body.Body_Fixed_Date();
        isOnGround = Con_body.IsGrounded;
        if (isWASDDowm) Con_body.Body_Move();

        // 起跳
        if (jumpRequest)
        {
            jumpRequest = false;
            Con_body.Body_Jump_Date();
        }

        // 场景交互，索道等本机自己走
        Player_Scene_Interaction_Date(GetComponent<Collider>());

        // 向权威位置收敛
        Player_Reconcile_Date();
    }

    // 客机纠偏，只纠真正的漂移
    void Player_Reconcile_Date()
    {
        if (authPos == Vector3.zero) return;    //还没收到过

        Vector3 offset = authPos - transform.position;
        if (offset.magnitude < reconcileRadius) return;

        // 偏差过大直接归位，不拖拽
        if (offset.magnitude > reconcileSnap)
        {
            Player_Teleport_Date(authPos, transform.rotation);
            return;
        }

        // 平滑回正，走物理接口避免硬拉
        Vector3 next = transform.position + offset.normalized
            * Mathf.Min(reconcileSpeed * Time.fixedDeltaTime, offset.magnitude);

        if (rigidbody != null) rigidbody.MovePosition(next);
        else transform.position = next;
    }

    // 客机同步瞬移，复活与传送用
    [ClientRpc]
    void Player_TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams ps = default)
    {
        if (IsServer) return;

        Player_Teleport_Date(position, rotation);

        authPos = position;    //同步纠偏基准，防止被拉回
    }

    // 回传权威位置
    [ClientRpc]
    void Player_ReconcileClientRpc(Vector3 position, ClientRpcParams ps = default)
    {
        if (IsServer) return;

        authPos = position;
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

        Vector3 flatAim = new Vector3(aimDir.x, 0f, aimDir.z);
        Vector3 flatView = new Vector3(
            Con_player_HostNetworkEvent.Packet.viewDir.x, 0f,
            Con_player_HostNetworkEvent.Packet.viewDir.z);

        if (flatAim.sqrMagnitude <= 0.001f || Vector3.Dot(flatAim, flatView) <= 0f)
            aimDir = Con_player_HostNetworkEvent.Packet.viewDir;

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

    // 注册本机预测事件
    void RegisterPredictEvents()
    {
        if (IsServer || !IsLocalPlayer) return;     //只有客机本机走预测
        if (Con_localInputEvent == null)
        {
            Debug.LogError("Player_Control|RegisterPredictEvents|Local_InputEvent 为空");
            return;
        }

        Con_localInputEvent.Move_event += OnMove;              //移动
        Con_localInputEvent.JumpDown_event += OnJumpDown;      //跳跃
        Con_localInputEvent.Mouse1_event += OnMouse1;          //左键
        Con_localInputEvent.Mouse2_event += OnMouse2;          //右键
        Con_localInputEvent.MouseHeld_event += OnMouseHeld;    //鼠标按住
        Con_localInputEvent.Shoulder_event += OnShoulder;      //肩射
        Con_localInputEvent.Ads_event += OnAds;                //开镜
        Con_localInputEvent.Run_event += OnRun;                //奔跑
        Con_localInputEvent.Squat_event += OnSquat;            //蹲下
    }

    // 反注册本机预测事件
    void UnregisterPredictEvents()
    {
        if (Con_localInputEvent == null) return;

        Con_localInputEvent.Move_event -= OnMove;
        Con_localInputEvent.JumpDown_event -= OnJumpDown;
        Con_localInputEvent.Mouse1_event -= OnMouse1;
        Con_localInputEvent.Mouse2_event -= OnMouse2;
        Con_localInputEvent.MouseHeld_event -= OnMouseHeld;
        Con_localInputEvent.Shoulder_event -= OnShoulder;
        Con_localInputEvent.Ads_event -= OnAds;
        Con_localInputEvent.Run_event -= OnRun;
        Con_localInputEvent.Squat_event -= OnSquat;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        UnregisterInputEvents();
        UnregisterPredictEvents();

        // 反注册本机表现事件
        if (Con_localInputEvent != null)
        {
            Con_localInputEvent.AimPose_event -= OnLocalAimPose;
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

        if (IsServer) Player_Fire();    //主机算伤害
        else OnLocalFire();             //客机只本机表现
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
        if (Con_body == null || Con_input_Manage == null || Con_player_HostNetworkEvent == null) return;
        if (!isAimDown) return;

        // 举枪就朝视角转，主机取网络包，客机取本机包
        Vector3 viewDir = IsServer ? Con_player_HostNetworkEvent.Packet.viewDir : Con_input_Manage.LastPacket.viewDir;
        Con_body.Body_Aim_Performance(viewDir);
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

    // 找可瞄准的敌人
    void UpdateAimTarget(Vector3 origin, Vector3 fwd)
    {
        fwd = fwd.normalized;

        // 准星落点在敌人身上锁落点
        Vector3 aimPoint = Con_player_HostNetworkEvent.Packet.aimPoint;
        Collider[] atAim = Physics.OverlapSphere(aimPoint, 0.2f, enemyMask);

        foreach (Collider col in atAim)
        {
            if (col.transform.IsChildOf(transform)) continue;

            Con_gun_Control.SetTarget(true, col.ClosestPoint(aimPoint));
            return;
        }

        Ray ray = new Ray(origin, fwd);

        // 准星射线上最近的命中
        RaycastHit[] hits = Physics.RaycastAll(ray, Con_gun_Control.range);

        float nearest = float.MaxValue;
        RaycastHit closest = default;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
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

        // 取外圈内可见敌人的最近点
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

    readonly RaycastHit[] hitBuffer = new RaycastHit[16];   //遮挡检测缓存

    // 判断目标点是否被挡
    bool Visible(Vector3 origin, Vector3 point, Collider target)
    {
        Vector3 dir = point - origin;
        if (dir.sqrMagnitude <= 0.001f) return true;

        int count = Physics.RaycastNonAlloc(origin, dir.normalized, hitBuffer, dir.magnitude);

        for (int i = 0; i < count; i++)
        {
            if (hitBuffer[i].collider == target) continue;
            if (hitBuffer[i].collider.transform.IsChildOf(transform)) continue;   // 忽略自身

            return false;
        }

        return true;
    }
}
