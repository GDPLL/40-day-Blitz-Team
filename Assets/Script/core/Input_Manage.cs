using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// 负责全局输入系统，定时发送输入包 
// 负责本地事件分发
public class Input_Manage : NetworkBehaviour
{
    public static Input_Manage Instance { get; private set; }   // 全局实例

    [Header("网络输入")]
    public float sendRate = 60f;    // 发送频率
    float sendTimer;                // 发送计时

    [Header("举枪瞄准")]
    public float focusDistance = 10f;   // 未命中时的瞄点距离
    public Vector3 AimPoint;            // 当前瞄准落点

    // 输入事件
    public event Action<InputPacket> Input_event;   // 收到输入包
    public event Action<bool> Debug_event;          // 调试面板

    // 建立全局单例
    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Input_Manage|Awake|已存在实例，销毁重复对象");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("Input_Manage|Awake|完成初始化");
    }

    // 每帧读取输入并上传
    void Update()
    {
        if (Player_Main.player_Main == null || !Player_Main.player_Main.isInit) return;  // 等待关卡初始化完成
        Input_get();

        DebugE(DebugKey);
        SendPacket();
    }

    // 固定时段上传输入
    void SendPacket()
    {
        if (!IsSpawned) return;                     // 未接入网络不发
        

        sendTimer += Time.deltaTime;
        if (sendTimer < 1f / sendRate) return;

        sendTimer = 0f;
        SubmitInputServerRpc(GetPacket());
    }

    // 服务器接收输入包，跳过所有权校验
    [ServerRpc(RequireOwnership = false)]
    void SubmitInputServerRpc(InputPacket packet)
    {
        Input_event?.Invoke(packet);
    }

    // 打包当前输入
    public InputPacket GetPacket()
    {
        InputPacket packet = new InputPacket();
        packet.clientId = NetworkManager.Singleton.LocalClientId;

        Camera cam = Player_Main.player_Main != null ? Player_Main.player_Main.oCamera : null;
        if (cam != null)
        {
            packet.viewDir = cam.transform.forward;    //本机视角朝向
            packet.viewPos = cam.transform.position;   //本机相机位置
        }

        packet.aimPoint = AimPoint;     //举枪瞄准落点

        packet.move = MoveAxis;
        packet.jump = JumpDownHeld;
        packet.mouse1 = Mouse1Held;
        packet.mouse2 = Mouse2Held;
        packet.mouseHeld = MouseHeld;
        packet.shoulder = ShoulderHeld;
        packet.ads = AdsHeld;
        packet.run = RunHeld;
        packet.reload = ReloadHeld;
        packet.squat = SquatHeld;
        return packet;
    }

    // 准星射线取举枪瞄准落点
    void GetAimPoint()
    {
        Camera cam = Player_Main.player_Main != null ? Player_Main.player_Main.oCamera : null;
        RectTransform focus = Player_Main.player_Main != null ? Player_Main.player_Main.oUI_RectTransform : null;
        if (cam == null || focus == null) return;      // 引用缺失沿用上次

        Ray ray = cam.ScreenPointToRay(focus.position);
        if (TryGetAimPoint(ray, out Vector3 hit)) AimPoint = hit;      // 命中碰撞落点
        else AimPoint = ray.GetPoint(focusDistance);                   // 未命中取固定焦点距离
    }

    // 取射线命中点
    bool TryGetAimPoint(Ray ray, out Vector3 point)
    {
        point = Vector3.zero;

        Transform self = Player_Main.player_Main != null && Player_Main.player_Main.oPlayer_Control != null
            ? Player_Main.player_Main.oPlayer_Control.transform
            : null;

        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player")) continue;   // 跳过玩家
            if (self != null && hit.collider.transform.IsChildOf(self)) continue;   // 跳过自己

            point = hit.point;
            return true;
        }
        return false;
    }

    // 调试分发
    public void DebugE(bool on)
    {
        Debug_event?.Invoke(on);
    }

    //输入检测状态
    public Vector2 MoveAxis;        //移动输入轴
    public bool JumpDownHeld;       //跳跃空格
    public bool Mouse1Held;         //左键输入
    public bool Mouse2Held;         //右键输入
    public bool MouseHeld => Mouse1Held || Mouse2Held;
    public bool WASDHeld;           //移动输入
    public bool RunHeld;            //奔跑输入
    public bool ReloadHeld;         //换弹输入
    public bool SquatHeld;          //蹲下输入
    public bool ShoulderHeld;       //肩射输入
    public bool AdsHeld;            //开镜输入
    public bool DebugKey;           // 调试输入

    // 读取输入状态
    void Input_get()
    {
        MoveAxis = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        JumpDownHeld = Input.GetKey(KeyCode.Space);     //跳跃空格
        Mouse1Held = Input.GetMouseButton(0);        //左键输入
        Mouse2Held = Input.GetMouseButton(1);        //右键输入
        ShoulderHeld = Input.GetKey(KeyCode.Z);      //肩射输入
        AdsHeld = Input.GetKey(KeyCode.X);           //开镜输入
        WASDHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);        //移动输入
        RunHeld = Input.GetKey(KeyCode.LeftShift) && !ShoulderHeld && !AdsHeld;       //奔跑输入
        ReloadHeld = Input.GetKey(KeyCode.R);            //换弹输入 
        SquatHeld = Input.GetKey(KeyCode.LeftControl);       //蹲下输入
        DebugKey = Input.GetKey(KeyCode.BackQuote);         //调试输入按钮

        GetAimPoint();      // 瞄准落点
    }



}



