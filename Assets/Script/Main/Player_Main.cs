using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Netcode;
using Unity.Mathematics;
using System;
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
    public UI_Player uI_Player;                     //本机弹药血量UI
    public UI_curtain uI_Curtain;                   //UI幕布
    public UI_Time uI_Time;                         //关卡时间UI

    bool levelEntered;                  // 已进入关卡
    bool hasLocalPlayer;                // 是否找到本地玩家
    bool gameOver;                      // 游戏已结束
    public GameObject playerPrefab;     // 角色预制体    
    public Vector3 startPos;            // 出生起点
    public Transform[] Num1respawnPoints;   // 复活点，按玩家编号
    public Transform[] Num2respawnPoints;   // 复活点，按玩家编号
    public Transform[] Num3respawnPoints;   // 复活点，按玩家编号
    public bool isInit { get; private set; }                //初始化准备完成

    NetworkVariable<float> netGameTime = new NetworkVariable<float>();   //关卡时间，同步显示
    //关卡时间，主机权威
    float Level_Time => IsServer ? Player_level.Instance.gameTime : netGameTime.Value;


    //游戏场景总驱动器
    void Update()
    {
        if (!isInit) return;

        // 游戏结束判定，撤离成功即获胜
        if (IsServer && !gameOver && Player_level.Instance != null && Player_level.Instance.Level_end)
            GameOver_Start(Player_level.Instance.WinnerId);

        // 玩家更新
        for (int i = 0; i < oplayer_Controls.Count; i++)
        {
            if (oplayer_Controls[i] == null)      //退出的玩家
            {
                oplayer_Controls.RemoveAt(i);
                continue;
            }
            oplayer_Controls[i].Player_Control_Date_HostUpdate();
            oplayer_Controls[i].Player_Control_Show_ClientUpdate();
        }

        // 关卡时间与事件提示
        if (uI_Time != null && Player_level.Instance != null)
            uI_Time.UI_Time_Show_Time(Level_Time);


        // 本机表现层驱动
        if (oPlayer_Control != null)
        {
            oPlayer_Control.Player_Control_Show_ClientUpdate_Own();

            // 本机弹药血量
            if (uI_Player != null)
                uI_Player.UI_Player_Show(oPlayer_Control.Ammo, oPlayer_Control.MaxAmmo,
                    oPlayer_Control.HP, oPlayer_Control.MaxHp);

            // 死亡等待幕布，淘汰后常驻
            if (uI_Curtain != null && !gameOver)
            {
                float wait = oPlayer_Control.RespawnWait;

                if (oPlayer_Control.Is_Out) uI_Curtain.UI_curtain_Show("已淘汰，等待本局结束");
                else if (wait > 0f) uI_Curtain.UI_curtain_Show($"等待复活 {wait:0.0}s");
                else uI_Curtain.UI_curtain_Hide();
            }
        }
    }

    //游戏场景物理总驱动器
    void FixedUpdate()
    {
        if (!isInit) return;

        for (int i = oplayer_Controls.Count - 1; i >= 0; i--)
        {
            if (oplayer_Controls[i] == null)      //退出的玩家
            {
                oplayer_Controls.RemoveAt(i);
                continue;
            }
            oplayer_Controls[i].Player_Control_Date_HostFixedUpdate();
        }

        // 关卡计时与机关，主机检测各端表现
        if (Player_level.Instance != null)
        {
            Player_level.Instance.Player_level_FixUpdate();
            Player_level.Instance.Player_level_FixUpdate_Performance();

            // 关卡时间同步各端
            if (IsServer) netGameTime.Value = Player_level.Instance.gameTime;
        }

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

        // 订阅玩家断开
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("Player_Main|Awake|NetworkManager 为空");
            return;
        }
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    // 取消订阅
    public override void OnDestroy()
    {
        base.OnDestroy();

        if (GameManager.gameManager != null)
            GameManager.gameManager.GameAction -= GameScence_Init;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

        // 取消撤离点播报与关卡节点
        if (Player_level.Instance != null)
        {
            if (Player_level.Instance.level_EndCollider != null)
                Player_level.Instance.level_EndCollider.Evac_Tip_event -= OnEvac_Tip;

            Player_level.Instance.Node_event -= OnLevel_Node;
        }

        if (player_Main == this) player_Main = null;
    }

    // 玩家断开，移出玩家列表
    void OnClientDisconnect(ulong clientId)
    {
        for (int i = oplayer_Controls.Count - 1; i >= 0; i--)
        {
            Player_Control control = oplayer_Controls[i];
            if (control == null || control.OwnerClientId == clientId)
                oplayer_Controls.RemoveAt(i);
        }

        Debug.Log($"Player_Main|OnClientDisconnect|玩家 {clientId} 已离开");

        // 对手离开，剩余者获胜
        if (IsServer) JudgeWinner_Date();
    }

    // 击杀结算，重置击杀者死亡计数
    public void Player_Kill(ulong killerId)
    {
        if ((int)killerId > oplayer_Controls.Count - 1) return;
        for (int i = 0; i < oplayer_Controls.Count; i++)
        {
            Player_Control control = oplayer_Controls[i];
            if (control == null || control.OwnerClientId != killerId) continue;

            control.Player_Death_Reset();
            return;
        }
    }

    // 游戏结束，广播胜利者
    void GameOver_Start(ulong winnerId)
    {
        if (gameOver) return;      //只结算一次

        gameOver = true;

        int winnerIndex = 0;                           //胜利者编号

        for (int i = 0; i < oplayer_Controls.Count; i++)
        {
            Player_Control control = oplayer_Controls[i];
            if (control == null || control.OwnerClientId != winnerId) continue;

            winnerIndex = control.Player_Number;
            break;
        }

        GameOverClientRpc(winnerIndex);
        StartCoroutine(GameOver_End());
    }

    // 5秒后回大厅
    IEnumerator GameOver_End()
    {
        yield return new WaitForSeconds(5f);

        if (GameManager.gameManager == null)
        {
            Debug.LogError("Player_Main|GameOver_End|GameManager 为空");
            yield break;
        }
        GameManager.gameManager.Scence_SwitchClientRpc(GameManager.GameState.房间中);
    }

    // 各端显示结束幕布
    [ClientRpc]
    void GameOverClientRpc(int winnerIndex)
    {
        gameOver = true;

        if (uI_Curtain == null)
        {
            Debug.LogError("Player_Main|GameOverClientRpc|UI_Curtain 为空");
            return;
        }

        uI_Curtain.UI_curtain_Show($"游戏结束 胜利者：玩家{winnerIndex}");
    }

    // 撤离点播报，playerNumber为玩家编号
    void OnEvac_Tip(int playerNumber, float remain)
    {
        Level_Tip_Date($"玩家{playerNumber} 撤离中 剩余{remain:0}秒");
    }

    // 玩家被击败，各端提示
    public void Player_Death_Date(Player_Control control)
    {
        if (!IsServer) return;      //只有主机产生

        if (control == null)
        {
            Debug.LogError("Player_Main|Player_Death_Date|Player_Control 为空");
            return;
        }

        Level_Tip_Date($"玩家{control.Player_Number} 已阵亡");
    }

    // 玩家淘汰，各端提示并判定胜负
    public void Player_Out_Date(Player_Control control)
    {
        if (!IsServer) return;      //只有主机产生

        if (control == null)
        {
            Debug.LogError("Player_Main|Player_Out_Date|Player_Control 为空");
            return;
        }

        Level_Tip_Date($"玩家{control.Player_Number} 已淘汰");
        JudgeWinner_Date();
    }

    // 只剩一人则其获胜
    void JudgeWinner_Date()
    {
        if (!IsServer || gameOver || !isInit) return;

        Player_Control last = null;     //最后存活者

        for (int i = 0; i < oplayer_Controls.Count; i++)
        {
            Player_Control control = oplayer_Controls[i];
            if (control == null || control.Is_Out) continue;

            if (last != null) return;   //还有两人以上
            last = control;
        }

        if (last != null) GameOver_Start(last.OwnerClientId);
    }

    // 主机显示关卡文本并广播各端
    void Level_Tip_Date(string text)
    {
        if (!IsServer) return;      //只有主机产生

        if (uI_Time == null)
        {
            Debug.LogError("Player_Main|Level_Tip_Date|UI_Time 为空");
            return;
        }

        // 主机自己直接显示
        uI_Time.UI_Time_Show_Event(text, Level_Time);

        // 通知客机显示
        Level_Tip_ClientRpc(text);
    }

    // 客机显示关卡文本
    [ClientRpc]
    void Level_Tip_ClientRpc(string text)
    {
        if (IsServer) return;       //主机已显示

        if (uI_Time == null)
        {
            Debug.LogError("Player_Main|Level_Tip_ClientRpc|UI_Time 为空");
            return;
        }

        uI_Time.UI_Time_Show_Event(text, Level_Time);
    }

    // 关卡节点，主机广播各端
    void OnLevel_Node(int index)
    {
        if (!IsServer) return;      //只有主机广播

        Level_Node_ClientRpc(index);
    }

    // 客机跑关卡节点
    [ClientRpc]
    void Level_Node_ClientRpc(int index)
    {
        if (IsServer) return;       //主机已跑

        Player_level.Instance.Player_level_Event_Date(index);
    }

    //初始化游戏场景
    void GameScence_Init()
    {
        // 主机负责生成玩家
        if (!levelEntered)
        {
            levelEntered = true;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SpawnPlayers();

            // 主机关卡初始化
            Player_level.Instance.Player_Level_Init();

            // 播报与节点只有主机会产生，客机不注册
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                // 撤离点播报
                Player_level.Instance.level_EndCollider.Evac_Tip_event += OnEvac_Tip;

                // 关卡节点分发
                Player_level.Instance.Node_event += OnLevel_Node;
            }
        }
    }

    // 服务器为每个客户端生成玩家
    void SpawnPlayers()
    {
        // 按客户端编号排序，保证复活点分配稳定
        List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClients.Keys);
        clientIds.Sort();

        int i = 0;
        foreach (ulong clientId in clientIds)
        {
            int index = i++;    //玩家编号

            GameObject go = Instantiate(playerPrefab,
                startPos + Vector3.right * 2f * index, Quaternion.identity);

            Player_Control go_control = go.GetComponent<Player_Control>();
            if (go_control == null)
            {
                Debug.LogError("Player_Main|SpawnPlayers|未找到 Player_Control");
                continue;
            }

            go_control.respawnIndex = index;    //分配复活点
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
            go_control.Player_Control_Start_Own(oCamera, Input_Manage.Instance, oPlayer_Camera);   //只有本机玩家生效

            //重置位置
            Player_Respawn(go_control);
        }

        JudgePlayerClientRpc();
        InitScenceClientRpc();
    }

    // 按玩家编号重置到复活点
    public void Player_Respawn(Player_Control control)
    {
        if (control == null)
        {
            Debug.LogError("Player_Main|Player_Respawn|Player_Control 为空");
            return;
        }

        int index = control.RespawnIndex;   //玩家编号，各端一致

        Transform point = Respawn_Point(index);
        if (point == null)
        {
            Debug.LogError("Player_Main|Player_Respawn|复活点未绑定");
            return;
        }

        control.Player_Respawn_Date(point.position, point.rotation);
    }

    // 按编号取复活点，十位为楼层个位为点序号
    Transform Respawn_Point(int index)
    {
        switch (index / 10)
        {
            case 0:
                return Num1respawnPoints[index % Num1respawnPoints.Length];
            case 1:
                return Num2respawnPoints[UnityEngine.Random.Range(0, Num2respawnPoints.Length)];
            case 2:
                return Num3respawnPoints[UnityEngine.Random.Range(0, Num3respawnPoints.Length)];
        }
        return null;
    }

    // 近处落点触发本机压制
    public void Suppress_Check_Local(Vector3 hitPoint)
    {
        if (oPlayer_Control == null)
        {
            Debug.LogError("Player_Main|Suppress_Check_Local|本机玩家为空");
            return;
        }
        if (oPlayer_Control.Con_player_camera == null)
        {
            Debug.LogError("Player_Main|Suppress_Check_Local|本机相机为空");
            return;
        }

        oPlayer_Control.Con_player_camera.Camera_Suppress_Performance_Local(hitPoint);
    }

    // 各端收集玩家并寻找本地玩家
    [ClientRpc]
    void JudgePlayerClientRpc()
    {
        var netSingle = NetworkManager.Singleton;
        if (netSingle == null || netSingle.LocalClient == null)
        {
            Debug.LogError("Player_Main|JudgePlayerClientRpc|NetworkManager 为空");
            return;
        }

        oplayer_Controls.Clear();

        foreach (var kv in netSingle.SpawnManager.SpawnedObjects)
        {
            Player_Control control = kv.Value.GetComponent<Player_Control>();
            if (control == null) continue;

            oplayer_Controls.Add(control);

            if (kv.Value.OwnerClientId == netSingle.LocalClientId)
            {
                oPlayer_Control = control;
                hasLocalPlayer = true;
            }
        }

        if (!hasLocalPlayer) Debug.LogError("Player_Main|JudgePlayerClientRpc|未找到本地玩家");
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
        oPlayer_Control.Player_Control_Start_Own(oCamera, Input_Manage.Instance, oPlayer_Camera);

        uI_Debug.UI_Debug_Start(oPlayer_Control, oPlayer_Control.Con_input_Manage);

        // 本机弹药血量
        if (uI_Player == null)
        {
            Debug.LogError("Player_Main|InitScenceClientRpc|UI_Player 为空");
        }
        else uI_Player.UI_Player_Init();

        // UI幕布
        if (uI_Curtain == null)
        {
            Debug.LogError("Player_Main|InitScenceClientRpc|UI_Curtain 为空");
        }
        else uI_Curtain.UI_curtain_Init();

        // 关卡时间UI
        if (uI_Time == null)
        {
            Debug.LogError("Player_Main|InitScenceClientRpc|UI_Time 为空");
        }
        else uI_Time.UI_Time_Init();

        isInit = true;

        Debug.Log($"Player_Main|InitScenceClientRpc|完成初始化{oPlayer_Control.transform.position}");
    }
}
