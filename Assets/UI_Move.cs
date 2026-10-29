using UnityEngine;

// Canvas跟随摄像机，姿态驱动缩放
public class UI_Move : MonoBehaviour
{
    public Transform target;            // 跟随目标，空则取主相机
    public Vector3 positionOffset;      // 相对摄像机的位置偏移
    public Vector3 rotationOffset;      // 朝向摄像机后的旋转偏移

    [Header("平滑")]
    public float smoothTime = 0.08f;    // 位置与缩放平滑时长
    public float turnSpeed = 12f;       // 旋转插值速度

    [Header("姿态缩放")]
    public Transform scaleTarget;       // 缩放对象，空则取自身
    public float walkScale = 0.5f;      // 移动缩放
    public float runScale = 0.4f;       // 奔跑缩放
    public float fireHold = 0.25f;      // 开火恢复原位时长

    Player_Control control;             // 本机玩家
    float fireTimer;                    // 开火保持计时
    Vector3 posVelocity;                // 位置平滑速度
    Vector3 scaleVelocity;              // 缩放平滑速度
    Vector3 baseScale = Vector3.one;    // 原始缩放

    void Awake()
    {
        if (scaleTarget == null) scaleTarget = transform;
        baseScale = scaleTarget.localScale;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        Follow_Camera(dt);
        Pose_Scale(dt);
    }

    // 跟随摄像机位置与朝向
    void Follow_Camera(float dt)
    {
        if (target == null) target = Camera.main != null ? Camera.main.transform : null;
        if (target == null) return;

        // 位置取摄像机加偏移
        Vector3 want = target.position + target.TransformDirection(positionOffset);
        transform.position = Vector3.SmoothDamp(transform.position, want, ref posVelocity, smoothTime);

        // 朝向摄像机，再叠加旋转偏移
        Vector3 look = target.position - transform.position;
        if (look.sqrMagnitude <= 0.0001f) look = target.forward;

        Quaternion wantRot = Quaternion.LookRotation(look, target.up) * Quaternion.Euler(rotationOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, turnSpeed * dt);
    }

    // 人物姿态驱动缩放，开火恢复原位
    void Pose_Scale(float dt)
    {
        if (control == null) Bind_Control();
        if (control == null) return;

        if (fireTimer > 0f) fireTimer -= dt;

        float factor = 1f;

        // 抬枪或开火期间保持原位，否则移动缩小
        bool raised = control.Con_gun_Control != null && control.Con_gun_Control.IsRaised;

        if (fireTimer <= 0f && !raised && control.Con_move != null && control.Con_move.isWASDDowm)
            factor = control.Con_move.isRuning ? runScale : walkScale;

        scaleTarget.localScale = Vector3.SmoothDamp(scaleTarget.localScale, baseScale * factor, ref scaleVelocity, smoothTime);
    }

    // 绑定本机玩家并订阅开火
    void Bind_Control()
    {
        if (Player_Main.player_Main == null) return;

        Player_Control c = Player_Main.player_Main.oPlayer_Control;
        if (c == null || c.Con_gun_Control == null) return;

        control = c;
        c.Con_gun_Control.Shoot_event += On_Shoot;
    }

    void On_Shoot()
    {
        fireTimer = fireHold;
    }

    void OnDestroy()
    {
        if (control != null && control.Con_gun_Control != null)
            control.Con_gun_Control.Shoot_event -= On_Shoot;
    }
}
