using System;
using UnityEngine;
using Unity.Netcode;

// 主机侧输入分发，一个玩家一份
public class HostNetWorkInputEvent : NetworkBehaviour
{
    // 输入事件
    public event Action<Vector2> Move_event;     // 移动
    public event Action<bool> JumpDown_event;    // 跳跃
    public event Action<bool> Mouse1_event;      // 左键
    public event Action<bool> Mouse2_event;      // 右键
    public event Action<bool> MouseHeld_event;   // 鼠标按住
    public event Action<bool> Shoulder_event;    // 肩射
    public event Action<bool> Ads_event;         // 开镜
    public event Action<bool> Run_event;         // 奔跑
    public event Action ReloadHeld_event;        // 换弹
    public event Action<bool> Squat_event;       // 蹲下

    // 输入缓存
    public InputPacket Packet => packet;        // 最新输入包
    InputPacket packet;                         // 最近一帧输入
    bool hasPacket;                             // 是否收到过输入

    // 注册主机输入事件
    public void Host_Input_Init(Input_Manage inputManage)
    {
        if (inputManage == null)
        {
            Debug.LogError("HostNetWorkInputEvent|Host_Input_Init|Input_Manage 为空");
            return;
        }

        inputManage.Input_event += Input_Resolve;

        Debug.Log("HostNetWorkInputEvent|Host_Input_Init|完成初始化");
    }

    // 取消注册
    public override void OnDestroy()
    {
        base.OnDestroy();

        if (Input_Manage.Instance != null) Input_Manage.Instance.Input_event -= Input_Resolve;
    }

    // 解析客户端输入包
    public void Input_Resolve(InputPacket p)
    {
        if (p.clientId != OwnerClientId) return;   // 只收自己的

        packet = p;
        hasPacket = true;
    }

    // 每帧分发输入
    void Update()
    {
        if (!IsServer || !hasPacket) return;

        Move_event?.Invoke(packet.move);
        JumpDown_event?.Invoke(packet.jump);
        Mouse1_event?.Invoke(packet.mouse1);
        Mouse2_event?.Invoke(packet.mouse2);
        MouseHeld_event?.Invoke(packet.mouseHeld);
        Shoulder_event?.Invoke(packet.shoulder);
        Ads_event?.Invoke(packet.ads);
        Run_event?.Invoke(packet.run);
        if (packet.reload) ReloadHeld_event?.Invoke();
        Squat_event?.Invoke(packet.squat);
    }
}
