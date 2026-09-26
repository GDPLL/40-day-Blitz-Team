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

    // 相机偏移
    public Vector3 startOffset;                 // 初始偏移

    [Header("肩射模式")]
    public Vector3 shoulderStartOffset;   // 肩射偏移
    public float shoulderRadius;          // 肩射半径
    public bool isShoulderAim;            // 是否肩射

    public Vector3 shakeOffset;           // 震动偏移

    [Header("相机震动")]
    public float shakeDuration = 0.1f;   // 震动持续时间
    public float shakeMagnitude = 0.1f;  // 震动幅度
    bool isShaking;                      // 是否震动中
    float shakeTimer;                    // 震动计时
    Vector3 worldOffset;                 // 世界偏移

    [Header("视角切换平滑")]
    public float modeSwitchSmoothTime = 0.15f;   // 平滑时间
    Vector3 currentLocalOffset;                  // 当前局部偏移
    Vector3 localVelocity;                       // 平滑速度缓存

    NetworkObject pNet;   // 联网对象

    // 记录初始视角与偏移
    private void Start()
    {
        rotationX = transform.rotation.x;
        rotationY = transform.rotation.y;
        currentLocalOffset = new Vector3(0, 0, -radius) + startOffset;   // 初始为常规姿态
        pNet = Player != null ? Player.GetComponentInParent<NetworkObject>() : null;
    }

    // 每帧更新视角与相机位置
    private void Update()
    {
        if (pNet != null && !pNet.IsOwner) return;   // 非本地玩家不更新

        // 视角旋转
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        rotationX += mouseX;
        rotationY -= mouseY;                       // 鼠标上移视角向下
        rotationY = Mathf.Clamp(rotationY, -verticalLimit, verticalLimit);

        Quaternion quat = Quaternion.Euler(rotationY, rotationX, 0f);
        transform.localRotation = quat;


        //位置旋转
        Quaternion position_quat = transform.rotation;

        // 目标局部偏移
        Vector3 targetLocalOffset = isShoulderAim
            ? new Vector3(0, 0, -shoulderRadius) + shoulderStartOffset
            : new Vector3(0, 0, -radius) + startOffset;

        currentLocalOffset = Vector3.SmoothDamp(currentLocalOffset, targetLocalOffset, ref localVelocity, modeSwitchSmoothTime);
        worldOffset = position_quat * currentLocalOffset;

        // 震动计时
        if (isShaking)
        {
            shakeTimer += Time.deltaTime;
            if (shakeTimer >= shakeDuration)
            {
                isShaking = false;
                shakeOffset = Vector3.zero;
            }
        }

        // 应用位置
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

    // 相机震动
    public void Shake()
    {
        ShakeOffset();
    }

    // 切换肩射
    public void SetShoulderAim(bool on)
    {
        isShoulderAim = on;
    }
}
