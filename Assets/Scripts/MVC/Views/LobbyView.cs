using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Lobby View: owns widget display and exposes clicks as events.
public sealed class LobbyView : MonoBehaviour
{
    private Button hostButton, clientButton, startButton;
    private TextMeshProUGUI playerCountText;
    public event Action HostClicked, ClientClicked, StartClicked;

    public void Configure(Button host, Button client, Button start, TextMeshProUGUI label)
    {
        hostButton = host; clientButton = client; startButton = start; playerCountText = label;
        if (hostButton != null) hostButton.onClick.AddListener(() => HostClicked?.Invoke());
        if (clientButton != null) clientButton.onClick.AddListener(() => ClientClicked?.Invoke());
        if (startButton != null) startButton.onClick.AddListener(() => StartClicked?.Invoke());
    }

    public void RenderPlayerCount(int count)
    {
        if (playerCountText != null) playerCountText.text = $"当前玩家数:{count}";
    }
}
