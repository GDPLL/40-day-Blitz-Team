using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GameManager : MonoBehaviour
{
    // 游戏状态
    public enum GameState { 房间中, 关卡, 结算 }
    public static GameState State { get; private set; } = GameState.房间中;

    [Header("关卡设置")]
    public string levelSceneName = "SampleScene";   // 关卡场景名
    public GameObject playerPrefab;                 // 角色预制体
    public Vector3 startPos;                        // 出生起点

    bool levelEntered;      // 已进入关卡
    bool localReady;        // 本地玩家已初始化

    // 跨场景保留
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // 驱动状态与玩家生成
    void Update()
    {
        // 不在关卡则回房间状态
        if (SceneManager.GetActiveScene().name != levelSceneName)
        {
            levelEntered = false;
            localReady = false;
            State = GameState.房间中;
            return;
        }

        // 首次进关卡生成玩家
        if (!levelEntered)
        {
            levelEntered = true;
            State = GameState.关卡;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SpawnPlayers();
        }

        // 本地玩家出现后初始化
        if (!localReady &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localReady = true;
            InitLocalPlayer();
        }
    }

    // 为每个客户端生成玩家
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

    // 绑定本地相机与UI
    void InitLocalPlayer()
    {
        Transform local = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
        Player_camera camRig = FindObjectOfType<Player_camera>();
        if (camRig == null)
        {
            Debug.LogError("GameManager|InitLocalPlayer|未找到 Player_camera");
            return;
        }

        Player_Control pc = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Player_Control>();
        if (pc == null)
        {
            Debug.LogError("GameManager|InitLocalPlayer|玩家上未找到 Player_Control");
            return;
        }

        Camera cam = camRig.GetComponent<Camera>();
        pc.SetupLocal(cam, camRig);   // 绑定相机
        Debug.Log("绑定完成");

        // 把本机角色交给调试面板
        UI_Debug debug = FindObjectOfType<UI_Debug>();
        if (debug != null) debug.SetupDebug(pc);
    }

    // 结束游戏
    public void EndGame()
    {
        State = GameState.结算;
    }
}
