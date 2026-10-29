using System;
using UnityEngine;

// 玩家体力数据与恢复
[Serializable]
public class Player_Stamina_Date
{
    public float maxStamina = 100f;             //体力上限
    public float stamina = 100f;                //当前体力
    public float jumpStaminaCost = 10f;         //跳跃消耗
    public float slideStaminaCost = 40f;        //滑铲消耗
    public float rollStaminaCost = 30f;         //翻滚消耗
    public float recoverRate = 30f;             //每秒恢复
    public float runRecoverRate = 10f;          //跑步每秒恢复
    public float recoverDelay = 0.3f;          //恢复延迟秒

    float recoverTimer;                         //恢复延迟剩余

    // 扣除体力，cost为消耗量
    public bool Try_Spend(float cost)
    {
        if (stamina < cost) return false;

        stamina = Mathf.Max(stamina - cost, 0f);
        recoverTimer = recoverDelay;
        return true;
    }

    // 推进体力恢复，action为动作占用
    public void Recover_Date(float deltaTime, bool action,
        bool grounded, bool running)
    {
        if (deltaTime <= 0f) return;

        if (action)
        {
            recoverTimer = recoverDelay;
            return;
        }

        if (recoverTimer > 0f)
        {
            recoverTimer -= deltaTime;
            if (recoverTimer <= 0f) recoverTimer = 0f;
            return;
        }

        if (!grounded) return;

        float rate = running ? runRecoverRate : recoverRate;
        stamina = Mathf.MoveTowards(stamina, maxStamina,
            rate * deltaTime);
    }

    // 客机校正体力，action为动作占用
    public void Sync_Date(float authoritative, bool action)
    {
        if (action) return;
        if (Mathf.Abs(authoritative - stamina) <= 3f) return;

        stamina = Mathf.Clamp(authoritative, 0f, maxStamina);
    }

    // 复活恢复体力
    public void Reset_Date()
    {
        stamina = maxStamina;
        recoverTimer = 0f;
    }
}
