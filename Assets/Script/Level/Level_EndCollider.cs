using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Level_EndCollider : MonoBehaviour, IScene_Interaction
{
    Player_Control in_player;
    float timer;

    float tipTimer;             //播报计时
    float tipInterval = 10f;    //播报间隔

    public event Action<int, float> Evac_Tip_event;   //撤离点播报，玩家编号与剩余秒数

    public event Action<int> Evac_Event_event;   //撤离突发，0开始 1剩20秒 2中断 3完成

    public float End_time = 30f;
    public bool Is_End;

    bool tip20;     //剩余20秒已播报

    public Player_Control Winner => in_player;   // 撤离成功的玩家
    public void Level_EndCollider_Init()
    {
        Is_End =false;
        timer = 0;
        tipTimer = 0;
        tip20 = false;
        in_player = null;
    }
    public void Scene_Interaction(Player_Control player_Control)
    {
        if (Is_End) return;
        if (in_player == null || in_player != player_Control) //切换玩家
        {
            in_player = player_Control;
            timer = 0;
            tip20 = false;
            Evac_Event_event?.Invoke(0);    //有人开始撤离
            return;
        }

    }
    public void Level_EndCollider_FixUpdate()
    {
        // 场景交互检测
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("Level_EndCollider|Level_EndCollider_FixUpdate|未找到 Collider");
            return;
        }

        // 获取玩家对象碰撞并调用对应方法
        Collider[] hits = Physics.OverlapBox(col.bounds.center, col.bounds.extents, Quaternion.identity);

        // 范围内是否有玩家
        bool isplayer = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponent<Player_Control>())
            {
                isplayer = true;
                break;
            }
        }

        if (isplayer && in_player != null)
        {
            timer += Time.fixedDeltaTime;

            // 剩余20秒提醒一次
            if (!tip20 && timer >= End_time - 20f)
            {
                tip20 = true;
                Evac_Event_event?.Invoke(1);
            }

            // 每10秒播报撤离玩家与剩余时间
            tipTimer += Time.fixedDeltaTime;
            if (tipTimer >= tipInterval)
            {
                tipTimer = 0f;
                if (!Is_End) Evac_Tip_event?.Invoke(in_player.Player_Number, End_time - timer);
            }
        }
        else //玩家离开
        {
            // 撤离中断只报一次，完成离开不算
            if (!Is_End && in_player != null)
            {
                in_player = null;
                Evac_Event_event?.Invoke(2);
            }

            timer = 0;
            tipTimer = 0;
            tip20 = false;
            return;
        }
        if (!Is_End && timer >= End_time)
        {
            Is_End = true;
            Evac_Event_event?.Invoke(3);    //完成撤离
        }
    }
}
