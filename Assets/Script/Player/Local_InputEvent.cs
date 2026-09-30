using System;
using UnityEngine;

// 本机表现层输入事件分发，只有本地玩家运行
public class Local_InputEvent : MonoBehaviour
{
    Input_Manage input_Manage;      // 全局输入引用
    bool lastFireHeld;              // 上次左键状态

    // 表现层事件
    public event Action<bool> ShoulderAim_event;    // 肩射开关
    public event Action<bool> FireHeld_event;       // 左键按住

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

    // 每帧按本机输入分发表现事件
    void Update()
    {
        if (input_Manage == null) return;

        ShoulderAim_event?.Invoke(input_Manage.Mouse2Held);     //肩射跟随右键

        if (input_Manage.Mouse1Held != lastFireHeld) FireHeld_event?.Invoke(input_Manage.Mouse1Held);   //左键按下
        lastFireHeld = input_Manage.Mouse1Held;
    }
}
