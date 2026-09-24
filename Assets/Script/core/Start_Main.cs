using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;

public class Start_Main : MonoBehaviour
{
    public NetworkManager networkManager;
    public Button hostButton;
    public Button clientButton;
    public Button startButton;
    public Vector3 StartVector3;
    public GameObject ObjectPrefab;
    public TextMeshProUGUI textMeshPro;

    void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        startButton.onClick.AddListener(StartGame);
        networkManager.OnConnectionEvent += OnPlayerJoin;
    }

    void Update()
    {
        // 一个连接生成一个对象，同步到各端后对象总数即玩家数
        //textMeshPro.text = "当前玩家数:" + networkManager.SpawnManager.SpawnedObjectsList.Count;
    }

    public void StartHost()
    {
        networkManager.StartHost();
    }

    public void StartClient()
    {
        networkManager.StartClient();
    }

    // 由房主点击"开始游戏"：NGO 自动通知所有已连接客户端一起切到游戏场景
    public void StartGame()
    {
        networkManager.SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

    void OnPlayerJoin(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected && manager.IsServer)
            SpawnPlayer();
    }

    // 核心方法：有玩家进入，直接生成对象；位置 = StartVector3.X + 2*(已连接数-1)，依次 +2 排列
    void SpawnPlayer()
    {
        int index = networkManager.ConnectedClients.Count - 1;

        GameObject go = Instantiate(ObjectPrefab,
            StartVector3 + Vector3.right * 2f * index, Quaternion.identity);
        // Spawn(true)：destroyWithScene=true
        go.GetComponent<NetworkObject>().Spawn(true);
    }
}
