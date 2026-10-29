using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;
using Unity.VisualScripting;

//负责开始界面的场景初始化与配置方法
public class Start_Main : MonoBehaviour
{
    const int HardPlayerLimit = 4;

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

        int index = manager.ConnectedClients.Count - 1;
        if (index < LobbyPlayerLimit) SpawnPlayer(index);    //新玩家编号
    }

    // 回到房间，放开鼠标并重建成员
    void Room_Enter()
    {
        Cursor.lockState = CursorLockMode.None;     //房间内放开鼠标
        Cursor.visible = true;

        if (!GameManager.gameManager.networkManager.IsServer) return;   //只有主机生成

        // 按当前人数重新生成
        int count = Mathf.Min(
            GameManager.gameManager.networkManager.ConnectedClients.Count,
            LobbyPlayerLimit);
        for (int i = 0; i < count; i++) SpawnPlayer(i);
    }

    int LobbyPlayerLimit => Mathf.Clamp(MaxLobbyPlayers, 1, HardPlayerLimit);

    // 生成玩家对象，index为编号
    void SpawnPlayer(int index)
    {
        if (index < 0 || index >= LobbyPlayerLimit) return;

        // 固定四个展示位，Camera.right 对应屏幕从左到右。
        float centerIndex = (LobbyPlayerLimit - 1) * 0.5f;
        Camera mainCamera = Camera.main;
        Vector3 right = mainCamera != null ? mainCamera.transform.right : Vector3.back;

        float slotOffset = (index - centerIndex) * PlayerSpacing;
        if (index == 0) slotOffset -= FirstPlayerExtraLeft;

        // 首位额外左移后，按最左和最右槽位的包围中心整体回正。
        float leftmost = -centerIndex * PlayerSpacing - FirstPlayerExtraLeft;
        float rightmost = centerIndex * PlayerSpacing;
        float rowCenter = (leftmost + rightmost) * 0.5f;
        Vector3 spawnPosition = StartVector3 + right * (slotOffset - rowCenter);

        Vector3 faceCamera = mainCamera != null
            ? mainCamera.transform.position - spawnPosition
            : Vector3.zero;
        faceCamera.y = 0f;
        Quaternion rotation = faceCamera.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(faceCamera)
            : Quaternion.Euler(StartRotation);

        GameObject go = Instantiate(ObjectPrefab, spawnPosition, rotation);

        // destroyWithScene 为 true，切场景时销毁
        go.GetComponent<NetworkObject>().Spawn(true);
    }
}
