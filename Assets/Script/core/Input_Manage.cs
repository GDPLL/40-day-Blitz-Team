using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 全局输入管理，跨场景常驻
public class Input_Manage : MonoBehaviour
{
    public static Input_Manage Instance { get; private set; }   // 全局实例

    // 输入事件
    public event Action<Vector2> Move_event;     // 移动
    public event Action<bool> JumpDown_event;    // 跳跃
    public event Action<bool> Mouse1_event;      // 左键
    public event Action<bool> Mouse2_event;      // 右键
    public event Action<bool> MouseHeld_event;   // 鼠标按住
    public event Action<bool> Run_event;         // 奔跑
    public event Action ReloadHeld_event;        // 换弹
    public event Action<bool> Squat_event;       // 蹲下
    public event Action<bool> Debug_event;       // 调试面板

    // 建立全局单例
    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Input_Manage|Awake|已存在实例，销毁重复对象");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 每帧读取并分发输入
    void Update()
    {
        Input_get();

        //移动输入
        if (WASDHeld || MoveAxis != Vector2.zero)
        {
            Move(MoveAxis);
        }
        //跳跃
        Jump(JumpDownHeld);
        //左键
        Mouse1(Mouse1Held);
        //右键
        Mouse2(Mouse2Held);
        //鼠标按住
        MouseDown(MouseHeld);
        //奔跑
        Run(RunHeld);
        //换弹
        if (ReloadHeld)
        {
            Reload();
        }
        //蹲下
        Squat(SquatHeld);
        //调试呼出
        DebugE(DebugKey);
    }

    // 事件分发
    public void Move(Vector2 vector2)
    {
        Move_event?.Invoke(vector2);
    }
    public void Jump(bool on)
    {
        JumpDown_event?.Invoke(on);
    }
    public void Mouse1(bool on)
    {
        Mouse1_event?.Invoke(on);
    }
    public void Mouse2(bool on)
    {
        Mouse2_event?.Invoke(on);
    }
    public void MouseDown(bool on)
    {
        MouseHeld_event?.Invoke(on);
    }
    public void Run(bool on)
    {
        Run_event?.Invoke(on);
    }
    public void Reload()
    {
        ReloadHeld_event?.Invoke();
    }
    public void Squat(bool on)
    {
        Squat_event?.Invoke(on);
    }
    public void DebugE(bool on)
    {
        Debug_event?.Invoke(on);
    }

    //输入检测状态
    public Vector2 MoveAxis;        //移动输入轴
    public bool JumpDownHeld;       //跳跃空格
    public bool Mouse1Held;         //左键输入
    public bool Mouse2Held;         //右键输入
    public bool MouseHeld => Mouse1Held || Mouse2Held;
    public bool WASDHeld;           //移动输入
    public bool RunHeld;            //奔跑输入
    public bool ReloadHeld;         //换弹输入
    public bool SquatHeld;          //蹲下输入
    public bool DebugKey;           // 调试输入

    // 读取输入状态
    void Input_get()
    {
        MoveAxis = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        JumpDownHeld = Input.GetKey(KeyCode.Space);     //跳跃空格
        Mouse1Held = Input.GetMouseButton(0);        //左键输入
        Mouse2Held = Input.GetMouseButton(1);        //右键输入
        WASDHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);        //移动输入
        RunHeld = Input.GetKey(KeyCode.LeftShift) && !Mouse2Held;       //奔跑输入
        ReloadHeld = Input.GetKey(KeyCode.R);            //换弹输入 
        SquatHeld = Input.GetKey(KeyCode.LeftControl);       //蹲下输入
        DebugKey = Input.GetKey(KeyCode.BackQuote);         //调试输入按钮
    }



}



