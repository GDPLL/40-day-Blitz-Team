using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Unity.Netcode;


public class Player_Control : Character_Move
{
    NetworkObject netObj;   // 联网对象（用于判断是否本地玩家）

    public Transform Head;
    public Camera Player_camera;

    [Header("落地检测")]
    public float groundCheckDistance = 0.2f;  // 落地检测距离

    public Gun_Control gun_Control;

    public Player_camera camShake;   // 相机震动脚本（开火时触发）

    public Player_Input input;  
    //状态
    public bool isOnGround;  //在地面
    public bool isJumpDown;    //跳跃空格
    public bool isMouse1Down;   //左键输入
    public bool isMouse2Down;   //右键输入
    public bool isMouseDown => isMouse1Down || isMouse2Down;
    public bool isWASDDowm;    //移动输入
    public bool isRuning;      //奔跑输入
    public bool isReload;       //换弹输入
    public bool isSquat;        //蹲下输入

    public Player_Body body;

    void Awake()
    {
        if (body == null) body = GetComponent<Player_Body>();
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

    }

    void Update()
    {
        if (netObj != null && !netObj.IsOwner) return;   // 非本地对象不更新
        if (Player_camera == null) return;               // 等关卡管理器绑定本地相机后再控制

         //鼠标输入方向计算
        float X, Y;
        ReadInput(out X, out Y);
        ForwardInputToBody(X, Y);

        //键盘输入状态检测
        isOnGround = IsGrounded();

        // 右键：切换肩射模式 + 后坐力减少 70%
        if (camShake != null) camShake.SetShoulderAim(isMouse2Down);
        if (gun_Control != null) gun_Control.SetRecoilReduction(isMouse2Down);
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
        if (netObj != null && !netObj.IsOwner) return;   // 非本地对象不更新
        if (Player_camera == null) return;   // 等关卡管理器初始化后再执行

        if (isReload)
        {
            gun_Control.Reload();
        }

        if (isMouse1Down && gun_Control.Shoot())    // 成功开火时触发相机震动
            camShake.Shake();

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
        body.SetupContext(cam, ui);
        Player_camera = cam;
        camShake = camRig;
        if (camRig != null) camRig.Player = Head;   // 第三人称相机跟随本地玩家头部
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
