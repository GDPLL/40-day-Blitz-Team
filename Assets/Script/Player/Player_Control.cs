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
    public Camera Con_camera;                       // 本地相机引用
    public Gun_Control Con_gun_Control;             // 玩家枪械组件
    public Player_camera Con_player_camera;         // 玩家摄像机控制组件引用
    public Player_Body Con_body;                        // 本地玩家控制引用
    public Input_Manage Con_input_Manage;           // 本地全局输入引用
    public Player_animation Con_player_Animation;       // 本地动画引用

    // 本地对外变量
    [Header("头部位置")]
    public Transform Head;                          // 头部位置
    [Header("本地刚体")]
    public new Rigidbody rigidbody;                 // 刚体
    [Header("落地检测")]
    public float groundCheckDistance = 0.2f;  // 落地检测距离
    [Header("目标检测")]
    public LayerMask enemyMask;      // 敌人层

    //本地变量
    private RectTransform UIFocus;                   // 准星UI

    private bool IsLocalPlayer;         //本地对象判断
    private bool IsComplete;            //组件层完善判断
    public bool IsActive => IsLocalPlayer && IsComplete; //允许运行判断

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

    // 初始化组件与输入
    public void Player_Control_Start(Camera _camera, Input_Manage input_Manage, RectTransform UIFo, Player_camera player_Camera)
    {
        Con_player_camera = player_Camera;  //玩家相机控制器
        UIFocus = UIFo;                     //准心
        Con_input_Manage = input_Manage;    //全局输入单例
        Con_camera = _camera;                //相机引用

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
        if (UIFocus == null)
        {
            Debug.LogError("Player_Control|Start|UIFocus 为空");
            IsComplete = false;
        }
        if (Con_camera == null)
        {
            Debug.LogError("Player_Control|Start|Con_camera 为空");
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
        if (Con_player_camera == null)
        {
            Debug.LogError("Player_Control|Start|Con_player_camera 为空");
            IsComplete = false;
        }
        if (rigidbody == null)
        {
            Debug.LogError("Player_Control|Start|rigidbody 为空");
            IsComplete = false;
        }

        // 非本地玩家不控制
        IsLocalPlayer = Con_netObj != null && Con_netObj.IsOwner;
        if (!IsLocalPlayer)
        {
            Debug.Log("非本地玩家，已禁用本地控制");
            return;
        }

        // 锁定并隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 注册输入事件
        RegisterInputEvents();

        if (Con_body != null) Con_body.Body_Init(Con_camera, UIFocus, rigidbody);
        if (Con_gun_Control != null) Con_gun_Control.Gun_Control_Init(UIFocus);

        Debug.Log("Player_Control|Player_Control_Start|完成初始化");
    }

    // 每帧更新本地控制
    public void Player_Control_Update()
    {
        if (!IsActive) return;   // 非法不运行

        // 等相机绑定后再控制
        if (Con_camera == null)
        {
            Debug.LogError("Player_Control|Update|Player_camera 为空");
            return;
        }

        // 落地检测
        isOnGround = IsGrounded();

        if (Con_input_Manage == null || Con_body == null || Con_gun_Control == null || Con_player_Animation == null)
        {
            Debug.LogError("Player_Control|Update|input/body/gun_Control/player_Animation 为空");
            return;
        }

        // 计算移动数据
        Con_body.Player_Body_Update(Con_input_Manage.MoveAxis, isRuning, isSquat);

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
        if (Con_input_Manage == null)
        {
            Debug.LogError("Player_Control|RegisterInputEvents|input 为空");
            return;
        }

        Con_input_Manage.Move_event += OnMove;                 //移动
        Con_input_Manage.JumpDown_event += OnJumpDown;         //跳跃
        Con_input_Manage.Mouse1_event += OnMouse1;             //左键
        Con_input_Manage.Mouse2_event += OnMouse2;             //右键
        Con_input_Manage.MouseHeld_event += OnMouseHeld;       //鼠标按住
        Con_input_Manage.Run_event += OnRun;                   //奔跑
        Con_input_Manage.ReloadHeld_event += OnReloadHeld;     //换弹
        Con_input_Manage.Squat_event += OnSquat;               //蹲下
    }

    // 反注册输入事件
    void UnregisterInputEvents()
    {
        if (Con_input_Manage == null) return;

        Con_input_Manage.Move_event -= OnMove;
        Con_input_Manage.JumpDown_event -= OnJumpDown;
        Con_input_Manage.Mouse1_event -= OnMouse1;
        Con_input_Manage.Mouse2_event -= OnMouse2;
        Con_input_Manage.MouseHeld_event -= OnMouseHeld;
        Con_input_Manage.Run_event -= OnRun;
        Con_input_Manage.ReloadHeld_event -= OnReloadHeld;
        Con_input_Manage.Squat_event -= OnSquat;
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
            isWASDDowm = Con_input_Manage.WASDHeld;
        }
        catch (Exception e)
        {
            Debug.LogError($"Player_Control|OnMove|{e}");
        }
        if (Con_body == null)
        {
            Debug.LogError("Player_Control|OnMove|body 为空");
            return;
        }
        if (isOnGround)
        {
            Con_body.Body_calculateVectorMove(axis);
            Con_body.Body_Move();
            if (!isMouseDown) Con_body.Body_rotation();
        }
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
        if (Con_gun_Control.Shoot())
        {
            if (Con_player_camera != null) Con_player_camera.Shake();
        }
    }

    // 右键肩射
    void OnMouse2(bool on)
    {
        isMouse2Down = on;
        if (Con_player_camera != null) Con_player_camera.SetShoulderAim(isMouse2Down);
        if (Con_gun_Control != null)
        {
            Con_gun_Control.SetRecoilReduction(isMouse2Down);
        }
    }

    // 举枪瞄准
    void OnMouseHeld(bool on)
    {
        if (Con_body == null || Con_gun_Control == null) return;

        if (on)
        {
            Con_body.Body_calculateVectorCamera();
            Con_body.Body_rotationWithFocus();
            Con_gun_Control.AimAt();
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
        if (Con_input_Manage != null) isReload = Con_input_Manage.ReloadHeld;
        if (isReload && Con_gun_Control != null) Con_gun_Control.Reload();
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
