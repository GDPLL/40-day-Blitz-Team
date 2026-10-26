using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
// 生命系统，血量与死亡事件
public class Object_System : MonoBehaviour , Idamage
{
    // 上层注入
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

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
    [Range(0f, 1f)]
    public float rollFallDamageScale = 0.5f;    //翻滚摔落伤害倍率
    float fallSpeed;                        //空中最大下落速度
    float physicsDelta = 0.02f;             //物理帧间隔，算致死速度用

    // 死亡事件，血量归零触发
    public event Action HealthEnd;

    // 受击事件，携带攻击者位置
    public event Action<Vector3> Damage_event;

    // 死亡与复活
    public int respawnIndex;        //复活点编号
    public bool isDead;             //等待复活中
    int deathCount;                 //死亡次数
    float respawnDelay;             //本次等待秒数
    float respawnTimer;             //等待计时
    float gasTimer;                 //毒气扣血计时

    // 扣血并返回实际伤害与击杀
    public int Takedamage(int hit, ulong killerId, Vector3 fromPos, out bool killed)
    {
        int before = HP;
        HP = Mathf.Clamp(HP - hit, 0, MaxHp);
        KillerId = killerId;
        healTimer = 0f;     // 受击打断回血
        killed = before > 0 && HP <= 0;     // 已死亡不算击杀

        // 编辑器下打印受击
#if UNITY_EDITOR
        Debug.Log($"收到伤害{this.gameObject.name}");
#endif

        Damage_event?.Invoke(fromPos);
        return before - HP;     //实际扣血,残血不溢出
    }

    // 重置血量
    public void ResetHealth()
    {
        HP = MaxHp;
        fallSpeed = 0f;     //复活一并清坠落基准
    }

    // 清除坠落基准，瞬移后调用
    public void Fall_Speed_Clear()
    {
        fallSpeed = 0f;
    }

    // 坠落伤害，velocityY为当前垂直速度，grounded为是否在地面
    public void Fall_Damage_Date(float velocityY, bool grounded, float deltaTime, bool rolling)
    {
        if (deltaTime <= 0f) return;    //防除零

        physicsDelta = deltaTime;

        // 空中只记录最大下落速度
        if (!grounded)
        {
            if (-velocityY > fallSpeed) fallSpeed = -velocityY;
            return;
        }

        // 落地按空中峰值结算一次，换算成加速度
        float accel = fallSpeed / deltaTime;
        float damageScale = rolling ? rollFallDamageScale : 1f;
        int damage = (int)((accel - fallAccel) * fallDamageRate * damageScale);
        if (damage > 0) Takedamage(damage, 999, new Vector3(), out _);

        fallSpeed = 0f;
    }

    // 致死坠落速度，满血一次摔死的下落速度
    public float Fatal_Fall_Speed => (MaxHp / fallDamageRate + fallAccel) * physicsDelta;

    // 失重判断，speed为速度大小，不在空中不算
    public bool WeightLess_Date(float speed, bool grounded) =>
        !grounded && speed >= Fatal_Fall_Speed;

    // 生命系统更新，主机每帧驱动
    public void Object_System_Update()
    {
        // 血量归零触发死亡
        if (HP <= 0)
        {
            HealthEnd?.Invoke();
            Death_Event();
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

    // 死亡事件，主机开始等待复活
    public void Death_Event()
    {
        Player_Control c = Own_Control;
        if (c == null) return;
        if (!c.IsServer) return;    //主机处理
        if (isDead) return;         //已在等待

        if (c.Con_lock != null) c.Con_lock.Lock_On_Reset();   //死亡清空锁定提示
        if (c.Con_net_State.Is_Out) return;                   //已淘汰

        if (Player_Main.player_Main == null)
        {
            Debug.LogError("Object_System|Death_Date|Player_Main 为空");
            return;
        }

        // 击杀者死亡计数归零
        Player_Main.player_Main.Player_Kill(KillerId);

        // 等待时长随死亡次数翻倍，上限8秒
        deathCount++;
        respawnDelay = deathCount <= 1 ? 0f : Mathf.Min(Mathf.Pow(2f, deathCount - 1), 8f);
        respawnTimer = 0f;
        isDead = true;
        c.Con_net_State.Respawn_Wait_Push(respawnDelay);

        // 各端提示阵亡
        Player_Main.player_Main.Player_Death_Date(c);
    }

    // 击杀敌人，清空死亡计数
    public void Death_Reset()
    {
        deathCount = 0;
    }

    // 主机每帧死亡与毒气驱动，返回true表示跳过后续逻辑
    public bool Objcet_System_Update()
    {
        Player_Control c = Own_Control;
        if (c == null) return false;

        // 死亡等待复活，9分钟后不再复活
        if (isDead)
        {
            if (Player_level.Instance != null && Player_level.Instance.Respawn_Off)
            {
                Out_Date();     //9分钟后死亡即淘汰
                return true;
            }

            respawnTimer += Time.deltaTime;
            c.Con_net_State.Respawn_Wait_Push(Mathf.Max(respawnDelay - respawnTimer, 0f));
            if (respawnTimer < respawnDelay) return true;

            isDead = false;
            c.Con_net_State.Respawn_Wait_Push(0f);

            if (Player_Main.player_Main == null)
            {
                Debug.LogError("Object_System|Host_Date|Player_Main 为空");
                return true;
            }

            Player_Main.player_Main.Player_Respawn(c);
            ResetHealth();

            return true;
        }

        if (c.Con_net_State.Is_Out) return true;   //已淘汰不再参与

        // 毒气，低于毒气高度每秒扣血
        if (Player_level.Instance != null && Player_level.Instance.Gas_Contains(c.transform.position))
        {
            gasTimer += Time.deltaTime;
            if (gasTimer >= 1f)
            {
                gasTimer = 0f;
                Takedamage(Player_level.Instance.Gas_Damage, 999, new Vector3(), out _);
            }
        }
        else gasTimer = 0f;

        return false;
    }

    // 淘汰，9分钟后死亡不再复活
    void Out_Date()
    {
        Player_Control c = Own_Control;
        if (c.Con_net_State.Is_Out) return;   //已淘汰

        if (Player_Main.player_Main == null)
        {
            Debug.LogError("Object_System|Out_Date|Player_Main 为空");
            return;
        }

        isDead = false;
        c.Con_net_State.Respawn_Wait_Push(0f);
        c.Con_net_State.Out_Push();

        Player_Main.player_Main.Player_Out_Date(c);   //各端提示并判定胜负
    }

}

