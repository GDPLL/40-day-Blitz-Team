using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;

//负责开始界面的场景初始化与配置方法
public class Start_Main : MonoBehaviour
{
    const int HardPlayerLimit = 4;
    const int HostTestSlot = 0;   //房主测试使用玩家1

    public static Start_Main Instance { get; private set; }

    public SessionManager session;
    public Button hostButton;
    public Button clientButton;
    public Button startButton;
    public Vector3 StartVector3;
    public Vector3 StartRotation;
    public float PlayerSpacing = 1.5f;
    public float FirstPlayerExtraLeft = 2f;
    public int MaxLobbyPlayers = HardPlayerLimit;
    public GameObject ObjectPrefab;
    public TextMeshProUGUI textMeshPro;

    readonly Dictionary<ulong, int> clientSlots = new Dictionary<ulong, int>();
    readonly Dictionary<ulong, NetworkObject> lobbyPlayers =
        new Dictionary<ulong, NetworkObject>();
    StartViewStage viewStage;

    // 绑定按钮与连接事件
    void Awake()
    {
        Instance = this;

        if (session == null) session = GetComponent<SessionManager>();
        if (session == null) Debug.LogError("Start_Main|Start|未找到 SessionManager");

        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        startButton.onClick.AddListener(StartGame);

        GameManager.gameManager.networkManager.OnConnectionEvent += OnPlayerJoin;
        GameManager.gameManager.StartAction += Room_Enter;      //回到房间
        EnsureViewStage();

        Debug.Log("Start_Main|Awake|完成初始化");
    }

    void OnDestroy()
    {
        if (GameManager.gameManager != null
            && GameManager.gameManager.networkManager != null)
        {
            GameManager.gameManager.networkManager.OnConnectionEvent -= OnPlayerJoin;
            GameManager.gameManager.StartAction -= Room_Enter;
        }
        if (Instance == this) Instance = null;
    }

    // 开主机，创建中继房间
    public void StartHost()
    {
        session.StartHost();
    }

    // 连主机，加入中继房间
    public void StartClient()
    {
        session.StartClient();
    }

    // 房主开始游戏，各端切场景
    public void StartGame()
    {
        GameManager.gameManager.Scence_SwitchClientRpc(GameManager.GameState.关卡);
    }

    // 有玩家连接时生成对象
    void OnPlayerJoin(NetworkManager manager, ConnectionEventData data)
    {
        if (!manager.IsServer) return;

        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            int index = AssignClientSlot(data.ClientId);
            if (index >= 0) SpawnPlayer(data.ClientId, index);
        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
        {
            RemoveLobbyPlayer(data.ClientId);
        }
    }

    // 回到房间，放开鼠标并重建成员
    void Room_Enter()
    {
        Cursor.lockState = CursorLockMode.None;     //房间内放开鼠标
        Cursor.visible = true;

        if (!GameManager.gameManager.networkManager.IsServer) return;   //只有主机生成

        // 按当前客户端补齐缺失的展示玩家
        List<ulong> clientIds =
            new List<ulong>(GameManager.gameManager.networkManager.ConnectedClientsIds);
        clientIds.Sort();

        for (int i = 0; i < clientIds.Count; i++)
        {
            int slot = AssignClientSlot(clientIds[i]);
            if (slot >= 0) SpawnPlayer(clientIds[i], slot);
        }
    }

    int LobbyPlayerLimit => Mathf.Clamp(MaxLobbyPlayers, 1, HardPlayerLimit);

    // 生成玩家并绑定对应客户端视角
    void SpawnPlayer(ulong clientId, int index)
    {
        if (index < 0 || index >= LobbyPlayerLimit) return;
        if (lobbyPlayers.TryGetValue(clientId, out NetworkObject existing)
            && existing != null) return;

        EnsureViewStage();
        Vector3 spawnPosition = viewStage.GetPlayerPosition(index);
        Quaternion rotation = viewStage.GetPlayerRotation(index);

        GameObject go = Instantiate(ObjectPrefab, spawnPosition, rotation);
        StartPlayerDisplay display = go.GetComponent<StartPlayerDisplay>();
        if (display == null)
        {
            Debug.LogError("Start_Main|SpawnPlayer|展示组件缺失");
            Destroy(go);
            return;
        }

        display.ConfigureLobbyDisplay(index, clientId);

        // destroyWithScene 为 true，切场景时销毁
        NetworkObject networkObject = go.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("Start_Main|SpawnPlayer|NetworkObject缺失");
            Destroy(go);
            return;
        }

        networkObject.Spawn(true);
        lobbyPlayers[clientId] = networkObject;
        Debug.Log($"Start_Main|SpawnPlayer|客户端{clientId}对应玩家{index + 1}");
    }

    // 为客户端分配最小空闲席位
    int AssignClientSlot(ulong clientId)
    {
        if (clientSlots.TryGetValue(clientId, out int assigned)) return assigned;

        if (NetworkManager.Singleton != null
            && clientId == NetworkManager.ServerClientId
            && !clientSlots.ContainsValue(HostTestSlot))
        {
            clientSlots[clientId] = HostTestSlot;
            return HostTestSlot;
        }

        for (int slot = 0; slot < LobbyPlayerLimit; slot++)
        {
            if (slot == HostTestSlot) continue;   //留给房主
            if (clientSlots.ContainsValue(slot)) continue;
            clientSlots[clientId] = slot;
            return slot;
        }

        Debug.LogWarning($"Start_Main|AssignClientSlot|玩家已满，忽略客户端{clientId}");
        return -1;
    }

    // 客户端离开时释放对应展示对象
    void RemoveLobbyPlayer(ulong clientId)
    {
        if (lobbyPlayers.TryGetValue(clientId, out NetworkObject player)
            && player != null)
            player.Despawn(true);

        lobbyPlayers.Remove(clientId);
        clientSlots.Remove(clientId);
    }

    // 本机玩家收到席位后切换镜头
    public void ShowViewForSlot(int slot)
    {
        EnsureViewStage();
        viewStage.ShowSlot(slot);
    }

    public float GetViewSurfaceHeight(int slot)
    {
        EnsureViewStage();
        return viewStage.GetPlayerSurfaceHeight(slot);
    }

    public Vector3 GetViewAimDirection(int slot)
    {
        EnsureViewStage();
        return viewStage.GetAimDirection(slot);
    }

    // 场景视图组件只在本场景创建
    void EnsureViewStage()
    {
        if (viewStage != null) return;

        viewStage = GetComponent<StartViewStage>();
        if (viewStage == null) viewStage = gameObject.AddComponent<StartViewStage>();
        viewStage.BuildStage(StartVector3);
    }
}
