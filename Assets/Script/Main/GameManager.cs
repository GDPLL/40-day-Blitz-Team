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
        if (gameManager != null && gameManager != this)
        {
            Destroy(this);
            return;
        }

        gameManager = this;
        DontDestroyOnLoad(gameObject);

        if (networkManager == null) Debug.LogError("GameManager|Awake|networkManager==null");

        ScenceChange += Scence_OnChange;

        Debug.Log("GameManager|Awake|完成初始化");
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
