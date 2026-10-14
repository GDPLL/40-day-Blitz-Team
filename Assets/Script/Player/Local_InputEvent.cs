using System;
using UnityEngine;

// 本机预测与表现事件分发，只有本地玩家运行
public class Local_InputEvent : MonoBehaviour
{
    Input_Manage input_Manage;      // 全局输入引用

    // 表现层事件
    public event Action<bool, bool> AimPose_event;  // 肩射与开镜开关

    // 本机输入事件，与 HostNetWorkInputEvent 同名
    public event Action<Vector2> Move_event;        // 移动
    public event Action<bool> JumpDown_event;       // 跳跃
    public event Action<bool> Mouse1_event;         // 左键
    public event Action<bool> Mouse2_event;         // 右键
    public event Action<bool> MouseHeld_event;      // 鼠标按住
    public event Action<bool> Shoulder_event;       // 肩射
    public event Action<bool> Ads_event;            // 开镜
    public event Action<bool> Run_event;            // 奔跑
    public event Action ReloadHeld_event;           // 换弹
    public event Action<bool> Squat_event;          // 蹲下

    // 注入全局输入
    public void Local_Input_Init(Input_Manage inputManage)
    {
        if (inputManage == null)
        {
            Debug.LogError("Local_InputEvent|Local_Input_Init|Input_Manage 为空");
            return;
        }

        input_Manage = inputManage;

        Debug.Log("Local_InputEvent|Local_Input_Init|完成初始化");
    }

    // 每帧按本机输入分发事件
    void Update()
    {
        if (input_Manage == null) return;

        AimPose_event?.Invoke(input_Manage.ShoulderHeld, input_Manage.AdsHeld);   //Z肩射 X开镜

        // 预测事件，顺序与 HostNetWorkInputEvent 一致
        Move_event?.Invoke(input_Manage.MoveAxis);
        JumpDown_event?.Invoke(input_Manage.JumpDownHeld);
        Mouse1_event?.Invoke(input_Manage.Mouse1Held);
        Mouse2_event?.Invoke(input_Manage.Mouse2Held);
        MouseHeld_event?.Invoke(input_Manage.MouseHeld);
        Shoulder_event?.Invoke(input_Manage.ShoulderHeld);
        Ads_event?.Invoke(input_Manage.AdsHeld);
        Run_event?.Invoke(input_Manage.RunHeld);
        if (input_Manage.ReloadHeld) ReloadHeld_event?.Invoke();
        Squat_event?.Invoke(input_Manage.SquatHeld);
    }
}
