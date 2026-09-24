using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Player_camera : MonoBehaviour
{
    public Transform Camera_Object;
    public Transform Target_Object;

    public Transform Player;

    float mouseSensitivity = 1f;
    public float radius;
    float rotationX, rotationY;

    float verticalLimit = 90f;

    // 初始时记录的相机偏移坐标
    public Vector3 startOffset;

    [Header("肩射模式")]
    public Vector3 shoulderStartOffset;   // 肩射姿态下的偏移
    public float shoulderRadius;          // 肩射姿态下的半径
    public bool isShoulderAim;            // 是否处于肩射模式

    public Vector3 shakeOffset;

    // ============ 相机震动 ============
    [Header("相机震动")]
    public float shakeDuration = 0.1f;   // 震动持续时间（秒）
    public float shakeMagnitude = 0.1f;  // 震动幅度
    bool isShaking;
    float shakeTimer;
    Vector3 worldOffset;

    // ============ 视角切换平滑（仅肩射/常规模式切换生效，旋转即时） ============
    [Header("视角切换平滑")]
    public float modeSwitchSmoothTime = 0.15f;   // 模式切换平滑时间
    Vector3 currentLocalOffset;                  // 当前局部环绕偏移（模式间平滑过渡）
    Vector3 localVelocity;                       // 局部偏移 SmoothDamp 速度缓存

    private void Start()
    {
        rotationX = transform.rotation.x;
        rotationY = transform.rotation.y;
        // 初始化当前局部环绕偏移为常规姿态
        currentLocalOffset = new Vector3(0, 0, -radius) + startOffset;
    }

    private void Update()
    {
        // 非本地玩家：相机只跟随本地控制的对象，不更新
        NetworkObject pNet = Player != null ? Player.GetComponentInParent<NetworkObject>() : null;
        if (pNet != null && !pNet.IsOwner) return;

        //角度旋转控制
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        rotationX += mouseX;
        rotationY -= mouseY;                       // 鼠标上移，视角向下，所以减
        rotationY = Mathf.Clamp(rotationY, -verticalLimit, verticalLimit);

        Quaternion quat = Quaternion.Euler(rotationY, rotationX, 0f);
        transform.localRotation = quat;


        //位置旋转
        Quaternion position_quat = transform.rotation;
        
        // 目标局部环绕偏移（肩射/常规姿态）
        Vector3 targetLocalOffset = isShoulderAim
            ? new Vector3(0, 0, -shoulderRadius) + shoulderStartOffset
            : new Vector3(0, 0, -radius) + startOffset;
        // 局部偏移仅在模式切换时平滑过渡；旋转每帧即时叠加，不产生拖影
        currentLocalOffset = Vector3.SmoothDamp(currentLocalOffset, targetLocalOffset, ref localVelocity, modeSwitchSmoothTime);
        worldOffset = position_quat * currentLocalOffset;

        //震动逻辑（计时结束归零）
        if (isShaking)
        {
            shakeTimer += Time.deltaTime;
            if (shakeTimer >= shakeDuration)
            {
                isShaking = false;
                shakeOffset = Vector3.zero;
            }
        }

        //作用（旋转即时、模式切换平滑后的位置 + 震动偏移）
        Target_Object.position = Player.position;
        Camera_Object.transform.position = Target_Object.position + worldOffset + shakeOffset;

    }

    // 触发一次震动偏移
    void ShakeOffset()
    {
        isShaking = true;
        shakeTimer = 0;
        shakeOffset = Random.insideUnitSphere * shakeMagnitude;
    }

    // 触发相机震动（开火时调用）
    public void Shake()
    {
        ShakeOffset();
    }

    // 切换肩射模式（右键时由 Player_Control 调用）
    public void SetShoulderAim(bool on)
    {
        isShoulderAim = on;
    }
}
