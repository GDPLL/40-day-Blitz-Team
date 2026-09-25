using UnityEngine;

// 角色公共逻辑：转向/速度变量（玩家与敌人共用）
// 移动机制不同：玩家用 Rigidbody 力、敌人用 NavMeshAgent 寻路，各自在子类实现
// 速度模式判断由子类负责，父类只声明变量；SmoothRotate 由 FixedUpdate 驱动
public class Character_Move : MonoBehaviour
{
    public float turnSpeed = 360f;   // 转向速度（度/秒）
    public float walkSpeed = 3f;     // 走路速度
    public float runSpeed = 6f;      // 跑步速度
    public float squatSpeed = 1f;    //蹲下速度

    protected float currentSpeed;    // 当前速度（由子类速度模式决定）

    // 平滑转向某个水平方向（由 FixedUpdate 驱动）
    protected void SmoothRotate(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(dir.normalized),
            turnSpeed * Time.deltaTime);
    }
}
