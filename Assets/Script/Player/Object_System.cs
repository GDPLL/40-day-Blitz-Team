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
    public int healCap = 75;        // 回血上限
    public float healDelay = 6f;    // 无伤回血等待
    public float healSpeed = 10f;   // 每秒回血
    public ulong KillerId { get; private set; }   // 最后击杀者

    float healTimer;    // 无伤计时
    float healValue;    // 回血累积

    // 死亡事件，血量归零触发
    public event Action HealthEnd;

    // 扣血
    public void Takedamage(int hit, ulong killerId)
    {
        HP = Mathf.Clamp(HP - hit, 0, MaxHp);
        KillerId = killerId;
        healTimer = 0f;     // 受击打断回血
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
            return;
        }

        // 无伤6秒后呼吸回血，止于回血上限
        healTimer += Time.deltaTime;
        if (healTimer < healDelay || HP >= healCap) return;

        healValue += healSpeed * Time.deltaTime;
        int add = (int)healValue;
        if (add <= 0) return;

        healValue -= add;
        HP = Mathf.Min(healCap, HP + add);
    }

}

