using UnityEngine;

// 角色公共逻辑，转向与速度
public class Character_Move : MonoBehaviour
{
    // 速度参数
    public float turnSpeed = 360f;   // 转向速度
    public float walkSpeed = 3f;     // 走路速度
    public float runSpeed = 6f;      // 跑步速度
    public float squatSpeed = 1f;    // 蹲下速度

    protected float currentSpeed;    // 当前速度

    // 平滑转向水平方向
    protected void SmoothRotate(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(dir.normalized),
            turnSpeed * Time.deltaTime);
    }
}
