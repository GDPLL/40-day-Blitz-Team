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

    //坠落伤害
    public float fallAccel = 400f;          //触发加速度,米每二次方秒
    public float fallDamageRate = 0.05f;    //超出部分的伤害比例
    float lastVelocityY;                    //上一物理帧垂直速度

    // 死亡事件，血量归零触发
    public event Action HealthEnd;

    // 受击事件，携带攻击者位置
    public event Action<Vector3> Damage_event;

    // 扣血
    public void Takedamage(int hit, ulong killerId, Vector3 fromPos)
    {
        HP = Mathf.Clamp(HP - hit, 0, MaxHp);
        KillerId = killerId;
        healTimer = 0f;     // 受击打断回血

        // 编辑器下打印受击
#if UNITY_EDITOR
        Debug.Log($"收到伤害{this.gameObject.name}");
#endif

        Damage_event?.Invoke(fromPos);
    }

    // 重置血量
    public void ResetHealth()
    {
        HP = MaxHp;
        lastVelocityY = 0f;     //复活一并清坠落基准
    }

    // 坠落伤害，velocityY为当前垂直速度，deltaTime为物理帧间隔
    public void Fall_Damage_Date(float velocityY, float deltaTime)
    {
        if (deltaTime <= 0f) return;    //防除零

        float accel = (velocityY - lastVelocityY) / deltaTime;

        // 下落被地面刹住才算，上升与起跳不算
        if (lastVelocityY < 0f && velocityY <= 0f && accel > fallAccel)
        {
            int damage = (int)((accel - fallAccel) * fallDamageRate);
            if (damage > 0) Takedamage(damage, 999, new Vector3());
        }

        lastVelocityY = velocityY;
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

