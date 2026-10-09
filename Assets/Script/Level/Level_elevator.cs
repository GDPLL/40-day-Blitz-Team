using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 双层电梯，由 Player_level 驱动
public class Level_elevator : MonoBehaviour
{
    // 部件
    public Transform floor;         // 电梯地板
    public Rigidbody floorBody;     // 地板刚体，带人
    public Transform door1;         // 一楼门
    public Transform door2;         // 二楼门

    // 行程
    float doorUp = 5f;       // 开门上移
    float floorUp = 48.2f;     // 地板升高
    float floorDown = 0.3f;  // 先行下沉托人
    float moveSpeed = 5f;    // 移动速度

    // 初始位置
    Vector3 floorBase;      // 地板初始位置
    Vector3 door1Base;      // 一楼门初始位置
    Vector3 door2Base;      // 二楼门初始位置
    Vector3 floorNow;       // 地板当前位置
    int stage;              // 运行阶段

    // 初始化，重置所有位置
    public void Level_elevator_Init()
    {
        if (floor == null || floorBody == null || door1 == null || door2 == null)
        {
            Debug.LogError("Level_elevator|Level_elevator_Init|部件未绑定");
            return;
        }

        // 记录部件初始位置
        floorBase = floor.position;
        door1Base = door1.position;
        door2Base = door2.position;

        // 重置所有位置，地板由刚体驱动
        floorNow = floorBase;
        floorBody.isKinematic = true;
        floorBody.position = floorBase;
        floor.position = floorBase;
        door1.position = door1Base;
        door2.position = door2Base;

        stage = 0;      // 开始开一楼门
    }

    // 启动，关一楼门上行开二楼门
    public void Level_elevator_Start_Date()
    {
        if (stage != 1) return;     //未就绪不启动

        stage = 2;      // 开始关一楼门
    }

    // 回一楼，关二楼门下去重开一楼门
    public void Level_elevator_Back_Date()
    {
        if (stage != 6) return;     //上行结束才回

        stage = 7;      // 开始关二楼门
    }

    // 每物理帧驱动电梯运行
    public void Level_elevator_FixUpdate()
    {
        if (floor == null || floorBody == null || door1 == null || door2 == null)
        {
            Debug.LogError("Level_elevator|Level_elevator_FixUpdate|部件未绑定");
            return;
        }
        if (stage == 1) return;     // 待机，等启动

        float step = moveSpeed * Time.fixedDeltaTime;    // 匀速步长
        Vector3 door1Open = door1Base + Vector3.up * doorUp;
        Vector3 door2Open = door2Base + Vector3.up * doorUp;
        Vector3 floorTop = floorBase + Vector3.up * floorUp;
        Vector3 floorDip = floorBase - Vector3.up * floorDown;

        switch (stage)
        {
            case 0:     // 一楼门升起
                door1.position = Vector3.MoveTowards(door1.position, door1Open, step);
                if (door1.position == door1Open) stage = 1;
                break;
            case 2:     // 关一楼门
                door1.position = Vector3.MoveTowards(door1.position, door1Base, step);
                if (door1.position == door1Base) stage = 3;
                break;
            case 3:     // 地板下沉托人
                floorNow = Vector3.MoveTowards(floorNow, floorDip, step);
                floorBody.MovePosition(floorNow);
                if (floorNow == floorDip) stage = 4;
                break;
            case 4:     // 地板上行
                floorNow = Vector3.MoveTowards(floorNow, floorTop, step);
                floorBody.MovePosition(floorNow);
                if (floorNow == floorTop) stage = 5;
                break;
            case 5:     // 开二楼门
                door2.position = Vector3.MoveTowards(door2.position, door2Open, step);
                if (door2.position == door2Open) stage = 6;
                break;
            case 7:     // 关二楼门
                door2.position = Vector3.MoveTowards(door2.position, door2Base, step);
                if (door2.position == door2Base) stage = 8;
                break;
            case 8:     // 地板下行
                floorNow = Vector3.MoveTowards(floorNow, floorBase, step);
                floorBody.MovePosition(floorNow);
                if (floorNow == floorBase) stage = 9;
                break;
            case 9:     // 开一楼门
                door1.position = Vector3.MoveTowards(door1.position, door1Open, step);
                if (door1.position == door1Open) stage = 10;
                break;
            default: return;    // 运行结束
        }
    }
}
