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
    public Object_System Con_ObjectSystem;          // 生命系统
    public Aim_Ring_UI Con_aimRing_UI;              // 本机瞄准圈显示
    public Hit_Feedback_UI Con_hitFeedback_UI;      // 本机命中反馈
    public Lock_On_UI Con_lockOn_UI;                // 本机锁定提示
    public Player_Audio Con_player_Audio;           // 玩家音效表现
    public Player_XRay Con_xRay;                    // 本机透视
    public Player_Net_State Con_net_State;          // 网络数据仓库
    public Player_Move_Date Con_move;               // 移动驱动
    public Player_Lock_Date Con_lock;               // 锁定机制
    public Player_Combat_Date Con_combat;           // 战斗结果
    public Player_Reconcile_Date Con_reconcile;     // 位置纠偏

    // 本地对外变量
    [Header("头部位置")]
    public Transform Head;                          // 头部位置
    [Header("本地相机")]
    public Camera Con_camera;                       // 本地相机引用
    public Player_camera Con_player_camera;         // 玩家相机控制组件
    [Header("本地刚体")]
    public new Rigidbody rigidbody;                 // 刚体
    [Header("目标检测")]
    public LayerMask enemyMask;      // 敌人层

    //复活点编号，各端一致
    public int RespawnIndex => IsServer ? Con_ObjectSystem.respawnIndex : Con_net_State.RespawnIndex;

    //玩家编号，按加入编号，界面显示用
    public int Player_Number => (int)OwnerClientId + 1;

    //对外读取弹夹数量
    public int MagazineCount
    {
        get
        {
            if (Con_gun_Control == null) return 0;
            if (IsServer) return Con_gun_Control.magazineCount;
            return Con_net_State != null ? Con_net_State.Magazine : Con_gun_Control.magazineCount;
        }
    }

    public int MaxMagazineCount =>
        Con_gun_Control != null ? Con_gun_Control.maxMagazineCount : 0;

    //本地变量
    private bool IsComplete;            //组件层完善判断
    private bool isStarted;             //是否已初始化
    private bool isLocalStarted;        //本机表现层是否已初始化
    public bool IsActive => IsServer && IsComplete; //主机合法运行判断

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
        if (Con_player_Audio == null) Con_player_Audio = GetComponent<Player_Audio>();
        if (Con_player_Audio == null) Debug.LogError("Player_Control|Start|未找到 Player_Audio");

        if (Con_net_State == null) Con_net_State = GetComponent<Player_Net_State>();
        if (Con_net_State == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Player_Net_State");
            IsComplete = false;
        }

        if (Con_move == null) Con_move = GetComponent<Player_Move_Date>();
        if (Con_move == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Player_Move_Date");
            IsComplete = false;
        }

        if (Con_lock == null) Con_lock = GetComponent<Player_Lock_Date>();
        if (Con_lock == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Player_Lock_Date");
            IsComplete = false;
        }

        if (Con_combat == null) Con_combat = GetComponent<Player_Combat_Date>();
        if (Con_combat == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Player_Combat_Date");
            IsComplete = false;
        }

        if (Con_reconcile == null) Con_reconcile = GetComponent<Player_Reconcile_Date>();
        if (Con_reconcile == null)
        {
            Debug.LogError("Player_Control|Start|未找到 Player_Reconcile_Date");
            IsComplete = false;
        }

        // 注入上层控制，供各组件自行读取状态
        if (Con_body != null) Con_body.Owner_Init(this);
        if (Con_gun_Control != null) Con_gun_Control.Owner_Init(this);
        if (Con_player_Animation != null) Con_player_Animation.Owner_Init(this);
        if (Con_ObjectSystem != null) Con_ObjectSystem.Owner_Init(this);
        if (Con_player_Audio != null) Con_player_Audio.Owner_Init(this);
        if (Con_net_State != null) Con_net_State.Owner_Init(this);
        if (Con_move != null) Con_move.Owner_Init(this);
        if (Con_lock != null) Con_lock.Owner_Init(this);
        if (Con_combat != null) Con_combat.Owner_Init(this);
        if (Con_reconcile != null) Con_reconcile.Owner_Init(this);

        // 注册输入包接收
        if (Con_player_HostNetworkEvent != null) Con_player_HostNetworkEvent.Host_Input_Init(Con_input_Manage);

        // 注册生命事件
        if (Con_combat != null) Con_combat.Combat_Init(Con_ObjectSystem);

        // 初始化同步
        if (IsServer && Con_net_State != null && Con_ObjectSystem != null && Con_gun_Control != null && Con_move != null)
            Con_net_State.Init_Push(Con_ObjectSystem.HP, Con_gun_Control.ammo,
                Con_gun_Control.magazineCount, Con_ObjectSystem.respawnIndex);

        if (Con_body != null) Con_body.Body_Init(rigidbody);
        if (Con_gun_Control != null)
        {
            Con_gun_Control.Gun_Control_Init(OwnerClientId);//持有者编号
        }
        if (Con_player_Audio != null) Con_player_Audio.Player_Audio_Init(Con_gun_Control);

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

            // 本机命中与受击反馈
            if (Con_hitFeedback_UI == null) Con_hitFeedback_UI = gameObject.AddComponent<Hit_Feedback_UI>();
            Con_hitFeedback_UI.Hit_Feedback_UI_Init(Player_Main.player_Main.oUI_RectTransform, Con_camera);
        }

        // 本机锁定提示
        if (Con_lockOn_UI == null) Con_lockOn_UI = gameObject.AddComponent<Lock_On_UI>();

        // 本机透视
        if (Con_xRay == null) Con_xRay = gameObject.AddComponent<Player_XRay>();
        Con_xRay.Player_XRay_Init(Con_camera, enemyMask);

        // 客机本地预测，位置自己算
        if (Con_reconcile != null) Con_reconcile.Reconcile_Init();

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Player_Control|Player_Control_Start_Own|完成初始化");
    }


    // 重置方法
    public void Player_Respawn_Date(Vector3 position, Quaternion rotation)
    {
        Con_move.Reset_State(position, rotation);

        // 复活恢复满弹夹与满弹匣
        if (Con_gun_Control != null)
        {
            Con_gun_Control.Gun_Respawn_Reset();
            if (IsServer && Con_net_State != null)
            {
                Con_net_State.Ammo_Magazine_Push(
                    Con_gun_Control.ammo, Con_gun_Control.magazineCount);

                // 本机客机同步清除换弹计时与弹药显示
                ClientRpcParams ps = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
                };
                Respawn_Weapon_ClientRpc(ps);
            }
        }

        // 客机强制瞬移
        if (Con_reconcile != null) Con_reconcile.Teleport_Push(position, rotation);
    }

    // 复活时重置本机枪械状态
    [ClientRpc]
    void Respawn_Weapon_ClientRpc(ClientRpcParams ps = default)
    {
        if (IsServer) return;   //主机本地已经重置
        if (Con_gun_Control != null) Con_gun_Control.Gun_Respawn_Reset();
    }


    // 各端每帧数据采集，各端各玩家都跑
    public void Player_Control_Date_Update()
    {
        if (Con_move != null) Con_move.Move_Update();
        if (IsOwner && !IsServer && Con_input_Manage != null)
            Aim_Predict_Date(Con_input_Manage.LastPacket);
        if (Con_gun_Control != null) Con_gun_Control.Gun_Update();
    }

    // 客机本机用本地世界数据预测锁定目标
    void Aim_Predict_Date(InputPacket packet)
    {
        if (Con_gun_Control == null || Con_lock == null) return;
        if (Con_ObjectSystem != null &&
            (Con_ObjectSystem.isDead ||
             (Con_net_State != null && (Con_net_State.RespawnWait > 0f || Con_net_State.Is_Out))))
            return;

        // 本机预测直接取当前输入，避免与表现层混用上一包按键状态
        if (Con_input_Manage != null)
        {
            InputPacket localPacket = Con_input_Manage.GetPacket();
            localPacket.inputSeq = packet.inputSeq;
            localPacket.clientTick = packet.clientTick;
            packet = localPacket;
        }

        Con_gun_Control.SetAimState(packet.shoulder, packet.ads);

        Vector3 origin = packet.viewPos != Vector3.zero
            ? packet.viewPos
            : Con_gun_Control.MuzzlePosition;
        Vector3 aimDir = packet.aimPoint - origin;
        if (aimDir.sqrMagnitude <= 0.001f) aimDir = packet.viewDir;

        Con_gun_Control.SetAimDirection(aimDir);
        Con_gun_Control.SetAimOrigin(origin);

        bool aimDown = packet.mouse1 || packet.mouse2 || packet.shoulder || packet.ads;
        Con_lock.Target_Date(origin, aimDir, packet.aimPoint, Con_gun_Control.range,
            Con_gun_Control.OuterAngle, out bool hasTarget, out Vector3 targetPos);

        ulong targetId = hasTarget ? Con_lock.Current_Target_Id : ulong.MaxValue;
        Con_gun_Control.Target_Push(aimDown && hasTarget, targetPos, targetId);
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

        // 表现状态先下发，死亡期间也要同步
        Con_net_State.Show_State_Push();

        // 死亡与毒气，返回true跳过后续
        if (Con_ObjectSystem.Objcet_System_Update()) return;

        InputPacket packet = Con_player_HostNetworkEvent.Packet;   //纯数据来源

        // 落地检测
        Con_move.Ground_Date();

        // 空中超致死速度算失重
        Con_move.isWeightLess = rigidbody != null &&
            Con_ObjectSystem.WeightLess_Date(rigidbody.velocity.magnitude, Con_move.isOnGround);

        // 本机坠地音效
        if (IsOwner && Con_player_Audio != null && rigidbody != null)
            Con_player_Audio.Land_Check(rigidbody.velocity.y, Con_move.isOnGround);

        // 传递枪械状态
        Con_gun_Control.SetAimState(packet.shoulder, packet.ads);
        Con_gun_Control.SetMoveState(Con_move.isSquat, Con_move.isWASDDowm, Con_move.isRuning && !Con_gun_Control.IsReloading);   //换弹按走路算精度

        // 锁定基准取相机
        Vector3 origin = packet.viewPos != Vector3.zero ? packet.viewPos : Con_gun_Control.MuzzlePosition;

        // 准星方向取相机到落点
        Vector3 aimDir = packet.aimPoint - origin;
        if (aimDir.sqrMagnitude <= 0.001f) aimDir = packet.viewDir;

        Con_gun_Control.SetAimDirection(aimDir);
        Con_gun_Control.SetAimOrigin(origin);

        // 复活点随楼层上移，进度可提前
        if (Player_level.Instance != null)
        {
            int idx = Con_ObjectSystem.respawnIndex;
            int floor = Mathf.Max(idx / 10, Player_level.Instance.Respawn_Floor);
            Con_ObjectSystem.respawnIndex = idx % 10 + floor * 10;
            Con_net_State.Respawn_Index_Push(Con_ObjectSystem.respawnIndex);
        }

        // 生命系统状态
        Con_ObjectSystem.Object_System_Update();

        // 锁敌，结果由Control推给枪械
        if (Con_lock != null)
        {
            bool aimDown = packet.mouse1 || packet.mouse2 || packet.shoulder || packet.ads;

            Con_lock.Target_Date(origin, aimDir, packet.aimPoint, Con_gun_Control.range, Con_gun_Control.OuterAngle,
                out bool hasTarget, out Vector3 targetPos);

            Con_gun_Control.Target_Push(hasTarget, targetPos, Con_lock.Current_Target_Id);
            Con_lock.Lock_On_Date(aimDown, Con_gun_Control.CurrentAngle, Con_gun_Control.InnerAngle);
        }

        // 数据更新末尾统一写入同步
        Con_net_State.Host_State_Push();
    }

    // 主机每物理帧各端逻辑更新
    public void Player_Control_Date_HostFixedUpdate()
    {
        // 客机本机预测
        if (IsOwner && !IsServer && Con_input_Manage != null)
            Player_Control_Date_ClientFixedUpdate_Own(Con_input_Manage.LastPacket);

        // 翻滚死亡失重共用碰撞体缩放
        Con_move.Body_Size_Date(Con_ObjectSystem.isDead);

        if (!IsActive) return;
        if (Con_ObjectSystem.isDead) return;     //等待复活不驱动

        // 回传权威位置，10Hz
        if (Con_reconcile != null) Con_reconcile.Reconcile_Host_Fixed();

        InputPacket packet = Con_player_HostNetworkEvent.Packet;   //纯数据来源

        Con_gun_Control.Gun_Fixed_Date();

        // 帧内仲裁并执行瞬移申请
        Con_move.Teleport_Resolve();

        // 移动驱动，含坠落伤害
        Con_move.Move_Host_Fixed(packet, Con_gun_Control.IsReloading);

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

        // 本机信号花屏
        if (Con_player_camera != null && Con_ObjectSystem != null)
            Con_player_camera.Camera_Signal_Performance_Local(Con_ObjectSystem.HP, Con_net_State.Is_Out);

        // 姿态本地预测，避免按键延迟
        if (Con_gun_Control != null && Con_input_Manage != null)
            Con_gun_Control.SetAimState(Con_input_Manage.ShoulderHeld, Con_input_Manage.AdsHeld);

        // 瞄准圈
        if (Con_aimRing_UI != null) Con_aimRing_UI.Aim_Ring_UI_Show();

        // 回血边缘提示
        if (Con_hitFeedback_UI != null && Con_ObjectSystem != null)
            Con_hitFeedback_UI.Heal_Edge_Performance_Local(Con_ObjectSystem.HP, Con_ObjectSystem.healCap);

        // 命中与受击反馈
        if (Con_hitFeedback_UI != null) Con_hitFeedback_UI.Hit_Feedback_UI_Show(Con_net_State.Score);

        // 呼吸与咳嗽，血量与毒气驱动
        if (Con_player_Audio != null && Con_ObjectSystem != null)
        {
            bool inGas = Player_level.Instance != null
                && transform.position.y < Player_level.Instance.Gas_Height;
            Con_player_Audio.Player_Audio_Body_Show(Con_ObjectSystem.HP, Con_ObjectSystem.healCap, inGas);
        }

        // 速度越大风声越响
        if (Con_player_Audio != null && rigidbody != null)
            Con_player_Audio.Wind_Performance(rigidbody.velocity.magnitude);

        // 本机透视
        if (Con_xRay != null)
        {
            if (Player_Main.player_Main == null)
                Debug.LogError("Player_Control|Player_Control_Show_ClientUpdate_Own|Player_Main 为空");
            else
                Con_xRay.Player_XRay_Show(Player_Main.player_Main.oplayer_Controls, OwnerClientId);
        }
    }

    // 客户端每帧各端表现层更新
    public void Player_Control_Show_ClientUpdate()
    {
        ushort state = IsOwner ? Con_net_State.Show_State_Local() : Con_net_State.Show_State;   //本机用本地输入预测

        // 本机用本地垂直速度，其余读下发值
        float verticalSpeed = IsOwner && rigidbody != null ? rigidbody.velocity.y : Con_net_State.VerticalSpeed;

        // 动画，垂直速度驱动失重与坠落
        if (Con_player_Animation != null)
            Con_player_Animation.Player_animation_Show(state, verticalSpeed);

        // 脚步与装备晃动
        if (Con_player_Audio != null) Con_player_Audio.Player_Audio_Show(state);

        // 血量同步到本地
        if (!IsServer && Con_ObjectSystem != null) Con_ObjectSystem.HP = Con_net_State.Health;

        if (Con_gun_Control == null) return;

        // 换弹期间松开手臂IK，各端按同步状态驱动
        Con_gun_Control.Arm_Rig_Date((state & Player_animation.BitReload) == 0);

        // 弹药与瞄准数据同步到本地
        if (!IsServer)
        {
            Con_gun_Control.ammo = Con_net_State.Ammo;
            Con_gun_Control.magazineCount = Con_net_State.Magazine;
            if (!IsOwner)
                Con_gun_Control.SetAimShow(Con_net_State.HasTarget, Con_net_State.TargetPos, Con_net_State.AimAngle);
        }

        // 举枪，锁定时指锁定点
        if ((state & Player_animation.BitGun) != 0)
        {
            Vector3 aimPoint = IsOwner ? Con_input_Manage.AimPoint : Con_net_State.AimPoint;

            // 锁定点须在枪前方
            Vector3 muzzle = Con_gun_Control.MuzzlePosition;
            if (Con_gun_Control.HasTarget &&
                Vector3.Dot(Con_gun_Control.TargetPos - muzzle, aimPoint - muzzle) > 0f)
                aimPoint = Con_gun_Control.TargetPos;

            // 换弹时不跟准星，枪指身体正前方
            if (Con_gun_Control.IsReloading) aimPoint = muzzle + transform.forward * 10f;

            Con_gun_Control.Gun_Aim_Performance(aimPoint);
        }
        else Con_gun_Control.Gun_AimDown_Performance();

        Con_gun_Control.Gun_Fixed_Performance();
    }

    // 客户端每物理帧本机逻辑更新
    public void Player_Control_Date_ClientFixedUpdate_Own(InputPacket packet)
    {
        if (Con_body == null)
        {
            Debug.LogError("Player_Control|Player_Control_Date_ClientFixedUpdate_Own|body 为空");
            return;
        }
        // 翻滚死亡失重共用碰撞体缩放
        Con_move.Body_Size_Date(Con_net_State.RespawnWait > 0f);

        if (Con_net_State.RespawnWait > 0f) return;    //等待复活不驱动

        // 本机换弹计时
        if (Con_gun_Control != null) Con_gun_Control.Gun_Reload_Tick(Time.fixedDeltaTime);

        // 移动，与主机同一套逻辑，含落地失重与坠地音效
        Con_move.Move_ClientOwn_Fixed_Predict(packet, Con_gun_Control.IsReloading);

        // 移动后重算本地瞄准，再推进本机精度预测
        Aim_Predict_Date(packet);
        Con_gun_Control.Gun_Aim_Predict_Fixed(Time.fixedDeltaTime);

        // 场景交互，索道等本机自己走
        Player_Scene_Interaction_Date(GetComponent<Collider>());

        if (Con_reconcile == null || Con_move == null) return;

        // 计算纠偏，超阈值转为瞬移申请
        if (Con_reconcile.Reconcile_ClientOwn_Date(transform.position, out Vector3 target, out bool hard))
        {
            if (hard) Con_move.Teleport_Request(target, transform.rotation, Player_Move_Date.Priority_Reconcile);
            else Con_move.Reconcile_Move_Date(target);
        }

        // 帧内仲裁并执行瞬移申请
        Con_move.Teleport_Resolve();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (Con_lock != null) Con_lock.Lock_On_Release();     //退出解除锁定

        // 关闭本机透视
        if (Con_xRay != null) Con_xRay.Player_XRay_Clear();
    }
}
