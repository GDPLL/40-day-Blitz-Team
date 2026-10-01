using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
// 生命系统，血量与死亡事件
public class Object_System : MonoBehaviour , Idamage
{

    
    public int HP = 100;      // 当前血量，主机权威
    public int MaxHp = 100;   // 血量上限

    // 死亡事件，血量归零触发
    public event Action HealthEnd;

    // 扣血
    public void Takedamage(int hit)
    {
        HP = Mathf.Clamp(HP - hit, 0, MaxHp);
        Debug.Log($"收到伤害{this.gameObject.name}");

    }

    // 重置血量
    public void ResetHealth()
    {
        HP = MaxHp;
    }

    // 生命系统更新，主机每帧驱动
    public void Object_System_Update()
    {
        // 血量归零触发死亡
        if (HP <= 0)
        {
            HealthEnd?.Invoke();
        }

    }

}

