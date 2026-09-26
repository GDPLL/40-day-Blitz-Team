using System.Text;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using Unity.Sync.Relay.Lobby;
using Unity.Sync.Relay.Model;
using Unity.Sync.Relay.Transport.Netcode;

// 房间场景调试面板
public class Lobby_Debug : MonoBehaviour
{
    public TMP_Text output;   // 输出文本

    RelayTransportNetcode relayTransport;   // 中继组件
    readonly StringBuilder builder = new StringBuilder(512);   // 文本缓存

    // 订阅调试事件
    void Start()
    {
        if (Input_Manage.Instance == null)
        {
            Debug.LogError("Lobby_Debug|Start|未找到 Input_Manage");
            return;
        }

        Input_Manage.Instance.Debug_event += OnDebug;
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
            Debug.LogError("Lobby_Debug|OnDebug|output 未绑定");
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

    // 拼接身份、房间与成员信息
    void BuildText()
    {
        builder.Clear();

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
        {
            Debug.LogError("Lobby_Debug|BuildText|未找到 NetworkManager");
            builder.AppendLine("未找到 NetworkManager");
            return;
        }

        if (relayTransport == null || relayTransport.gameObject != manager.gameObject)
        {
            relayTransport = manager.GetComponent<RelayTransportNetcode>();
        }

        if (relayTransport == null)
        {
            Debug.LogError("Lobby_Debug|BuildText|未挂载 RelayTransportNetcode");
            builder.AppendLine("NetworkManager 上未挂载 RelayTransportNetcode");
            return;
        }

        RelayRoom room = relayTransport.GetRoomInfo();       //房间信息
        RelayPlayer me = relayTransport.GetCurrentPlayer();  //本机玩家

        builder.Append("本机身份: ").AppendLine(
            !manager.IsListening ? "未连接" :
            manager.IsHost ? "Host" :
            manager.IsServer ? "Server" : "Client");

        builder.Append("连接状态: ").AppendLine(
            !manager.IsListening ? "未启动" :
            manager.IsServer ? "已启动" :
            manager.IsConnectedClient ? "已连接" : "连接中");

        builder.Append("LocalClientId: ").AppendLine(manager.LocalClientId.ToString());
        builder.Append("IsHost / IsServer / IsClient: ")
               .Append(manager.IsHost).Append(" / ")
               .Append(manager.IsServer).Append(" / ")
               .AppendLine(manager.IsClient.ToString());

        if (me != null && !string.IsNullOrEmpty(me.ID))
        {
            builder.Append("PlayerID: ").AppendLine(me.ID);
            builder.Append("昵称: ").AppendLine(me.Name);
            builder.Append("TransportId: ").AppendLine(me.TransportId.ToString());
            builder.Append("是否房主: ")
                   .AppendLine(room != null && room.MasterClientID == me.TransportId ? "是" : "否");
        }
        else
        {
            builder.AppendLine("玩家信息: 未设置");
        }

        builder.Append("Relay RTT: ").Append(relayTransport.GetRelayServerRtt()).AppendLine(" ms");

        if (room == null || string.IsNullOrEmpty(room.ID))
        {
            builder.AppendLine("房间信息: 未加入任何房间");
            return;
        }

        builder.Append("房间名: ").AppendLine(room.Name);
        builder.Append("Namespace: ").AppendLine(room.NameSpace);
        builder.Append("RoomUuid: ").AppendLine(room.ID);
        builder.Append("RoomCode: ").AppendLine(string.IsNullOrEmpty(room.RoomCode) ? "-" : room.RoomCode);
        builder.Append("状态: ").Append(room.Status).Append(" (").Append(LobbyRoomStatusHelper.ValueOf(room.Status)).AppendLine(")");
        builder.Append("可见性: ").AppendLine(room.Visibility.ToString());
        builder.Append("人数: ").Append(room.Players?.Count ?? 0).Append('/')
               .AppendLine(room.MaxPlayers > 0 ? room.MaxPlayers.ToString() : "-");
        builder.Append("JoinCode: ").AppendLine(string.IsNullOrEmpty(room.JoinCode) ? "-" : room.JoinCode);

        int memberCount = 0;
        bool meFound = false;

        if (room.Players != null)
        {
            foreach (RelayPlayer player in room.Players.Values)
            {
                if (player == null) continue;

                memberCount++;
                if (me != null && player.ID == me.ID) meFound = true;
                AppendMember(room, player, me);
            }
        }

        // 成员列表可能还没同步到本机，补一条保证可见
        if (me != null && !string.IsNullOrEmpty(me.ID) && !meFound)
        {
            memberCount++;
            AppendMember(room, me, me);
        }

        builder.Append("成员数量: ").AppendLine(memberCount.ToString());
    }

    // 拼接单个成员信息
    void AppendMember(RelayRoom room, RelayPlayer player, RelayPlayer me)
    {
        builder.Append(player.Name)
               .Append("  TransportId=").Append(player.TransportId)
               .Append("  PlayerID=").Append(player.ID);

        if (room.MasterClientID == player.TransportId) builder.Append("  <房主>");
        if (me != null && player.ID == me.ID) builder.Append("  <本机>");

        builder.AppendLine();
    }
}
