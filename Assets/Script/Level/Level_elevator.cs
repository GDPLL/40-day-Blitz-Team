using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 双层电梯，由 Player_level 驱动
public class Level_elevator : MonoBehaviour
{
    // 部件
    public Transform floor;         // 电梯地板
    public Rigidbody floorBody;     // 地板刚体，带人
    public Transform door1Left;     // 一楼左门
    public Transform door1Right;    // 一楼右门
    public Transform door2Left;     // 二楼左门
    public Transform door2Right;    // 二楼右门
    public Transform cabLeft;       // 电梯间左门
    public Transform cabRight;      // 电梯间右门

    // 音效
    public AudioSource audioSource;     // 一次性音源
    public AudioSource riseSource;      // 运行音源，循环
    public AudioClip startClip;         // 启动声
    public AudioClip arriveClip;        // 到达铃声
    public AudioClip riseClip;          // 运行声

    // 行程
    public float doorSlide = 1.5f;  // 单扇门左右开合距离
    float floorUp = 48.2f;     // 地板升高
    float floorDown = 0.3f;  // 先行下沉托人
    float moveSpeed = 5f;    // 移动速度

    // 初始位置
    Vector3 floorBase;      // 地板初始位置
    Vector3 door1LBase;     // 一楼左门初始位置
    Vector3 door1RBase;     // 一楼右门初始位置
    Vector3 door2LBase;     // 二楼左门初始位置
    Vector3 door2RBase;     // 二楼右门初始位置
    Vector3 cabLBase;       // 电梯间左门初始位置
    Vector3 cabRBase;       // 电梯间右门初始位置
    Vector3 floorNow;       // 地板当前位置
    int stage;              // 运行阶段
    bool startup;           // 开局下行中

    // 初始化，重置所有位置
    public void Level_elevator_Init()
    {
        if (floor == null || floorBody == null ||
            door1Left == null || door1Right == null ||
            door2Left == null || door2Right == null)
        {
            Debug.LogError("Level_elevator|Level_elevator_Init|部件未绑定");
            return;
        }

        // 记录部件初始位置
        floorBase = floor.position;
        door1LBase = door1Left.position;
        door1RBase = door1Right.position;
        door2LBase = door2Left.position;
        door2RBase = door2Right.position;

        // 电梯间门取局部坐标，随轿厢移动
        if (cabLeft != null) cabLBase = cabLeft.localPosition;
        if (cabRight != null) cabRBase = cabRight.localPosition;

        // 运行循环音
        if (riseSource != null)
        {
            riseSource.clip = riseClip;
            riseSource.loop = true;
            riseSource.Stop();
        }

        // 开局停在二楼，先下行到一楼再待机
        floorBody.isKinematic = true;
        floorNow = floorBase + Vector3.up * floorUp;
        floorBody.position = floorNow;
        floor.position = floorNow;
        door1Left.position = door1LBase;
        door1Right.position = door1RBase;
        door2Left.position = door2LBase + Vector3.left * doorSlide;
        door2Right.position = door2RBase + Vector3.right * doorSlide;

        // 电梯间门与二楼门同为打开
        if (cabLeft != null) cabLeft.localPosition = cabLBase + Vector3.left * doorSlide;
        if (cabRight != null) cabRight.localPosition = cabRBase + Vector3.right * doorSlide;

        startup = true;
        stage = 7;      // 关二楼门下行开一楼门
    }

    // 启动，关一楼门上行开二楼门
    public void Level_elevator_Start_Date()
    {
        if (stage != 1) return;     //未就绪不启动

        stage = 2;      // 开始关一楼门
        Play_Once(startClip);       // 启动声
    }

    // 回一楼，关二楼门下去重开一楼门
    public void Level_elevator_Back_Date()
    {
        if (stage != 6) return;     //上行结束才回

        stage = 7;      // 开始关二楼门
        Play_Once(startClip);       // 启动声
    }

    // 每物理帧驱动电梯运行
    public void Level_elevator_FixUpdate()
    {
        if (floor == null || floorBody == null ||
            door1Left == null || door1Right == null ||
            door2Left == null || door2Right == null)
        {
            Debug.LogError("Level_elevator|Level_elevator_FixUpdate|部件未绑定");
            return;
        }
        if (stage == 1) return;     // 待机，等启动

        float step = moveSpeed * Time.fixedDeltaTime;    // 匀速步长
        Vector3 floorTop = floorBase + Vector3.up * floorUp;
        Vector3 floorDip = floorBase - Vector3.up * floorDown;

        // 地板运行声
        bool moving = stage == 3 || stage == 4 || stage == 8;
        if (riseSource != null)
        {
            if (moving && !riseSource.isPlaying) riseSource.Play();
            else if (!moving && riseSource.isPlaying) riseSource.Stop();
        }

        switch (stage)
        {
            case 0:     // 一楼门左右打开
                if (Door_Date(door1Left, door1LBase, door1Right, door1RBase, true, step)) stage = 1;
                break;
            case 2:     // 关一楼门
                if (Door_Date(door1Left, door1LBase, door1Right, door1RBase, false, step)) stage = 3;
                break;
            case 3:     // 地板下沉托人
                floorNow = Vector3.MoveTowards(floorNow, floorDip, step);
                floorBody.MovePosition(floorNow);
                if (floorNow == floorDip) stage = 4;
                break;
            case 4:     // 地板上行
                floorNow = Vector3.MoveTowards(floorNow, floorTop, step);
                floorBody.MovePosition(floorNow);
                if (floorNow != floorTop) break;

                stage = 5;
                Play_Once(arriveClip);      // 到达铃声
                break;
            case 5:     // 开二楼门
                if (Door_Date(door2Left, door2LBase, door2Right, door2RBase, true, step)) stage = 6;
                break;
            case 7:     // 关二楼门
                if (Door_Date(door2Left, door2LBase, door2Right, door2RBase, false, step)) stage = 8;
                break;
            case 8:     // 地板下行
                floorNow = Vector3.MoveTowards(floorNow, floorBase, step);
                floorBody.MovePosition(floorNow);
                if (floorNow != floorBase) break;

                stage = 9;
                Play_Once(arriveClip);      // 到达铃声
                break;
            case 9:     // 开一楼门
                if (!Door_Date(door1Left, door1LBase, door1Right, door1RBase, true, step)) break;

                // 开局下行回到待机，其余回到结束
                stage = startup ? 1 : 10;
                startup = false;
                break;
            default: return;    // 运行结束
        }
    }

    // 左右门开合，场景门用世界坐标，电梯间门用局部坐标，全部到位返回true
    bool Door_Date(Transform left, Vector3 lBase, Transform right, Vector3 rBase, bool open, float step)
    {
        Vector3 lTarget = open ? lBase + Vector3.left * doorSlide : lBase;
        Vector3 rTarget = open ? rBase + Vector3.right * doorSlide : rBase;

        left.position = Vector3.MoveTowards(left.position, lTarget, step);
        right.position = Vector3.MoveTowards(right.position, rTarget, step);

        bool sceneDone = left.position == lTarget && right.position == rTarget;

        // 电梯间门同步开合
        bool cabDone = Cab_Door_Date(open, step);

        return sceneDone && cabDone;
    }

    // 电梯间门开合，未绑定视为到位
    bool Cab_Door_Date(bool open, float step)
    {
        if (cabLeft == null || cabRight == null) return true;

        Vector3 lTarget = open ? cabLBase + Vector3.left * doorSlide : cabLBase;
        Vector3 rTarget = open ? cabRBase + Vector3.right * doorSlide : cabRBase;

        cabLeft.localPosition = Vector3.MoveTowards(cabLeft.localPosition, lTarget, step);
        cabRight.localPosition = Vector3.MoveTowards(cabRight.localPosition, rTarget, step);

        return cabLeft.localPosition == lTarget && cabRight.localPosition == rTarget;
    }

    // 播一次音效
    void Play_Once(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;

        audioSource.PlayOneShot(clip);
    }
}
