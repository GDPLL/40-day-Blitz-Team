using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Sync.Relay;
using Unity.Sync.Relay.Lobby;
using Unity.Sync.Relay.Model;
using Unity.Sync.Relay.Transport.Netcode;

// Sync Relay 中继房间管理
public class SessionManager : MonoBehaviour
{
    [Header("房间配置")]
    [SerializeField] private string roomName = "Demo";      // 房间名称
    [SerializeField] private string roomNamespace = "Unity";    // 房间命名空间
    [SerializeField] private LobbyRoomVisibility visibility = LobbyRoomVisibility.Public;   // 房间可见性
    [SerializeField] private string privateJoinCode = "";   // Private 房间加入码，空则随机生成
    [SerializeField] private string roomProfileUUID = "";   // 房间配置ID，空则用组件上的

    [Header("玩家信息")]
    [SerializeField] private string playerID = "";      // 玩家ID，空则自动生成
    [SerializeField] private string playerName = "";    // 玩家昵称，空则自动生成

    [SerializeField] private bool closeRoomWhenHostLeaves = true;   // 房主离开时关闭房间

    RelayTransportNetcode relayTransport;   // 中继组件

    // 创建房间并启动主机
    public async void StartHost()
    {
        try
        {
            string joinCode = await StartHostWithRelay(3);
            if (!string.IsNullOrEmpty(joinCode))
            {
                Debug.Log($"中继分配完成，加入代码: {joinCode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 按房间名加入房间
    public void StartClient()
    {
        try
        {
            StartClientWithRelay();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 离开房间，房主额外关闭房间
    public void LeaveRoom()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("SessionManager|LeaveRoom|NetworkManager 不存在");
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("SessionManager|LeaveRoom|当前未在房间中");
            return;
        }

        bool isHostOrServer = NetworkManager.Singleton.IsServer;
        RelayRoom room = relayTransport != null ? relayTransport.GetRoomInfo() : null;
        string roomUuid = room != null ? room.ID : null;

        // 断开连接并退出房间
        NetworkManager.Singleton.Shutdown();
        Debug.Log($"已离开房间（{(isHostOrServer ? "Host/Server" : "Client")}），RoomUuid: {roomUuid}");

        // 房主退出后关闭房间
        if (closeRoomWhenHostLeaves && isHostOrServer && !string.IsNullOrEmpty(roomUuid))
        {
            StartCoroutine(CloseRoom(roomUuid));
        }
    }

    // 创建中继房间并启动主机
    public Task<string> StartHostWithRelay(int maxConnections = 3)
    {
        var tcs = new TaskCompletionSource<string>();
        StartCoroutine(CreateRoomAndStartHost(maxConnections, tcs));
        return tcs.Task;
    }

    // 按本地房间名加入房间
    public void StartClientWithRelay()
    {
        StartCoroutine(JoinRoomAndStartClient());
    }

    // 查询大厅房间并加入
    private IEnumerator JoinRoomAndStartClient()
    {
        // 先设置玩家信息与回调
        if (!PrepareRelayTransport())
        {
            yield break;
        }

        var request = new ListRoomRequest   //筛选房间
        {
            Namespace = roomNamespace,
            Start = 0,
            Count = 10,
            Name = roomName,
            Statuses = new List<LobbyRoomStatus> { LobbyRoomStatus.Ready, LobbyRoomStatus.Running }
        };

        //等待大厅房间查询
        yield return LobbyService.AsyncListRoom(request, listResp =>
        {
            if (listResp.Code != (uint)RelayCode.OK) return;

            foreach (var item in listResp.Items)
            {
                if (item.Status != LobbyRoomStatus.Ready
                && item.Status != LobbyRoomStatus.Running) continue;

                if (item.Name != roomName) continue;

                StartCoroutine(LobbyService.AsyncQueryRoom(item.RoomUuid, response =>
                {
                    if (response == null)
                    {
                        Debug.LogError("SessionManager|JoinRoomAndStartClient|Lobby 未返回结果");
                        return;
                    }

                    if (response.Code != (uint)RelayCode.OK)
                    {
                        Debug.LogError($"SessionManager|JoinRoomAndStartClient|加入房间失败 {RelayStatusCodeHelper.Convert(response.Code).Description}");
                        return;
                    }
                    relayTransport.SetRoomData(response);   //提取Relay参数并设置到relayTransport
                    NetworkManager.Singleton.StartClient(); //以成员模式参与
                    Debug.Log($"房间加入成功，房间名称{item.Name}");
                }));

                break;
            }

        });
    }

    // 创建房间并启动主机
    private IEnumerator CreateRoomAndStartHost(int maxConnections, TaskCompletionSource<string> tcs)
    {
        // 先设置玩家信息与回调
        if (!PrepareRelayTransport())
        {
            tcs.TrySetResult(null);
            yield break;
        }

        bool isPrivate = visibility == LobbyRoomVisibility.Private;

        // 为私人房间生成随机加入码
        string joinCode = isPrivate
            ? (string.IsNullOrEmpty(privateJoinCode) ? GenerateJoinCode() : privateJoinCode)
            : null;

        var request = new CreateRoomRequest //房间配置信息
        {
            Name = roomName,
            Namespace = roomNamespace,
            MaxPlayers = maxConnections,
            Visibility = visibility,
            OwnerId = playerID,     //房主ID
            JoinCode = joinCode,
            RoomProfileUUID = roomProfileUUID,  //房间配置ID
        };

        CreateRoomResponse response = null;     //创建房间返回信息
        yield return LobbyService.AsyncCreateRoom(request, resp => response = resp);

        if (response == null)
        {
            Debug.LogError("SessionManager|CreateRoomAndStartHost|Lobby 未返回结果");
            tcs.TrySetResult(null);
            yield break;
        }

        if (response.Code != (uint)RelayCode.OK)
        {
            Debug.LogError($"SessionManager|CreateRoomAndStartHost|创建房间失败 {RelayStatusCodeHelper.Convert(response.Code).Description}");
            tcs.TrySetResult(null);
            yield break;
        }

        if (response.Status != LobbyRoomStatus.ServerAllocated)
        {
            Debug.LogError($"SessionManager|CreateRoomAndStartHost|房间状态异常 {response.Status}");
            tcs.TrySetResult(null);
            yield break;
        }

        Debug.Log($"分配创建成功! RoomUuid: {response.RoomUuid}");

        // 注入 Relay 连接参数
        relayTransport.SetRoomData(response);
        // Private 房间需要额外设置 JoinCode
        if (isPrivate && !string.IsNullOrEmpty(joinCode))
        {
            relayTransport.SetJoinCode(joinCode);
        }

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("SessionManager|CreateRoomAndStartHost|Host 启动失败");
            tcs.TrySetResult(null);
            yield break;
        }

        // RoomCode 供客户端查找房间，Private 房间返回 JoinCode
        string result = !string.IsNullOrEmpty(response.RoomCode) ? response.RoomCode : response.JoinCode;
        Debug.Log($"Host 启动成功，加入代码: {result}");
        tcs.TrySetResult(result);
    }

    // 取中继组件并设置玩家信息与回调
    private bool PrepareRelayTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("SessionManager|PrepareRelayTransport|未找到 NetworkManager");
            return false;
        }

        //获取中继协议配置
        relayTransport = NetworkManager.Singleton.GetComponent<RelayTransportNetcode>();
        if (relayTransport == null)
        {
            Debug.LogError("SessionManager|PrepareRelayTransport|未挂载 RelayTransportNetcode");
            return false;
        }

        if (string.IsNullOrEmpty(playerID))
        {
            playerID = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player-" + playerID.Substring(0, Math.Min(8, playerID.Length));
        }

        relayTransport.SetPlayerData(playerID, playerName); //向Relay声明当前对象信息

        var callbacks = new RelayCallbacks();
        callbacks.RegisterConnectToRelayServer(OnConnectToRelayServer); //设置连接回调
        relayTransport.SetCallbacks(callbacks);

        return true;
    }

    // 通知 Lobby 关闭房间，仅房主调用
    private IEnumerator CloseRoom(string roomUuid)
    {
        CloseRoomResponse response = null;
        yield return LobbyService.AsyncCloseRoom(roomUuid, resp => response = resp);

        if (response != null && response.Code == (uint)RelayCode.OK)
        {
            Debug.Log($"房间已关闭: {roomUuid}");
        }
        else
        {
            string reason = response == null ? "Lobby 未返回结果" : RelayStatusCodeHelper.Convert(response.Code).Description;
            Debug.LogWarning($"SessionManager|CloseRoom|关闭房间失败 {reason}");
        }
    }

    // 中继连接结果回调
    private void OnConnectToRelayServer(uint code, RelayRoom room)
    {
        if (code == (uint)RelayCode.OK)
        {
            Debug.Log($"已连接到 Relay 服务器，房间: {room?.Name}");
        }
        else
        {
            Debug.LogError($"SessionManager|OnConnectToRelayServer|连接 Relay 失败 {code} {RelayStatusCodeHelper.Convert(code).Description}");
        }
    }

    // 生成 6 位随机加入码
    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var builder = new System.Text.StringBuilder(6);
        for (int i = 0; i < 6; i++)
        {
            builder.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        }

        return builder.ToString();
    }
}
