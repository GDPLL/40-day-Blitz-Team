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
    public Rigidbody rigidbody;                  // 刚体
    public Camera Player_camera;                 // 相机
    public Gun_Control gun_Control;              // 枪械
    public Player_camera camShake;               // 相机震动
    public Player_Body body;                     // 本体
    public Player_Input input;                   // 输入
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
    public bool isAimDown => isMouse1Down || isMouse2Down || isShoulderDown || isAdsDown;   //举枪中
    public bool isWASDDowm;    //移动输入
    public bool isRuning;      //奔跑输入
    public bool isReload;       //换弹输入
    public bool isSquat;        //蹲下输入
    public bool isShoulderDown;   //肩射输入
    public bool isAdsDown;        //开镜输入


    // 取缺失的组件引用
    void Awake()
    {
        if (body == null) body = GetComponent<Player_Body>();
        if (input == null) input = GetComponent<Player_Input>();
    }

    // 初始化组件与输入
    void Start()
    {
        netObj = GetComponent<NetworkObject>();

        UIFocus = Player_Main.UI_RectTransform;

    
        if (Player_camera == null)
        {
            Debug.LogError("Player_camera==null");
        }
        if (UIFocus == null)
        {
            Debug.LogError("UIFocus==null");
        }
        if (rigidbody == null)
        {
            Debug.LogError("rigidbody==null");
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

        if (Aim_Ring_UI.Instance != null)
            Aim_Ring_UI.Instance.Bind(gun_Control, UIFocus, Player_camera);
    }

    // 注册输入事件
    void RegisterInputEvents()
    {
        if (input == null) return;

        input.Move_event += OnMove;                 //移动
        input.JumpDown_event += OnJumpDown;         //跳跃
        input.Mouse1_event += OnMouse1;             //左键
        input.Mouse2_event += OnMouse2;             //右键
        input.MouseHeld_event += OnMouseHeld;       //鼠标按住
        input.Run_event += OnRun;                   //奔跑
        input.ReloadHeld_event += OnReloadHeld;     //换弹
        input.Squat_event += OnSquat;               //蹲下
        input.Shoulder_event += OnShoulder;         //肩射
        input.Ads_event += OnAds;                   //开镜
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
        input.Shoulder_event -= OnShoulder;
        input.Ads_event -= OnAds;
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
            if (!isAimDown) body.Body_rotation();
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
        if (gun_Control == null) return;

        if (isMouse1Down)
        {
            if (gun_Control != null && gun_Control.Shoot())
            {
                if (camShake != null) camShake.Shake();
            }
        }
        else
        {

        }


    }

    // 右键只朝向
    void OnMouse2(bool on)
    {
        isMouse2Down = on;
    }

    // 举枪时人物朝准星
    void OnMouseHeld(bool on)
    {
        if (on)
        {
            body.Body_calculateVectorCamera();
            body.Body_rotationWithFocus();
        }
    }

    // 肩射
    void OnShoulder(bool on)
    {
        isShoulderDown = on;
        if (camShake != null) camShake.SetShoulderAim(on);
        if (gun_Control == null) return;

        if (on) gun_Control.AimAt();
        else if (!isAdsDown) gun_Control.AimDown();
    }

    // 开镜
    void OnAds(bool on)
    {
        isAdsDown = on;
        if (gun_Control == null) return;

        if (on) gun_Control.AimAt();
        else if (!isShoulderDown) gun_Control.AimDown();
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
        if (Player_camera == null) return;               // 等相机绑定后再控制

        // 落地检测
        isOnGround = IsGrounded();

        if (input == null || body == null || gun_Control == null) return;

        // 计算移动数据
        body.Player_Body_Update(input.MoveAxis, isRuning, isSquat);

        // 动画更新
        player_Animation.Player_animation_Update(this);

        // 传递枪械状态
        gun_Control.SetRecoilReduction(isShoulderDown);
        gun_Control.SetAimState(isShoulderDown, isAdsDown);
        gun_Control.SetMoveState(isSquat, isWASDDowm, isRuning);
        UpdateAimTarget();

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
        if (col == null) return false;

        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, rayDistance);
    }


    // 绑定本地相机
    public void SetupLocal(Camera cam, Player_camera camRig, RectTransform ui)
    {
        Player_camera = cam;
        camShake = camRig;
        if (camRig != null) camRig.Player = Head;   // 相机跟随头部

        if (Aim_Ring_UI.Instance != null)
            Aim_Ring_UI.Instance.Bind(gun_Control, UIFocus, cam);
    }

    // 找可见目标
    void UpdateAimTarget()
    {
        if (UIFocus == null) return;

        Vector3 origin = gun_Control.MuzzlePosition;
        Ray ray = Camera.main.ScreenPointToRay(UIFocus.position);
        Vector3 fwd = ray.direction;

        // 准星射线上最近的命中
        RaycastHit[] hits = Physics.RaycastAll(ray, gun_Control.range);

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
            gun_Control.SetTarget(true, closest.point);
            return;
        }

        // 临时调试，测完删掉
        if (Time.frameCount % 30 == 0)
        {
            string hitInfo = nearest < float.MaxValue
                ? $"{closest.collider.name} 层{closest.collider.gameObject.layer} 距离{nearest:F1}"
                : "无";
            Debug.Log($"[调试] 准星最近命中 {hitInfo}");
        }

        // 其次取外圈内可见的敌人最近点
        float outer = gun_Control.OuterAngle * 0.5f;   //外圈按直径算

        Collider[] cols = Physics.OverlapSphere(origin, gun_Control.range, enemyMask);

        float bestAngle = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(transform)) continue;

            // 准星射线上敌人所在深度处的点
            float depth = Vector3.Dot(col.bounds.center - ray.origin, fwd);
            if (depth <= 0f) continue;

            // 敌人表面离准星最近的点
            Vector3 point = col.ClosestPoint(ray.origin + fwd * depth);

            float angle = Vector3.Angle(fwd, point - ray.origin);
            if (angle > outer || angle >= bestAngle) continue;
            if (!Visible(origin, point, col)) continue;

            bestAngle = angle;
            bestPoint = point;
        }

        // 临时调试，测完删掉
        if (Time.frameCount % 30 == 0)
            Debug.Log($"[调试] 兜底 锁定={bestAngle < float.MaxValue} 角度={(bestAngle < float.MaxValue ? bestAngle.ToString("F1") : "-")} 外圈={outer:F1}");

        gun_Control.SetTarget(bestAngle < float.MaxValue, bestPoint);
    }

    // 判断目标点是否被挡
    bool Visible(Vector3 origin, Vector3 point, Collider target)
        => !Physics.Linecast(origin, point, out RaycastHit h) || h.collider == target;
}
