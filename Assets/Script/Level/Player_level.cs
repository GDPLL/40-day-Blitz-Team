using System;
using UnityEngine;
using Unity.Netcode;

//游戏进行时关卡控制器
public class Player_level : MonoBehaviour
{
    public static Player_level Instance;    //全局单例


    public Level_EndCollider level_EndCollider; //撤离点     
    public Level_elevator level_elevator;    //电梯    
    public GameObject time3mObject;          //3m10s索道对象
    public int gasDamage = 5;               //毒气每秒伤害
    public float gameTime;    //关卡已进行时间

    //时间节点事件
    public event Action Time30s_event;      //30秒
    public event Action Time3m_event;       //3分钟
    public event Action Time3m10s_event;    //3分10秒
    public event Action Time4m_event;       //4分钟
    public event Action Time5m_event;       //5分钟
    public event Action Time5m30s_event;    //5分30秒
    public event Action Time9m_event;       //9分钟

    public event Action<int> Node_event;    //节点分发，主机广播用

    public bool Level_end => level_EndCollider.Is_End;  //结束符号

    //节点时刻 30s 3m 3m10s 4m 5m 5m30s 9m
    readonly float[] nodeTime = { 30f, 180f, 190f, 240f, 300f, 330f, 540f };
    //节点毒气高度，0为不变
    readonly float[] nodeGasY = { 0f, 45f, 0f, 95f, 120f, 165f, 0f };
    //毒气粒子1-4
    public ParticleSystem[] gasParticle = new ParticleSystem[4];
    float gasHeight;                        //当前毒气高度
    public float Gas_Height => gasHeight;   //对外读取
    int gasShowIndex;                       //毒气粒子进度

    //复活楼层，3m10s二楼 4m三楼
    public int Respawn_Floor => gameTime >= 240f ? 2 : gameTime >= 190f ? 1 : 0;

    //9分钟关闭复活
    public bool Respawn_Off => gameTime >= 540f;

    //胜利者编号
    public ulong WinnerId => level_EndCollider.Winner != null
        ? level_EndCollider.Winner.OwnerClientId : 0;

    int pointIndex;    //当前节点序号

    // 初始化单例
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }
    }

    public void Player_Level_Init()
    {
        level_EndCollider.Level_EndCollider_Init();

        if (level_elevator == null)
        {
            Debug.LogError("Player_level|Player_Level_Init|level_elevator 为空");
            return;
        }
        level_elevator.Level_elevator_Init();

        // 30秒启动电梯，3分钟回一楼
        Time30s_event += level_elevator.Level_elevator_Start_Date;
        Time3m_event += level_elevator.Level_elevator_Back_Date;

        if (time3mObject == null)
        {
            Debug.LogError("Player_level|Player_Level_Init|time3mObject 为空");
            return;
        }

        // 初始隐藏，3m10s显示
        time3mObject.SetActive(false);
        Time3m10s_event += Time3m10s_Show;

        // 毒气层与粒子重置
        gasHeight = 0f;
        gasShowIndex = 0;
        pointIndex = 0;

        for (int i = 0; i < gasParticle.Length; i++)
        {
            if (gasParticle[i] != null) gasParticle[i].Stop();
        }
    }

    // 3m10s显示索道
    void Time3m10s_Show()
    {
        if (time3mObject == null)
        {
            Debug.LogError("Player_level|Time3m10s_Show|time3mObject 为空");
            return;
        }

        time3mObject.SetActive(true);
    }

    // 关卡计时，只有主机检测分发
    public void Player_level_FixUpdate()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;   //只有主机

        gameTime += Time.fixedDeltaTime;

        // 撤离点计时
        level_EndCollider.Level_EndCollider_FixUpdate();

        // 到点跑节点并广播，节点事件触发在下方
        while (pointIndex < nodeTime.Length && gameTime >= nodeTime[pointIndex])
        {
            Player_level_Event_Date(pointIndex);    //主机自己跑
            Node_event?.Invoke(pointIndex);         //客机由 Player_Main 广播
            pointIndex++;
        }
    }

    // 关卡表现，各端每物理帧跑
    public void Player_level_FixUpdate_Performance()
    {
        // 电梯运行
        if (level_elevator != null) level_elevator.Level_elevator_FixUpdate();
    }

    // 节点表现，index为节点序号，各端按主机节点跑
    public void Player_level_Event_Date(int index)
    {
        if (index < 0 || index >= nodeTime.Length)
        {
            Debug.LogError("Player_level|Player_level_Event_Date|节点序号越界");
            return;
        }

        // 毒气逐层抬升，只放当前层
        if (nodeGasY[index] > 0f)
        {
            gasHeight = nodeGasY[index];

            if (gasShowIndex > 0 && gasParticle[gasShowIndex - 1] != null)
                gasParticle[gasShowIndex - 1].Stop();

            if (gasShowIndex < gasParticle.Length && gasParticle[gasShowIndex] != null)
                gasParticle[gasShowIndex].Play();

            gasShowIndex++;
        }

        switch (index)
        {
            case 0: Time30s_event?.Invoke(); break;
            case 1: Time3m_event?.Invoke(); break;
            case 2: Time3m10s_event?.Invoke(); break;
            case 3: Time4m_event?.Invoke(); break;
            case 4: Time5m_event?.Invoke(); break;
            case 5: Time5m30s_event?.Invoke(); break;
            case 6: Time9m_event?.Invoke(); break;
        }
    }
}
