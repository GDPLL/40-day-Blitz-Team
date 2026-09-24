using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Player_Input : MonoBehaviour
{
    
    public Vector2 MoveAxis;    //移动输入轴
    public bool JumpDownHeld;    //跳跃空格
    public bool Mouse1Held;   //左键输入
    public bool Mouse2Held;   //右键输入
    public bool MouseHeld => Mouse1Held || Mouse2Held;
    public bool WASDHeld;    //移动输入
    public bool RunHeld;      //奔跑输入
    public bool ReloadHeld;       //换弹输入
    public bool SquatHeld;        //蹲下输入

    
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        MoveAxis   = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        JumpDownHeld = Input.GetKey(KeyCode.Space);     //跳跃空格
        Mouse1Held = Input.GetMouseButton(0);        //左键输入
        Mouse2Held = Input.GetMouseButton(1);        //右键输入
        WASDHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);        //移动输入
        RunHeld = Input.GetKey(KeyCode.LeftShift) && !Mouse2Held;       //奔跑输入
        ReloadHeld = Input.GetKey(KeyCode.R);            //换弹输入 
        SquatHeld = Input.GetKey(KeyCode.LeftControl);       //蹲下输入
    }
}
    
    

