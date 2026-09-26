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

    // 绑定按钮与连接事件
    void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        startButton.onClick.AddListener(StartGame);
        networkManager.OnConnectionEvent += OnPlayerJoin;
    }

    // 开主机
    public void StartHost()
    {
        networkManager.StartHost();
    }

    // 连主机
    public void StartClient()
    {
        networkManager.StartClient();
    }

    // 房主开始游戏，各端切场景
    public void StartGame()
    {
        networkManager.SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

    // 有玩家连接时生成对象
    void OnPlayerJoin(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected && manager.IsServer)
            SpawnPlayer();
    }

    // 生成玩家对象，位置依次右移 2
    void SpawnPlayer()
    {
        int index = networkManager.ConnectedClients.Count - 1;

        GameObject go = Instantiate(ObjectPrefab,
            StartVector3 + Vector3.right * 2f * index, Quaternion.identity);
        // destroyWithScene 为 true，切场景时销毁
        go.GetComponent<NetworkObject>().Spawn(true);
    }
}
