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

    [Header("测试")]
    public bool testStop;    //关闭关卡进程

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
    readonly float[] nodeGasY = { -100f, 45f, 0f, 95f, 120f, 165f, 0f };
    //毒气粒子1-4
    public ParticleSystem[] gasParticle = new ParticleSystem[4];
    float gasHeight;                        //当前毒气高度
    public float Gas_Height => gasHeight;   //对外读取
    int gasShowIndex;                       //毒气粒子进度

    //毒气释放节点序号，与 nodeGasY 对应
    readonly int[] gasNode = { 1, 3, 4, 5 };
    public float alarmWarn = 30f;           //警报提前秒数
    public AudioSource alarmSource;         //警报音源，循环
    public AudioSource[] gasSources;        //每层毒气音源
    public AudioClip alarmClip;             //毒气警报
    public AudioClip releaseClip;           //毒气释放声
    int alarmIndex;                         //毒气警报进度

    public AudioSource cablewaySource;      //索道提示音源，循环
    public AudioClip cablewayClip;          //索道提示音
    public float cablewayWarn = 10f;        //索道提示提前秒数
    bool cablewayTip;                       //索道提示已响

    public AudioSource battleSource;        //决战音源，循环
    public AudioClip battleClip;            //决战音乐
    public float battleTime = 510f;         //决战音乐时刻，8分30秒
    bool battleOn;                          //决战音乐已播

    //复活楼层，3m10s二楼 5m三楼 5m30s四楼，一层毒气不管
    public int Respawn_Floor => gameTime >= 330f ? 3 : gameTime >= 300f ? 2 : gameTime >= 190f ? 1 : 0;

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
        alarmIndex = 0;
        pointIndex = 0;

        // 警报循环音
        if (alarmSource != null)
        {
            alarmSource.loop = true;
            alarmSource.Stop();
        }

        // 索道提示重置
        cablewayTip = false;
        if (cablewaySource != null) cablewaySource.Stop();

        // 决战音乐重置
        battleOn = false;
        if (battleSource != null) battleSource.Stop();

        for (int i = 0; i < gasParticle.Length; i++)
        {
            if (gasParticle[i] != null) gasParticle[i].Stop();
        }
    }

    // 3m10s显示索道
    void Time3m10s_Show()
    {
        // 索道出现，停提示音
        if (cablewaySource != null && cablewaySource.isPlaying) cablewaySource.Stop();

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
        if (testStop) return;   //测试关闭进程，时间停在当前

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

    // 关卡表现，各端每物理帧跑，time为当前关卡时间
    public void Player_level_FixUpdate_Performance(float time)
    {
        // 电梯运行
        if (level_elevator != null) level_elevator.Level_elevator_FixUpdate();

        // 毒气警报，释放前 alarmWarn 秒开始响
        if (alarmIndex < gasNode.Length && time >= nodeTime[gasNode[alarmIndex]] - alarmWarn)
        {
            alarmIndex++;

            if (alarmSource != null && alarmClip != null)
            {
                alarmSource.clip = alarmClip;
                alarmSource.loop = true;    //释放时只关循环，不硬切
                alarmSource.Play();
            }
        }

        // 索道刷新提示，提前 cablewayWarn 秒循环
        if (!cablewayTip && time >= nodeTime[2] - cablewayWarn)
        {
            cablewayTip = true;

            if (cablewaySource != null && cablewayClip != null)
            {
                cablewaySource.clip = cablewayClip;
                cablewaySource.loop = true;
                cablewaySource.Play();
            }
        }

        // 决战音乐，8分30秒起循环
        if (!battleOn && time >= battleTime)
        {
            battleOn = true;

            if (battleSource != null && battleClip != null)
            {
                battleSource.clip = battleClip;
                battleSource.loop = true;
                battleSource.Play();
            }
        }
    }

    // 第 index 层毒气音源
    AudioSource Gas_Source(int index)
    {
        if (gasSources == null || index < 0 || index >= gasSources.Length) return null;
        return gasSources[index];
    }

    // 节点表现，index为节点序号，各端按主机节点跑
    public void Player_level_Event_Date(int index)
    {
        if (index < 0 || index >= nodeTime.Length)
        {
            Debug.LogError("Player_level|Player_level_Event_Date|节点序号越界");
            return;
        }

        // 毒气逐层抬升，只在毒气节点释放
        if (Array.IndexOf(gasNode, index) >= 0)
        {
            gasHeight = nodeGasY[index];

            // 释放声，警报播完当前一轮再停
            if (alarmSource != null) alarmSource.loop = false;
            AudioSource source = Gas_Source(gasShowIndex);
            if (source != null && releaseClip != null) source.PlayOneShot(releaseClip);

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
