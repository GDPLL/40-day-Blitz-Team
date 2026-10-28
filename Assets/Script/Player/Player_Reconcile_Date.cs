using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

// 客机位置预测纠偏，权威位置回传
public class Player_Reconcile_Date : NetworkBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    // 纠偏参数
    public float radius = 0.5f;    //误差阈值,米
    public float snap = 5f;        //超过此偏差直接归位,米

    Vector3 authPos;    //主机权威位置
    int frame;          //回传计时

    // 客机本地预测初始化
    public void Reconcile_Init()
    {
        Player_Control c = Own_Control;
        if (c.IsServer) return;
        if (c.rigidbody == null) return;

        NetworkTransform netTrans = c.GetComponent<NetworkTransform>();
        if (netTrans != null) netTrans.enabled = false;

        c.rigidbody.isKinematic = false;    // 本地非运动学控制
    }

    // 主机每物理帧回传权威位置，10Hz
    public void Reconcile_Host_Fixed()
    {
        if (!IsServer) return;
        if (++frame < 5) return;

        frame = 0;

        ClientRpcParams ps = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { Own_Control.OwnerClientId } }
        };
        Player_ReconcileClientRpc(Own_Control.transform.position, ps);
    }

    // 客机本机每物理帧计算纠偏，只返回目标与是否瞬移，平滑由Move计算
    public bool Reconcile_ClientOwn_Date(Vector3 current, out Vector3 target, out bool hard)
    {
        target = authPos;
        hard = false;

        if (authPos == Vector3.zero) return false;    //还没收到过

        float distance = (authPos - current).magnitude;
        if (distance < radius) return false;          //误差在阈值内

        hard = distance > snap;                       //超过直接归位
        return true;
    }

    // 主机下发瞬移，复活与传送用
    public void Teleport_Push(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;      //只有主机下发

        ClientRpcParams ps = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { Own_Control.OwnerClientId } }
        };
        Player_TeleportClientRpc(position, rotation, ps);
    }

    // 客机同步瞬移，转为Move的瞬移申请
    [ClientRpc]
    void Player_TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams ps = default)
    {
        if (IsServer) return;
        if (Own_Control == null || Own_Control.Con_move == null) return;   //初始化未完成

        authPos = position;    //同步纠偏基准，防止被拉回

        Own_Control.Con_move.Teleport_Request(position, rotation, Player_Move_Date.Priority_Respawn);
    }

    // 回传权威位置
    [ClientRpc]
    void Player_ReconcileClientRpc(Vector3 position, ClientRpcParams ps = default)
    {
        if (IsServer) return;

        authPos = position;
    }
}
