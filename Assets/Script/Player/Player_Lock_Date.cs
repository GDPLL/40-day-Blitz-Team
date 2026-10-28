using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// 玩家锁定机制，目标搜索与锁定提示
public class Player_Lock_Date : NetworkBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    // 锁定数据
    Player_Control lockTarget;      //本帧锁定目标
    Player_Control lockSentTarget;  //已通知目标
    bool lockDone;                  //已发完成提示
    readonly HashSet<ulong> lockSources = new HashSet<ulong>();      //锁定我的来源
    readonly HashSet<ulong> lockDoneSources = new HashSet<ulong>();  //完成锁定的来源
    int lockSentStage;              //已下发阶段
    readonly RaycastHit[] hitBuffer = new RaycastHit[16];   //遮挡检测缓存
    public ulong Current_Target_Id =>
        lockTarget != null ? lockTarget.OwnerClientId : ulong.MaxValue;

    // 主机每帧搜索目标，结果由Control推给枪械
    public void Target_Date(Vector3 origin, Vector3 fwd, Vector3 aimPoint, float range, float outerAngle,
        out bool hasTarget, out Vector3 targetPos)
    {
        hasTarget = false;
        targetPos = Vector3.zero;
        SetLockTarget(false, null);

        fwd = fwd.normalized;

        LayerMask enemyMask = Own_Control.enemyMask;
        Transform self = Own_Control.transform;

        // 准星落点在敌人身上锁落点
        Collider[] atAim = Physics.OverlapSphere(aimPoint, 0.2f, enemyMask);

        foreach (Collider col in atAim)
        {
            if (col.transform.IsChildOf(self)) continue;

            Player_Control target = col.GetComponentInParent<Player_Control>();
            if (target == null) continue;

            hasTarget = true;
            targetPos = col.ClosestPoint(aimPoint);
            SetLockTarget(true, target);
            return;
        }

        Ray ray = new Ray(origin, fwd);

        // 准星射线上最近的命中
        RaycastHit[] hits = Physics.RaycastAll(ray, range);

        float nearest = float.MaxValue;
        RaycastHit closest = default;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(self)) continue;
            if (h.distance >= nearest) continue;

            nearest = h.distance;
            closest = h;
        }

        // 最近命中的是敌人则锁定命中点
        if (nearest < float.MaxValue &&
            (enemyMask.value & (1 << closest.collider.gameObject.layer)) != 0)
        {
            Player_Control target = closest.collider.GetComponentInParent<Player_Control>();
            if (target == null) return;

            hasTarget = true;
            targetPos = closest.point;
            SetLockTarget(true, target);
            return;
        }

        // 取外圈内可见敌人的最近点
        float outer = outerAngle * 0.5f;   //外圈按直径算

        Collider[] cols = Physics.OverlapSphere(origin, range, enemyMask);

        float bestAngle = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;
        Player_Control bestTarget = null;   //外圈内最近目标玩家

        foreach (Collider col in cols)
        {
            if (col.transform.IsChildOf(self)) continue;

            // 准星射线上敌人所在深度处的点
            float depth = Vector3.Dot(col.bounds.center - origin, fwd);
            if (depth <= 0f) continue;

            // 敌人表面离准星最近的点
            Vector3 point = col.ClosestPoint(origin + fwd * depth);

            float angle = Vector3.Angle(fwd, point - origin);
            if (angle > outer || angle >= bestAngle) continue;
            if (!Visible(origin, point, col)) continue;

            bestAngle = angle;
            bestPoint = point;
            bestTarget = col.GetComponentInParent<Player_Control>();
        }

        if (bestAngle >= float.MaxValue || bestTarget == null) return;   //外圈内无目标

        hasTarget = true;
        targetPos = bestPoint;
        SetLockTarget(true, bestTarget);
    }

    // 记录锁定目标，has是否锁定
    void SetLockTarget(bool has, Player_Control target)
    {
        if (has && target == null) Debug.LogError("Player_Lock_Date|SetLockTarget|锁定目标无玩家组件");

        lockTarget = has ? target : null;
    }

    // 汇总锁定目标下发提示
    public void Lock_On_Date(bool aimDown, float aimAngle, float innerAngle)
    {
        Player_Control now = aimDown ? lockTarget : null;   //举枪才算锁定

        // 目标变化先解除旧目标
        if (lockSentTarget != now)
        {
            Lock_On_Release();
            if (now == null) return;

            lockSentTarget = now;
            Source_Push(lockSentTarget, 1);
            return;
        }

        if (lockSentTarget == null) return;

        // 精度圈到内圈算完成
        if (!lockDone && aimAngle <= innerAngle)
        {
            lockDone = true;
            Source_Push(lockSentTarget, 2);
        }
    }

    // 解除本机已发的锁定
    public void Lock_On_Release()
    {
        Source_Push(lockSentTarget, 0);

        lockSentTarget = null;
        lockDone = false;
    }

    // 向目标推送锁定阶段
    void Source_Push(Player_Control target, int stage)
    {
        if (target == null) return;
        if (target.Con_lock == null) return;

        target.Con_lock.Lock_On_Source_Date(Own_Control.OwnerClientId, stage);
    }

    // 合并锁定来源阶段
    public void Lock_On_Source_Date(ulong shooterId, int stage)
    {
        if (!IsServer) return;   //主机处理

        if (stage == 1) lockSources.Add(shooterId);
        else if (stage == 2) lockDoneSources.Add(shooterId);
        else if (stage == 0)
        {
            lockSources.Remove(shooterId);
            lockDoneSources.Remove(shooterId);
        }
        else
        {
            Debug.LogError("Player_Lock_Date|Lock_On_Source_Date|阶段参数非法");
            return;
        }

        int stageNow = lockSources.Count == 0 ? 0 : (lockDoneSources.Count > 0 ? 2 : 1);

        if (stageNow == lockSentStage) return;   //阶段没变不下发

        lockSentStage = stageNow;
        Lock_On_Send(stageNow);
    }

    // 本机显示并下发提示
    void Lock_On_Send(int stage)
    {
        Player_Control c = Own_Control;
        if (c == null)
        {
            Debug.LogError("Player_Lock_Date|Lock_On_Send|玩家控制为空");
            return;
        }

        if (c.IsOwner)
            Lock_On_Local_Performance(stage, Time.time);

        ClientRpcParams ps = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { c.OwnerClientId } }
        };
        Lock_On_ClientRpc(stage, ps);
    }

    // 锁定提示通知受害者本机
    [ClientRpc]
    void Lock_On_ClientRpc(int stage, ClientRpcParams ps = default)
    {
        if (IsServer) return;   //主机已显示
        if (!IsOwner) return;   //只本机显示

        Lock_On_Local_Performance(stage, Time.time);
    }

    // 本机锁定表现，time为当前时刻
    void Lock_On_Local_Performance(int stage, float time)
    {
        Player_Control c = Own_Control;
        if (c == null)
        {
            Debug.LogError("Player_Lock_Date|Lock_On_Local_Performance|玩家控制为空");
            return;
        }

        if (c.Con_lockOn_UI == null)
            Debug.LogError("Player_Lock_Date|Lock_On_Local_Performance|锁定提示UI为空");
        else
            c.Con_lockOn_UI.Lock_On_Performance_Local(stage);

        if (c.Con_player_Audio == null)
            Debug.LogError("Player_Lock_Date|Lock_On_Local_Performance|玩家音效为空");
        else
            c.Con_player_Audio.Lock_Buzzer_Performance_Local(stage, time);
    }

    // 死亡清空锁定状态
    public void Lock_On_Reset()
    {
        Lock_On_Release();      //解除自己发出的锁定

        lockSources.Clear();
        lockDoneSources.Clear();

        if (lockSentStage == 0) return;

        lockSentStage = 0;
        Lock_On_Send(0);
    }

    // 判断目标点是否被挡
    bool Visible(Vector3 origin, Vector3 point, Collider target)
    {
        Vector3 dir = point - origin;
        if (dir.sqrMagnitude <= 0.001f) return true;

        int count = Physics.RaycastNonAlloc(origin, dir.normalized, hitBuffer, dir.magnitude);

        for (int i = 0; i < count; i++)
        {
            if (hitBuffer[i].collider == target) continue;
            if (hitBuffer[i].collider.transform.IsChildOf(Own_Control.transform)) continue;   // 忽略自身

            return false;
        }

        return true;
    }
}
