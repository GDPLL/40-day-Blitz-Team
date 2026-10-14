using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Player_camera : MonoBehaviour
{
    public Transform Camera_Object;         //角度与朝向信息
    public Transform Target_Object;         //位置跟随信息
    public Transform PlayerHeadTransform;   //跟随位置

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

    [Header("开镜模式")]
    public Vector3 adsStartOffset;        // 开镜偏移
    public float adsRadius;               // 开镜半径
    public float adsFov = 45f;            // 开镜视角
    public bool isAds;                    // 是否开镜

    public Vector3 shakeOffset;           // 震动偏移

    [Header("相机震动")]
    public float shakeDuration = 0.1f;   // 震动持续时间
    public float shakeMagnitude = 0.1f;  // 震动幅度
    bool isShaking;                      // 是否震动中
    float shakeTimer;                    // 震动计时
    Vector3 worldOffset;                 // 世界偏移

    [Header("压制恍惚")]
    public float suppressRadius = 8f;        // 压制边缘距离
    public float suppressFullRadius = 2.5f;  // 压制满效距离
    public float suppressPerHit = 0.35f;     // 单发压制累加
    public float suppressDecay = 1.2f;       // 压制每秒衰减
    public float suppressShakePosition = 0.25f;  // 压制位置幅度
    public float suppressAimAngle = 2f;      // 压制俯仰偏航幅度
    public float suppressRollAngle = 4f;     // 压制翻滚幅度
    public float suppressFov = 6f;           // 压制视角收缩
    public float suppressNoiseSpeed = 25f;   // 压制抖动频率

    float suppressTrauma;        // 压制累积强度
    Vector3 suppressPosOffset;   // 压制位置抖动
    Vector3 suppressRotOffset;   // 压制角度抖动
    float suppressFovOffset;     // 压制视角偏移

    [Header("相机遮挡")]
    public LayerMask cameraBlockMask = ~0;   // 相机遮挡层
    public float cameraRadius = 0.4f;        // 相机碰撞半径
    public float cameraMinDistance = 0.3f;   // 相机最近距离
    public float cameraPullInSpeed = 25f;    // 拉近速度
    public float cameraPushOutSpeed = 6f;    // 推远速度

    float cameraDistance;                     // 当前遮挡距离
    readonly RaycastHit[] cameraHitBuffer = new RaycastHit[8];   // 遮挡检测缓存

    [Header("视角切换平滑")]
    public float modeSwitchSmoothTime = 0.15f;   // 平滑时间
    Vector3 currentLocalOffset;                  // 当前局部偏移
    Vector3 localVelocity;                       // 平滑速度缓存

    NetworkObject pNet;   // 联网对象
    Camera cam;           // 相机组件

    float startFov;       // 常规视角
    float currentFov;     // 当前视角
    float fovVelocity;    // 视角平滑缓存

    // 记录初始视角与偏移
    public void Player_camera_Start(Transform _PlayerHeadTransform)
    {
        if (_PlayerHeadTransform == null)
        {
            Debug.LogError("Player_camera|Player_camera_Start|PlayerHeadTransform 为空");
            return;
        }
        if (Camera_Object == null)
        {
            Debug.LogError("Player_camera|Player_camera_Start|Camera_Object 为空");
            return;
        }

        cam = Camera_Object.GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("Player_camera|Player_camera_Start|Camera_Object 上无相机组件");
            return;
        }

        PlayerHeadTransform = _PlayerHeadTransform;
        pNet = PlayerHeadTransform.GetComponentInParent<NetworkObject>();

        startFov = cam.fieldOfView;                                      // 记录常规视角
        currentFov = startFov;
        rotationX = transform.rotation.x;
        rotationY = transform.rotation.y;
        currentLocalOffset = new Vector3(0, 0, -radius) + startOffset;   // 初始为常规姿态
        cameraDistance = currentLocalOffset.magnitude;

        Debug.Log("Player_camera|Player_camera_Start|完成初始化");
    }

    // 按当前姿态取相机偏移
    Vector3 PoseOffset()
    {
        if (isAds) return new Vector3(0, 0, -adsRadius) + adsStartOffset;   // 开镜优先

        return isShoulderAim
            ? new Vector3(0, 0, -shoulderRadius) + shoulderStartOffset
            : new Vector3(0, 0, -radius) + startOffset;
    }

    // 视角旋转与相机跟随
    public void Camera_Follow_Performance_Local()
    {
        if(!Player_Main.player_Main.isInit) return;     //等待关卡初始化

        if (pNet != null && !pNet.IsOwner) return;      // 非本地玩家不更新

        // 引用为空或已销毁
        if (PlayerHeadTransform == null || Target_Object == null || Camera_Object == null)
        {
            Debug.LogError($"Player_camera|Update|引用失效 PlayerHeadTransform={PlayerHeadTransform != null} 目标={Target_Object != null} 相机={Camera_Object != null}");
            return;
        }

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
        Vector3 targetLocalOffset = PoseOffset();

        currentLocalOffset = Vector3.SmoothDamp(currentLocalOffset, targetLocalOffset, ref localVelocity, modeSwitchSmoothTime);
        worldOffset = position_quat * currentLocalOffset;

        // 压制抖动
        SuppressOffset(Time.deltaTime);

        // 开镜视角插值
        currentFov = Mathf.SmoothDamp(currentFov, isAds ? adsFov : startFov, ref fovVelocity, modeSwitchSmoothTime);
        cam.fieldOfView = currentFov + suppressFovOffset;

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
        Target_Object.position = PlayerHeadTransform.position;
        Camera_Object.transform.position = CameraBlockPosition(Target_Object.position, worldOffset)
            + shakeOffset + suppressPosOffset;

        // 压制抖动叠加到朝向
        transform.localRotation = quat * Quaternion.Euler(suppressRotOffset);
    }

    // 触发一次震动偏移
    void ShakeOffset()
    {
        isShaking = true;
        shakeTimer = 0;
        shakeOffset = Random.insideUnitSphere * shakeMagnitude;
    }

    // 开火震动
    public void Camera_Shoot_Performance_Local()
    {
        ShakeOffset();
    }

    // 近处落点累加压制，参数为落点
    public void Camera_Suppress_Performance_Local(Vector3 hitPoint)
    {
        if (PlayerHeadTransform == null)
        {
            Debug.LogError("Player_camera|Camera_Suppress_Performance_Local|PlayerHeadTransform 为空");
            return;
        }
        if (suppressRadius <= suppressFullRadius)
        {
            Debug.LogError("Player_camera|Camera_Suppress_Performance_Local|压制半径配置错误");
            return;
        }

        float distance = Vector3.Distance(PlayerHeadTransform.position, hitPoint);
        if (distance >= suppressRadius) return;   //超出压制范围

        // 距离越近压制越强
        float factor = Mathf.InverseLerp(suppressRadius, suppressFullRadius, distance);
        suppressTrauma = Mathf.Clamp01(suppressTrauma + suppressPerHit * factor);
    }

    // 按累积强度生成抖动，参数为帧时间
    void SuppressOffset(float dt)
    {
        if (suppressTrauma <= 0f)
        {
            suppressPosOffset = Vector3.zero;
            suppressRotOffset = Vector3.zero;
            suppressFovOffset = 0f;
            return;
        }

        suppressTrauma = Mathf.Max(0f, suppressTrauma - suppressDecay * dt);

        float amp = suppressTrauma * suppressTrauma;   //平方衰减
        float t = Time.time * suppressNoiseSpeed;
        float nx = Mathf.PerlinNoise(t, 0.11f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(t, 3.71f) * 2f - 1f;
        float nr = Mathf.PerlinNoise(t, 7.93f) * 2f - 1f;

        suppressPosOffset = new Vector3(nx, ny, 0f) * suppressShakePosition * amp;
        suppressRotOffset = new Vector3(ny * suppressAimAngle, nx * suppressAimAngle, nr * suppressRollAngle) * amp;
        suppressFovOffset = -suppressFov * amp;
    }

    // 计算遮挡后相机位置，参数为头部与期望偏移
    Vector3 CameraBlockPosition(Vector3 pivot, Vector3 offset)
    {
        float want = offset.magnitude;
        if (want <= 0.001f) return pivot;

        Vector3 dir = offset / want;
        float limit = want;

        int count = Physics.SphereCastNonAlloc(pivot, cameraRadius, dir,
            cameraHitBuffer, want, cameraBlockMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (cameraHitBuffer[i].collider.transform.IsChildOf(PlayerHeadTransform.root)) continue;   // 忽略自身
            if (cameraHitBuffer[i].distance < limit) limit = cameraHitBuffer[i].distance;
        }

        // 距离限制在最近与期望之间
        limit = Mathf.Min(limit, want);
        limit = Mathf.Max(limit, Mathf.Min(cameraMinDistance, want));

        float speed = limit < cameraDistance ? cameraPullInSpeed : cameraPushOutSpeed;   // 拉近快推远慢
        cameraDistance = Mathf.MoveTowards(cameraDistance, limit, speed * Time.deltaTime);

        return pivot + dir * cameraDistance;
    }

    // 切换肩射与开镜
    public void Camera_Aim_Performance_Local(bool shoulder, bool ads)
    {
        isShoulderAim = shoulder;
        isAds = ads;
    }
}
