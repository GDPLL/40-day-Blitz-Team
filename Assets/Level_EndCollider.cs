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

    public float End_time = 30f;
    public bool Is_End;

    public Player_Control Winner => in_player;   // 撤离成功的玩家
    public void Level_EndCollider_Init()
    {
        Is_End =false;
        timer = 0;
        tipTimer = 0;
        in_player = null;
    }
    public void Scene_Interaction(Player_Control player_Control)
    {
        if (Is_End) return;
        if (in_player == null || in_player != player_Control) //切换玩家
        {
            in_player = player_Control;
            timer = 0;
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

        if (hits.Length > 0 && in_player!=null)
        {
            bool isplayer = false;
            for(int i=0;i<hits.Length;i++)
            {
                if(hits[i].GetComponent<Player_Control>())
                {
                    isplayer =true;
                    break;
                }
            }
            if(isplayer)
            {
                timer += Time.fixedDeltaTime;

                // 每10秒播报撤离玩家与剩余时间
                tipTimer += Time.fixedDeltaTime;
                if (tipTimer >= tipInterval)
                {
                    tipTimer = 0f;
                    if (!Is_End) Evac_Tip_event?.Invoke(in_player.Player_Number, End_time - timer);
                }
            }
        }
        else //玩家离开
        {
            timer = 0;
            tipTimer = 0;
            return;
        }
        if (timer >= End_time)
        {
            Is_End = true;
        }
    }
}
