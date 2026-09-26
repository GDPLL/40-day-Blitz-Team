using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GameManager : MonoBehaviour
{
    // 游戏状态：房间中 -> 关卡 -> 结算
    public enum GameState { 房间中, 关卡, 结算 }
    public static GameState State { get; private set; } = GameState.房间中;

    [Header("关卡设置")]
    public string levelSceneName = "SampleScene";   // 游戏关卡场景名
    public GameObject playerPrefab;                 // 每个玩家的角色预制体
    public Vector3 startPos;                        // 玩家出生起点，X 依次 +2

    bool levelEntered;      // 已进入关卡（服务器已生成玩家）
    bool localReady;        // 本地玩家已出现并完成初始化

    void Awake()
    {
        DontDestroyOnLoad(gameObject);   // 跨场景不删除（保持唯一 GameManager）
    }

    void Update()
    {
        // 不在关卡中 -> 回到房间中状态
        if (SceneManager.GetActiveScene().name != levelSceneName)
        {
            levelEntered = false;
            localReady = false;
            State = GameState.房间中;
            return;
        }

        // 进入关卡（首次）：服务器生成所有玩家
        if (!levelEntered)
        {
            levelEntered = true;
            State = GameState.关卡;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SpawnPlayers();
        }

        // 本地玩家出现后：统一初始化（绑定相机/UI + 初始化敌人）
        if (!localReady &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localReady = true;
            InitLocalPlayer();
        }
    }

    // 服务器：为每个已连接客户端生成一个玩家对象（归属各自客户端，各自控制）
    void SpawnPlayers()
    {
        int i = 0;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            GameObject go = Instantiate(playerPrefab,
                startPos + Vector3.right * 2f * i, Quaternion.identity);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            i++;
        }
    }

    // 每个客户端各自执行：本地玩家接入场景相机/UI，并初始化所有敌人
    void InitLocalPlayer()
    {
        Transform local = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
        Player_camera camRig = FindObjectOfType<Player_camera>();
        if (camRig == null) return;

        Player_Control pc = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Player_Control>();
        if (pc != null)
        {
            Camera cam = camRig.GetComponent<Camera>();
            RectTransform ui = GameObject.Find("uiPos")?.GetComponent<RectTransform>();
            pc.SetupLocal(cam, camRig, ui);   // 相机跟随本地玩家头部、绑定准星 UI
        }

       
    }

    // 结算（战斗结束/胜负判定后手动调用）
    public void EndGame()
    {
        State = GameState.结算;
    }
}
