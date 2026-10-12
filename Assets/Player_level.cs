using System;
using UnityEngine;

//游戏进行时关卡控制器
public class Player_level : MonoBehaviour
{
    public static Player_level Instance;    //全局单例


    public Level_EndCollider level_EndCollider; //撤离点     
    public Level_elevator level_elevator;    //电梯    
    public GameObject time3mObject;          //3m10s索道对象
    public int gasDamage = 10;               //毒气每秒伤害
    public float gameTime;    //关卡已进行时间

    //时间节点事件
    public event Action Time30s_event;      //30秒
    public event Action Time3m_event;       //3分钟
    public event Action Time3m10s_event;    //3分10秒
    public event Action Time4m_event;       //4分钟
    public event Action Time5m_event;       //5分钟
    public event Action Time5m30s_event;    //5分30秒
    public event Action Time9m_event;       //9分钟

    public bool Level_end => level_EndCollider.Is_End;  //结束符号

    //毒气层高度 45 95 120 165
    readonly float[] gasY = { 45f, 95f, 120f, 165f };
    //毒气启用时刻 3m 4m 5m 5m30s
    readonly float[] gasTime = { 180f, 240f, 300f, 330f };
    //毒气粒子1-4
    public ParticleSystem[] gasParticle = new ParticleSystem[4];
    float gasHeight;                        //当前毒气高度
    public float Gas_Height => gasHeight;   //对外读取
    int gasIndex;                           //毒气层进度

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
        gasIndex = 0;

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

    // 关卡计时，到点触发对应节点
    public void Player_level_FixUpdate()
    {
        gameTime += Time.fixedDeltaTime;

        // 撤离点计时
        level_EndCollider.Level_EndCollider_FixUpdate();
        // 电梯运行
        if (level_elevator != null) level_elevator.Level_elevator_FixUpdate();

        // 毒气逐层抬升，只放当前层
        while (gasIndex < gasTime.Length && gameTime >= gasTime[gasIndex])
        {
            // 停掉上一层
            if (gasIndex > 0 && gasParticle[gasIndex - 1] != null)
                gasParticle[gasIndex - 1].Stop();

            gasHeight = gasY[gasIndex];

            if (gasIndex < gasParticle.Length && gasParticle[gasIndex] != null)
                gasParticle[gasIndex].Play();

            gasIndex++;
        }

        switch (pointIndex)
        {
            case 0: if (gameTime < 30f) return; Time30s_event?.Invoke(); break;
            case 1: if (gameTime < 180f) return; Time3m_event?.Invoke(); break;
            case 2: if (gameTime < 190f) return; Time3m10s_event?.Invoke(); break;
            case 3: if (gameTime < 240f) return; Time4m_event?.Invoke(); break;
            case 4: if (gameTime < 300f) return; Time5m_event?.Invoke(); break;
            case 5: if (gameTime < 330f) return; Time5m30s_event?.Invoke(); break;
            case 6: if (gameTime < 540f) return; Time9m_event?.Invoke(); break;
            default: return;    //节点已走完
        }

        pointIndex++;



    }
}
