using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Lobby Controller: handles networking commands; all button/text rendering belongs to LobbyView.
public sealed class Start_Main : MonoBehaviour
{
    [Header("Migration bindings - transferred to LobbyView in Awake")]
    public NetworkManager networkManager;
    public Button hostButton;
    public Button clientButton;
    public Button startButton;
    public TextMeshProUGUI textMeshPro;
    public Vector3 StartVector3;
    public GameObject ObjectPrefab;

    private LobbyView view;
    private readonly GameSessionModel model = new GameSessionModel();

    private void Awake()
    {
        view = GetComponent<LobbyView>();
        if (view == null) view = gameObject.AddComponent<LobbyView>();
        view.Configure(hostButton, clientButton, startButton, textMeshPro);
        view.HostClicked += StartHost;
        view.ClientClicked += StartClient;
        view.StartClicked += StartGame;
    }

    private void OnEnable()
    {
        if (networkManager != null) networkManager.OnConnectionEvent += OnPlayerJoin;
    }

    private void OnDisable()
    {
        if (networkManager != null) networkManager.OnConnectionEvent -= OnPlayerJoin;
    }

    private void Update()
    {
        int count = networkManager == null ? 0 : networkManager.ConnectedClients.Count;
        model.SetPlayerCount(count);
        view.RenderPlayerCount(model.PlayerCount);
    }

    public void StartHost() => networkManager?.StartHost();
    public void StartClient() => networkManager?.StartClient();
    public void StartGame()
    {
        if (networkManager != null && networkManager.IsServer)
            networkManager.SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

    private void OnPlayerJoin(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType != ConnectionEvent.ClientConnected) return;
        model.SetPlayerCount(manager.ConnectedClients.Count);
        // Gameplay players are created by GameManager after scene load; this preserves lobby avatars.
        if (manager.IsServer && ObjectPrefab != null)
        {
            GameObject avatar = Instantiate(ObjectPrefab, StartVector3 + Vector3.right * 2f * (model.PlayerCount - 1), Quaternion.identity);
            avatar.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }
}
