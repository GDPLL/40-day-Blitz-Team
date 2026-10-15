using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

// 游戏时间与事件提示UI，由 Player_Main 驱动
public class UI_Time : MonoBehaviour
{
    public TextMeshProUGUI timeText;     // 游戏时间文本
    public TextMeshProUGUI eventText;    // 事件文本
    public float showTime = 5f;          // 事件文本显示秒数

    // 事件时刻
    readonly float[] eventTime = { 30f, 180f, 190f, 240f, 300f, 330f, 540f };
    // 事件名称
    readonly string[] eventName = { "电梯上行", "一楼毒气", "索道开放", "二层毒气", "三层毒气", "毒气蔓延", "关闭复活" };

    // 事件前提前提示的秒数
    readonly float[] tipAhead = { 30f, 10f };

    float eventEnd;     // 事件文本结束时刻
    int tipIndex;       // 提示进度
    int eventIndex;     // 事件进度

    // 初始化
    public void UI_Time_Init()
    {
        if (timeText == null || eventText == null)
        {
            Debug.LogError("UI_Time|UI_Time_Init|文本未绑定");
            return;
        }

        timeText.text = string.Empty;
        eventText.text = string.Empty;

        tipIndex = 0;
        eventIndex = 0;
    }

    // 显示游戏时间，gameTime为关卡时间
    public void UI_Time_Show_Time(float gameTime)
    {
        if (timeText == null || eventText == null) return;

        // 分:秒
        timeText.text = $"{(int)gameTime / 60:00}:{(int)gameTime % 60:00}";

        // 提前提示，各提前量只提一次
        if (eventIndex < eventTime.Length)
        {
            while (tipIndex < tipAhead.Length && gameTime >= eventTime[eventIndex] - tipAhead[tipIndex])
            {
                float ahead = tipAhead[tipIndex];
                tipIndex++;

                if (eventTime[eventIndex] - ahead < 0f) continue;   // 关卡开始前的不提
                UI_Time_Show_Event($"{eventName[eventIndex]} 剩余{ahead:0}秒", gameTime);
                break;
            }
        }

        // 到点显示事件
        if (eventIndex < eventTime.Length && gameTime >= eventTime[eventIndex])
        {
            UI_Time_Show_Event(eventName[eventIndex], gameTime);
            eventIndex++;
            tipIndex = 0;
        }

        // 显示时间到就清空
        if (eventText.text.Length > 0 && gameTime >= eventEnd) eventText.text = string.Empty;
    }

    // 显示事件文本，text为内容
    public void UI_Time_Show_Event(string text, float gameTime)
    {
        if (eventText == null)
        {
            Debug.LogError("UI_Time|UI_Time_Show_Event|eventText 未绑定");
            return;
        }

        eventText.text = text;
        eventEnd = gameTime + showTime;     // 显示持续时间
    }
}
