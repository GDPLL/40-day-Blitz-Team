using UnityEngine;
using Unity.Netcode;

// 玩家战斗结果，受击反馈与积分
public class Player_Combat_Date : NetworkBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    Object_System healthSystem; //生命系统

    // 注册受击事件
    public void Combat_Init(Object_System system)
    {
        healthSystem = system;
        if (system != null) system.Damage_event += Damage_Date;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (healthSystem != null) healthSystem.Damage_event -= Damage_Date;
    }

    // 主机受击，转发攻击者位置
    void Damage_Date(Vector3 fromPos)
    {
        if (!IsServer) return;   //主机处理

        Player_Control c = Own_Control;

        // 主机自己直接显示
        if (c.IsOwner && c.Con_hitFeedback_UI != null) c.Con_hitFeedback_UI.Damage_Dir_Performance_Local(fromPos);

        // 受击震动
        if (c.IsOwner && c.Con_player_camera != null) c.Con_player_camera.Camera_Hurt_Performance_Local();

        // 被射中提示，环境伤害位置为零不播
        if (c.IsOwner && c.Con_player_Audio != null && fromPos != Vector3.zero) c.Con_player_Audio.Hurt_Show();

        Damage_Dir_ClientRpc(fromPos);
    }

    // 客户端受击方向表现
    [ClientRpc]
    void Damage_Dir_ClientRpc(Vector3 fromPos)
    {
        if (IsServer) return;   //主机已显示
        if (!IsOwner) return;   //只本机显示

        Player_Control c = Own_Control;

        if (c.Con_hitFeedback_UI == null)
        {
            Debug.LogError("Player_Combat_Date|Damage_Dir_ClientRpc|命中反馈UI 为空");
            return;
        }

        c.Con_hitFeedback_UI.Damage_Dir_Performance_Local(fromPos);

        // 受击震动
        if (c.Con_player_camera != null) c.Con_player_camera.Camera_Hurt_Performance_Local();

        // 被射中提示，环境伤害位置为零不播
        if (c.Con_player_Audio != null && fromPos != Vector3.zero) c.Con_player_Audio.Hurt_Show();
    }

    // 累加积分，主机处理
    public void Score_Add(int add)
    {
        if (!IsServer) return;
        if (Own_Control == null || Own_Control.Con_net_State == null) return;

        Own_Control.Con_net_State.Score_Add(add);
    }

    // 击杀提示，主机下发
    public void Player_Kill_Date()
    {
        if (!IsServer) return;      //只有主机下发

        Player_Control c = Own_Control;

        // 主机自己直接播
        if (c.IsOwner && c.Con_player_Audio != null) c.Con_player_Audio.Kill_Show();

        ClientRpcParams ps = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { c.OwnerClientId } }
        };
        Player_KillClientRpc(ps);
    }

    // 击杀者提示
    [ClientRpc]
    void Player_KillClientRpc(ClientRpcParams ps = default)
    {
        if (IsServer) return;       //主机已播
        if (!IsOwner) return;       //只击杀者

        if (Own_Control.Con_player_Audio != null) Own_Control.Con_player_Audio.Kill_Show();
    }
}
