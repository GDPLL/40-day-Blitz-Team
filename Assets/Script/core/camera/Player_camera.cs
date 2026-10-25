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
    public float hurtShakeScale = 0.6f;  // 受击震动倍数
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
    public float cameraDistanceSmoothTime = 0.08f;   // 遮挡距离平滑
    public float cameraSnapDistance = 0.25f; // 突变拉近距离

    [Header("滑铲跟随")]
    public float slideCameraHeight = 1.45f;      // 滑铲相机高度
    public float slideCameraSmoothTime = 0.12f;  // 滑铲跟随平滑
    public float slideCameraReleaseTime = 0.25f;  // 滑铲高度提前恢复

    [Header("高度跟随")]
    public float followHeightSmoothTime = 0.08f; // 头部高度平滑

    float cameraDistance;                     // 当前遮挡距离
    float cameraDistanceVelocity;             // 遮挡距离速度
    Vector3 targetVelocity;                   // 跟随速度缓存
    float heightVelocity;                     // 高度速度缓存
    readonly RaycastHit[] cameraHitBuffer = new RaycastHit[8];   // 遮挡检测缓存

    [Header("视角切换平滑")]
    public float modeSwitchSmoothTime = 0.15f;   // 平滑时间
    Vector3 currentLocalOffset;                  // 当前局部偏移
    Vector3 localVelocity;                       // 平滑速度缓存

    [Header("探头镜头")]
    public float peekCameraOffset = 0.4f;        // 探头横向镜头偏移
    public float peekCameraRoll = 2f;            // 探头镜头滚转角度
    public float peekSmoothTime = 0.12f;         // 探头横移与滚转平滑
    float currentPeekRoll;                       // 当前探头滚转
    float peekRollVelocity;                      // 滚转平滑速度缓存

    [Header("信号花屏")]
    public float breakTime = 0.12f;          // 断裂段时长
    public float holdTime = 0.2f;            // 保持段最短时长
    public float recoverTime = 0.5f;         // 恢复段时长
    public float strongTime = 0.2f;          // 恢复段强效时长
    public float holdStrength = 0.8f;        // 保持段强度
    public float recoverStrength = 0.85f;    // 恢复段起点强度
    public float outStrength = 0.5f;         // 淘汰后强度
    public float noiseMax = 0.85f;           // 雪花峰值
    public float tearMax = 0.12f;            // 撕裂位移峰值，屏幕宽比例
    public float chromaMax = 6f;             // 通道分离峰值，像素
    public float greyMax = 0.7f;             // 去色峰值
    public float brightMax = 0.18f;          // 亮度抖动峰值
    public float blockMax = 0.6f;            // 马赛克峰值
    public float rollPeriod = 1.6f;          // 滚动带周期，秒
    public float seedRate = 12f;             // 花屏换帧频率，次每秒

    [Header("信号花屏细节")]
    public float scanline = 0.18f;           // 扫描线强度
    public float bandBright = 0.35f;         // 滚动带亮度
    public float flickerSpeed = 30f;         // 亮度抖动频率
    public float flashChance = 0.05f;        // 白闪概率
    public float flashBright = 0.2f;         // 白闪亮度
    public float blockChance = 0.2f;         // 马赛克概率

    Material signalMat;        // 花屏材质
    bool signalReady;          // 花屏就绪
    Signal_Phase signalPhase;  // 当前花屏阶段
    float signalTimer;         // 阶段计时
    int lastSignalHp = -1;     // 上帧血量
    float signalSeed;          // 花屏随机种子
    float seedTimer;           // 换帧计时

    // 花屏阶段
    enum Signal_Phase { None, Break, Hold, Recover, Out }

    NetworkObject pNet;   // 联网对象
    Player_Control ownControl; // 玩家控制
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
        ownControl = PlayerHeadTransform.GetComponentInParent<Player_Control>();
        if (ownControl == null && pNet != null) ownControl = pNet.GetComponent<Player_Control>();
        Target_Object.position = PlayerHeadTransform.position;
        heightVelocity = 0f;

        startFov = cam.fieldOfView;                                      // 记录常规视角
        currentFov = startFov;
        rotationX = transform.rotation.x;
        rotationY = transform.rotation.y;
        currentLocalOffset = new Vector3(0, 0, -radius) + startOffset;   // 初始为常规姿态
        cameraDistance = currentLocalOffset.magnitude;

        // 信号花屏材质
        Shader signalShader = Resources.Load<Shader>("Signal_Glitch");
        if (signalShader == null)
        {
            Debug.LogError("Player_camera|Player_camera_Start|未找到 Signal_Glitch 着色器");
        }
        else
        {
            signalMat = new Material(signalShader);
            signalReady = true;
        }

        Debug.Log("Player_camera|Player_camera_Start|完成初始化");
    }

    // 按当前姿态取相机偏移，peekSide 为 -1 左、1 右、0 无探头
    Vector3 PoseOffset(float peekSide)
    {
        Vector3 offset;
        if (isAds) offset = new Vector3(0, 0, -adsRadius) + adsStartOffset;   // 开镜优先
        else if (isShoulderAim)
            offset = new Vector3(0, 0, -shoulderRadius) + shoulderStartOffset;
        else offset = new Vector3(0, 0, -radius) + startOffset;

        // 探头时用目标横移覆盖肩射自带的单侧偏移，保证左右对称
        if (peekSide != 0f) offset.x = peekSide * peekCameraOffset;
        return offset;
    }

    // 视角旋转与相机跟随
    public void Camera_Follow_Performance_Local()
    {
        if(!Player_Main.player_Main.isInit) return;     //等待关卡初始化

        if (pNet != null && !pNet.IsOwner) return;      // 非本地玩家不更新

        // 本机据枪与开镜输入，自行判断
        Input_Manage im = Input_Manage.Instance;
        float peekSide = 0f;
        if (im != null)
        {
            isShoulderAim = im.ShoulderHeld;
            isAds = im.AdsHeld;
            if (im.PeekLeftHeld != im.PeekRightHeld)
                peekSide = im.PeekLeftHeld ? -1f : 1f;
        }

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
        Vector3 targetLocalOffset = PoseOffset(peekSide);

        float offsetSmoothTime = peekSide != 0f ? peekSmoothTime : modeSwitchSmoothTime;
        currentLocalOffset = Vector3.SmoothDamp(currentLocalOffset, targetLocalOffset,
            ref localVelocity, offsetSmoothTime);
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

        // 滑铲时保持固定相机高度
        bool sliding = ownControl != null && ownControl.Con_move != null
            && ownControl.Con_move.Is_Slide;
        if (sliding)
        {
            // 水平方向锁住插值后的角色根节点，避免角色与平滑相机产生相对抖动
            Vector3 slideAnchor = ownControl.transform.position;
            float targetHeight = slideAnchor.y + slideCameraHeight;
            if (ownControl.Con_move.slideTimer <= slideCameraReleaseTime)
                targetHeight = PlayerHeadTransform.position.y;

            float slideY = Mathf.SmoothDamp(Target_Object.position.y, targetHeight,
                ref heightVelocity, slideCameraSmoothTime);
            Target_Object.position = new Vector3(slideAnchor.x, slideY, slideAnchor.z);
            targetVelocity = Vector3.zero;
        }
        else
        {
            Vector3 headPosition = PlayerHeadTransform.position;
            float cameraY = Mathf.SmoothDamp(Target_Object.position.y, headPosition.y,
                ref heightVelocity, followHeightSmoothTime);
            Target_Object.position = new Vector3(headPosition.x, cameraY, headPosition.z);
            targetVelocity = Vector3.zero;
        }
        Camera_Object.transform.position = CameraBlockPosition(Target_Object.position, worldOffset)
            + shakeOffset + suppressPosOffset;

        // 探头滚转只改变画面倾斜，不参与横移和射击方向计算
        float targetPeekRoll = peekSide * peekCameraRoll;
        currentPeekRoll = Mathf.SmoothDampAngle(currentPeekRoll, targetPeekRoll,
            ref peekRollVelocity, peekSmoothTime, Mathf.Infinity, Time.deltaTime);

        // 压制抖动与探头滚转叠加到朝向
        transform.localRotation = quat * Quaternion.Euler(suppressRotOffset)
            * Quaternion.Euler(0f, 0f, currentPeekRoll);
    }

    // 触发一次震动偏移，scale 为幅度倍数
    void ShakeOffset(float scale = 1f)
    {
        isShaking = true;
        shakeTimer = 0;
        shakeOffset = Random.insideUnitSphere * shakeMagnitude * scale;
    }

    // 开火震动
    public void Camera_Shoot_Performance_Local()
    {
        ShakeOffset();
    }

    // 受击震动
    public void Camera_Hurt_Performance_Local()
    {
        ShakeOffset(hurtShakeScale);
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

        // 从头部向后发射细射线，避免贴身正面墙体与检测球初始重叠导致零距离误判
        int count = Physics.RaycastNonAlloc(pivot, dir, cameraHitBuffer, want,
            cameraBlockMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (cameraHitBuffer[i].collider.transform.IsChildOf(PlayerHeadTransform.root)) continue;   // 忽略自身

            // 命中距离扣除相机半径，保留镜头与背后墙体之间的安全间距
            float hitLimit = Mathf.Max(0f, cameraHitBuffer[i].distance - cameraRadius);
            if (hitLimit < limit) limit = hitLimit;
        }

        // 距离限制在最近与期望之间
        limit = Mathf.Min(limit, want);
        limit = Mathf.Max(limit, Mathf.Min(cameraMinDistance, want));

        float speed = limit < cameraDistance ? cameraPullInSpeed : cameraPushOutSpeed;   // 拉近快推远慢
        if (limit < cameraDistance - cameraSnapDistance)
        {
            cameraDistance = limit;
            cameraDistanceVelocity = 0f;
        }
        else
        {
            cameraDistance = Mathf.SmoothDamp(cameraDistance, limit,
                ref cameraDistanceVelocity, cameraDistanceSmoothTime,
                speed, Time.deltaTime);
        }

        return pivot + dir * cameraDistance;
    }

    // 信号花屏，hp 为血量 isOut 为淘汰
    public void Camera_Signal_Performance_Local(int hp, bool isOut)
    {
        if (!signalReady) return;

        // 血量归零开始断裂
        if (hp <= 0 && lastSignalHp > 0) Signal_Switch(Signal_Phase.Break);

        // 淘汰后不再恢复
        if (isOut && signalPhase != Signal_Phase.Out) Signal_Switch(Signal_Phase.Out);

        lastSignalHp = hp;

        Signal_Timer(Time.deltaTime);
        Signal_Params(Time.deltaTime);
    }

    // 推进花屏阶段，dt 为帧时间
    void Signal_Timer(float dt)
    {
        if (signalPhase == Signal_Phase.None || signalPhase == Signal_Phase.Out) return;

        signalTimer += dt;

        // 断裂结束进入保持
        if (signalPhase == Signal_Phase.Break && signalTimer >= breakTime) Signal_Switch(Signal_Phase.Hold);

        // 已复活且保持够久进入恢复
        if (signalPhase == Signal_Phase.Hold && lastSignalHp > 0 && signalTimer >= holdTime)
            Signal_Switch(Signal_Phase.Recover);

        // 恢复结束关闭花屏
        if (signalPhase == Signal_Phase.Recover && signalTimer >= recoverTime) Signal_Switch(Signal_Phase.None);
    }

    // 切换花屏阶段
    void Signal_Switch(Signal_Phase next)
    {
        signalPhase = next;
        signalTimer = 0f;
    }

    // 按阶段写材质参数，dt 为帧时间
    void Signal_Params(float dt)
    {
        float strength;
        float band = 0f;

        switch (signalPhase)
        {
            case Signal_Phase.Break:
                strength = 1f;
                break;

            case Signal_Phase.Hold:
                strength = holdStrength;
                band = 1f;
                break;

            case Signal_Phase.Recover:
                // 恢复段快落，只留轻微残余
                float t = Mathf.Clamp01(signalTimer / recoverTime);
                strength = recoverStrength * (1f - t) * (1f - t);
                band = t < strongTime / recoverTime ? 1f : 0f;
                break;

            case Signal_Phase.Out:
                strength = outStrength;
                band = 1f;
                break;

            default:
                return;
        }

        // 换帧随机种子
        seedTimer += dt;
        if (seedTimer >= 1f / seedRate)
        {
            seedTimer = 0f;
            signalSeed = Random.value * 100f;
        }

        // 亮度抖动与偶发闪白
        float flicker = Mathf.PerlinNoise(Time.time * flickerSpeed, signalSeed) * 2f - 1f;
        float flash = Random.value < flashChance ? 1f : 0f;
        float bright = brightMax * strength * flicker + flashBright * strength * flash;

        // 马赛克只在部分帧出现
        float blockGate = Random.value < blockChance ? 1f : 0f;

        signalMat.SetFloat("_Noise", noiseMax * strength);
        signalMat.SetFloat("_Tear", tearMax * strength);
        signalMat.SetFloat("_Chroma", chromaMax * strength);
        signalMat.SetFloat("_Grey", greyMax * strength);
        signalMat.SetFloat("_Bright", bright);
        signalMat.SetFloat("_Scanline", scanline);
        signalMat.SetFloat("_BandBright", bandBright);
        signalMat.SetFloat("_Block", blockMax * strength * blockGate);
        signalMat.SetFloat("_Band", band * strength);
        signalMat.SetFloat("_Roll", Mathf.Repeat(Time.time / rollPeriod, 1f));
        signalMat.SetFloat("_Seed", signalSeed);
    }

    // 相机渲染后叠花屏
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!signalReady || signalPhase == Signal_Phase.None)
        {
            Graphics.Blit(src, dst);
            return;
        }

        Graphics.Blit(src, dst, signalMat);
    }
}
