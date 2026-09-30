using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Netcode;
// 游戏场景的初始化与配置
public class Player_Main : NetworkBehaviour
{
    public static Player_Main player_Main { get; private set; }


    // 场景引用
    public Camera oCamera;                  // 相机
    public Player_camera oPlayer_Camera;
    public Player_Control oPlayer_Control;          // 当前玩家总控制脚本
    public List<Player_Control> oplayer_Controls;   // 当前玩家列表
    public RectTransform oUI_RectTransform;         // 准星UI
    public UI_Debug uI_Debug;                       //UI测试代码

    bool levelEntered;                  // 已进入关卡
    bool hasLocalPlayer;                // 是否找到本地玩家
    public GameObject playerPrefab;     // 角色预制体    
    public Vector3 startPos;            // 出生起点
    public bool isInit { get; private set; }                //初始化准备完成


    //游戏场景总驱动器
    void Update()
    {
        if (!isInit) return;

        for (int i = 0; i < oplayer_Controls.Count; i++)
            oplayer_Controls[i].Player_Control_Update();

        // 本机表现层驱动
        if (oPlayer_Control != null) oPlayer_Control.Player_Control_Local_Update();
    }

    //游戏场景物理总驱动器
    void FixedUpdate()
    {
        if (!isInit) return;

        for (int i = 0; i < oplayer_Controls.Count; i++)
            oplayer_Controls[i].Player_Control_FixedUpdate();
    }

    //订阅场景初始化
    void Awake()
    {
        if (player_Main == null)
        {
            player_Main = this;
        }
        else
        {
            Destroy(this);
            return;
        }


        isInit = false;

        if (GameManager.gameManager != null) GameManager.gameManager.GameAction += GameScence_Init;
        else Debug.LogError("Player_Main|Awake|GameManager 为空");
    }

    // 取消订阅
    public override void OnDestroy()
    {
        base.OnDestroy();

        if (GameManager.gameManager != null) GameManager.gameManager.GameAction -= GameScence_Init;
        if (player_Main == this) player_Main = null;
    }

    //初始化游戏场景
    void GameScence_Init()
    {
        // 首次进关卡生成玩家
        if (!levelEntered)
        {
            levelEntered = true;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SpawnPlayers();
        }
    }

    // 服务器为每个客户端生成玩家
    void SpawnPlayers()
    {
        int i = 0;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            GameObject go = Instantiate(playerPrefab,
                startPos + Vector3.right * 2f * i, Quaternion.identity);
            i++;

            Player_Control go_control = go.GetComponent<Player_Control>();
            if (go_control == null)
            {
                Debug.LogError("Player_Main|SpawnPlayers|未找到 Player_Control");
                continue;
            }
            oplayer_Controls.Add(go_control);

            NetworkObject go_netObj = go.GetComponent<NetworkObject>();
            if (go_netObj == null)
            {
                Debug.LogError("Player_Main|SpawnPlayers|未找到 NetworkObject");
                continue;
            }
            go_netObj.SpawnAsPlayerObject(clientId);

            // 主机初始化该玩家
            go_control.Player_Control_Start(Input_Manage.Instance);
            go_control.Player_Control_Local_Init(oCamera, Input_Manage.Instance, oPlayer_Camera);   //只有本机玩家生效
        }

        JudgePlayerClientRpc();
        InitScenceClientRpc();
    }

    // 客户端寻找本地玩家
    [ClientRpc]
    void JudgePlayerClientRpc()
    {
        var netSingle = NetworkManager.Singleton;
        if (netSingle == null || netSingle.LocalClient == null)
        {
            Debug.LogError("Player_Main|JudgePlayerClientRpc|NetworkManager 为空");
            return;
        }

        foreach (var kv in netSingle.SpawnManager.SpawnedObjects)
        {
            Player_Control control = kv.Value.GetComponent<Player_Control>();
            if (control == null) continue;

            if (kv.Value.OwnerClientId == netSingle.LocalClientId)
            {
                oPlayer_Control = control;
                hasLocalPlayer = true;
                return;
            }
        }

        Debug.LogError("Player_Main|JudgePlayerClientRpc|未找到本地玩家");
    }

    // 客户端初始化场景
    [ClientRpc]
    void InitScenceClientRpc()
    {
        if (!hasLocalPlayer)
        {
            Debug.LogError("Player_Main|InitScenceClientRpc|本地玩家缺失");
            return;
        }
        if (oPlayer_Camera == null || uI_Debug == null)
        {
            Debug.LogError("Player_Main|InitScenceClientRpc|相机或调试面板为空");
            return;
        }

        oPlayer_Control.Player_Control_Start(Input_Manage.Instance);
        oPlayer_Control.Player_Control_Local_Init(oCamera, Input_Manage.Instance, oPlayer_Camera);
        uI_Debug.UI_Debug_Start(oPlayer_Control, oPlayer_Control.Con_input_Manage);

        isInit = true;

        Debug.Log("Player_Main|InitScenceClientRpc|完成初始化");
    }
}
