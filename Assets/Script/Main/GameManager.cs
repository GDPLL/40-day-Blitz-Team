using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System;
using System.Collections.Generic;


//游戏进程管理
public class GameManager : NetworkBehaviour
{
    // 游戏状态
    public enum GameState { 房间中, 关卡, 结算 }

    public static GameManager gameManager { get; private set; }
    public static GameState State { get; private set; } = GameState.房间中;

    [Header("关卡设置")]
    public const string levelSceneName = "SampleScene";   // 关卡场景名
    public const string StartScenceName = "Start";

    public NetworkManager networkManager;// 网络组件


    public event Action ScenceChange;   //场景切换事件 
    public event Action StartAction;    //菜单进入事件
    public event Action GameAction;     //游戏进入事件

    // 跨场景保留
    void Awake()
    {
        // 已有单例，重复对象整个销毁，防撞 GlobalObjectIdHash
        if (gameManager != null && gameManager != this)
        {
            Destroy(gameObject);
            return;
        }

        gameManager = this;
        DontDestroyOnLoad(gameObject);

        if (networkManager == null) Debug.LogError("GameManager|Awake|networkManager==null");

        ScenceChange += Scence_OnChange;

        Debug.Log("GameManager|Awake|完成初始化");
    }

    // 销毁时清空单例，防悬空引用
    public override void OnDestroy()
    {
        base.OnDestroy();

        if (gameManager != this) return;

        ScenceChange -= Scence_OnChange;
        gameManager = null;

        Debug.Log("GameManager|OnDestroy|单例已释放");
    }



    //切换场景事件分发
    void Scence_OnChange()
    {
        Scence_Judge();
        Event_invoke();
    }
    void Event_invoke()
    {
        switch (State)
        {
            case GameState.房间中:
                StartAction?.Invoke();
                break;
            case GameState.关卡:
                GameAction?.Invoke();
                break;
        }
    }

    //场景判断
    void Scence_Judge()
    {
        string Scence = SceneManager.GetActiveScene().name;
        switch (Scence)
        {
            case StartScenceName:
                State = GameState.房间中;
                break;
            case levelSceneName:
                State = GameState.关卡;
                break;
        }

    }

    //外部场景切换方法
    [ClientRpc]
    public void Scence_SwitchClientRpc(GameState gameState)
    {
        // 先挂回调再切场景
        networkManager.SceneManager.OnLoadEventCompleted -= Scence_OnLoadEventCompleted;
        networkManager.SceneManager.OnLoadEventCompleted += Scence_OnLoadEventCompleted;

        //由主机负责切换
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            // 清掉当前场景玩家
            Clear_Scence_Player_Date();

            switch (gameState)
            {
                case GameState.房间中:
                    networkManager.SceneManager.LoadScene(StartScenceName, LoadSceneMode.Single);
                    break;
                case GameState.关卡:
                    networkManager.SceneManager.LoadScene(levelSceneName, LoadSceneMode.Single);
                    break;
            }
        }

    }

    // 清除当前场景玩家对象，主机执行
    void Clear_Scence_Player_Date()
    {
        if (networkManager == null || networkManager.SpawnManager == null)
        {
            Debug.LogError("GameManager|Clear_Scence_Player_Date|SpawnManager 为空");
            return;
        }

        // 先拷贝再销毁，避免遍历中改集合
        List<NetworkObject> players = new List<NetworkObject>();
        foreach (NetworkObject player in networkManager.SpawnManager.SpawnedObjectsList)
            players.Add(player);

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) continue;
            if(players[i].GetComponent<Player_Control>()==null) continue;
            players[i].Despawn(true);      //连同对象一起销毁
        }
    }

    //场景加载完成后分发事件
    void Scence_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode
    , List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        networkManager.SceneManager.OnLoadEventCompleted -= Scence_OnLoadEventCompleted;
        ScenceChange?.Invoke();
    }

    // 结束游戏
    public void EndGame()
    {
        State = GameState.结算;
    }
}
