using System.Text;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using Unity.Sync.Relay.Model;
using Unity.Sync.Relay.Transport.Netcode;

// 游戏场景调试面板
public class UI_Debug : MonoBehaviour
{
    public TMP_Text output;   // 输出文本

    Player_Control player;   // 本机角色，由管理脚本注入

    RelayTransportNetcode relayTransport;   // 中继组件
    readonly StringBuilder builder = new StringBuilder(512);   // 文本缓存

    // 注入本机角色并订阅
    public void UI_Debug_Start(Player_Control control,Input_Manage input_Manage)
    {
        if (input_Manage == null)
        {
            Debug.LogError("UI_Debug|Start|未找到 Input_Manage");
            return;
        }
        input_Manage.Debug_event += OnDebug;

        player = control;

        Debug.Log("UI_Debug|UI_Debug_Start|完成初始化");
    }

    // 反订阅调试事件
    void OnDestroy()
    {
        if (Input_Manage.Instance != null) Input_Manage.Instance.Debug_event -= OnDebug;
    }

    // 按住显示，松开隐藏
    void OnDebug(bool on)
    {
        if (output == null)
        {
            Debug.LogError("UI_Debug|OnDebug|output 未绑定");
            enabled = false;
            return;
        }

        if (!on)
        {
            output.text = string.Empty;
            return;
        }

        BuildText();
        output.text = builder.ToString();
    }

    // 拼接连接与角色信息
    void BuildText()
    {
        builder.Clear();

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            Debug.LogError("UI_Debug|BuildText|未找到 NetworkManager");
            builder.AppendLine("未找到 NetworkManager");
            return;
        }

        if (relayTransport == null || relayTransport.gameObject != manager.gameObject)
        {
            relayTransport = manager.GetComponent<RelayTransportNetcode>();
        }

        builder.Append("本机身份: ").AppendLine(
            !manager.IsListening ? "未连接" :
            manager.IsHost ? "Host" :
            manager.IsServer ? "Server" : "Client");

        builder.Append("连接状态: ").AppendLine(
            !manager.IsListening ? "未启动" :
            manager.IsServer ? "已启动" :
            manager.IsConnectedClient ? "已连接" : "连接中");

        builder.Append("LocalClientId: ").AppendLine(manager.LocalClientId.ToString());
        builder.Append("已连接人数: ").AppendLine(
            manager.IsServer ? manager.ConnectedClients.Count.ToString() : "-");   //仅服务端可读

        if (relayTransport != null)
        {
            RelayRoom room = relayTransport.GetRoomInfo();
            RelayPlayer me = relayTransport.GetCurrentPlayer();

            if (room != null && !string.IsNullOrEmpty(room.ID))
            {
                builder.Append("房间名: ").AppendLine(room.Name);
                builder.Append("RoomUuid: ").AppendLine(room.ID);
                builder.Append("房间人数: ").Append(room.Players?.Count ?? 0).Append('/')
                       .AppendLine(room.MaxPlayers > 0 ? room.MaxPlayers.ToString() : "-");
            }

            if (me != null && !string.IsNullOrEmpty(me.ID))
            {
                builder.Append("PlayerID: ").AppendLine(me.ID);
                builder.Append("昵称: ").AppendLine(me.Name);
                builder.Append("TransportId: ").AppendLine(me.TransportId.ToString());
            }

            builder.Append("Relay RTT: ").Append(relayTransport.GetRelayServerRtt()).AppendLine(" ms");
        }

        if (player == null)
        {
            Debug.LogError("UI_Debug|BuildText|本机角色未注入");
            builder.AppendLine("角色数据: 未注入");
            return;
        }

        builder.Append("位置: ").AppendLine(player.transform.position.ToString("F1"));
        builder.Append("在地面: ").AppendLine(player.isOnGround ? "是" : "否");
        builder.Append("移动: ").Append(player.isWASDDowm ? "是" : "否")
               .Append("  奔跑: ").Append(player.isRuning ? "是" : "否")
               .Append("  蹲下: ").AppendLine(player.isSquat ? "是" : "否");
        builder.Append("举枪: ").Append(player.isMouseDown ? "是" : "否")
               .Append("  换弹: ").AppendLine(player.isReload ? "是" : "否");

        if (player.Con_gun_Control != null)
        {
            builder.Append("弹药: ").Append(player.Con_gun_Control.ammo).Append('/')
                   .AppendLine(player.Con_gun_Control.maxAmmo.ToString());
            builder.Append("精度圈: ").AppendLine(player.Con_gun_Control.CurrentAngle.ToString("F2"));
        }
    }
}
