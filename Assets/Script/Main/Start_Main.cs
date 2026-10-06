using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;
using Unity.VisualScripting;

//负责开始界面的场景初始化与配置方法
public class Start_Main : MonoBehaviour
{
    public SessionManager session;
    public Button hostButton;
    public Button clientButton;
    public Button startButton;
    public Vector3 StartVector3;
    public GameObject ObjectPrefab;
    public TextMeshProUGUI textMeshPro;

    // 绑定按钮与连接事件
    void Awake()
    {
        if (session == null) session = GetComponent<SessionManager>();
        if (session == null) Debug.LogError("Start_Main|Start|未找到 SessionManager");

        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        startButton.onClick.AddListener(StartGame);

        GameManager.gameManager.networkManager.OnConnectionEvent += OnPlayerJoin;
        GameManager.gameManager.StartAction += Room_Enter;      //回到房间

        Debug.Log("Start_Main|Awake|完成初始化");
    }

    void OnDestroy()
    {
        GameManager.gameManager.networkManager.OnConnectionEvent -= OnPlayerJoin;
        GameManager.gameManager.StartAction -= Room_Enter;
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
        if (data.EventType != ConnectionEvent.ClientConnected || !manager.IsServer) return;

        SpawnPlayer(manager.ConnectedClients.Count - 1);    //新玩家编号
    }

    // 回到房间，放开鼠标并重建成员
    void Room_Enter()
    {
        Cursor.lockState = CursorLockMode.None;     //房间内放开鼠标
        Cursor.visible = true;

        if (!GameManager.gameManager.networkManager.IsServer) return;   //只有主机生成

        // 按当前人数重新生成
        int count = GameManager.gameManager.networkManager.ConnectedClients.Count;
        for (int i = 0; i < count; i++) SpawnPlayer(i);
    }

    // 生成玩家对象，index为编号
    void SpawnPlayer(int index)
    {
        GameObject go = Instantiate(ObjectPrefab,
            StartVector3 + Vector3.right * 2f * index, Quaternion.identity);
        // destroyWithScene 为 true，切场景时销毁
        go.GetComponent<NetworkObject>().Spawn(true);
    }
}
