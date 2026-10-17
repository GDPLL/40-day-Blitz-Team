using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class Gun_Control : MonoBehaviour
{
    public Transform shootPoint;   // 枪口
    public LineRenderer line;      // 射线显示

    // 表现层事件，音效等外部注册
    public event Action Shoot_event;    //开火一次
    public event Action Empty_event;    //空仓，没子弹扣扳机
    public event Action Raise_event;    //举枪
    public event Action Lower_event;    //放枪
    public event Action Shoulder_event;    //据枪
    public event Action Ads_event;      //瞄准
    public event Action Reload_event;   //换弹

    public int ammo = 30;          // 弹药
    public int maxAmmo = 30;       // 弹匣容量
    public int magazineCount = 6;      // 剩余弹夹数，含当前弹夹
    public int maxMagazineCount = 6;   // 弹夹总数
    public float fireRate = 900f;   // 射速，发每分钟
    public float range = 100f;     // 射程
    public int damage = 34;        // 单发伤害
    public float reloadTime = 3.06f;   // 换弹时长
    public RigBuilder armRig;      // 手臂IK构建器
    public int armRigLayer;        // 手臂IK所在的层序号

    // 两发之间的最小间隔
    public float FireInterval => 60f / fireRate;

    public GameObject hitEffect;   // 命中特效
    public GameObject fireEffect;  // 枪口特效

    [Header("命中爆浆")]
    public int bloodCount = 26;             // 每次命中粒子数
    public float bloodLife = 1.2f;          // 粒子存活时长，秒
    public float bloodSpeed = 4.5f;         // 喷射速度，米每秒
    public float bloodSize = 0.16f;         // 粒子尺寸，米
    public float bloodSpread = 38f;         // 喷射锥角，度
    public Color bloodColor = new Color(0.62f, 0.05f, 0.05f);   // 血色
    ParticleSystem bloodTemplate;           // 爆浆模板

    [Header("举枪 / 收枪")]
    public float aimSmooth = 10f;            // 插值速度
    Quaternion originalLocalRot;             // 初始局部旋转

    // 计时
    float fireTimer;               // 射速计时
    float lineTimer;               // 射线显示计时
    float reloadTimer;             // 换弹计时
    bool reloadMagazineSpent;      // 本次换弹已消耗弹夹
    bool isRaised;                 // 举枪中

    public bool IsReloading => reloadTimer > 0f;   // 换弹中

    // 一种姿态对应的一组参数
    // 外圈、内圈、增长速度与距离衰减
    [System.Serializable]
    public class AimProfile
    {
        public float outerAngle = 20f;    // 外圈
        public float innerAngle = 5f;     // 内圈
        public float growSpeed = 8f;      // 精度增长速度
        public float nearDistance = 5f;   // 最近参考距离
        public float farDistance = 40f;   // 最远参考距离
        public float farFactor = 0.15f;   // 远距离系数
        public int stableShots = 16;      // 稳定弹药数

        public AimProfile(float outer, float inner, float speed,
            float near, float far, float factor, int stable)
        {
            outerAngle = outer;
            innerAngle = inner;
            growSpeed = speed;
            nearDistance = near;
            farDistance = far;
            farFactor = factor;
            stableShots = stable;
        }
    }

    [Header("精度：各姿态参数")]
    public AimProfile hipAim = new AimProfile(30f, 7f, 60f, 2f, 15f, 1f, 16);       // 腰射
    public AimProfile shoulderAim = new AimProfile(10f, 4f, 15f, 6f, 35f, 1f, 5);   // 据枪
    public AimProfile adsAim = new AimProfile(15f, 2f, 10f, 100f, 500f, 1f, 2);       // 开镜

    [Header("精度：开火消耗")]
    public float stableCost = 0.30f;   // 稳定段总消耗比例
    public float burstCost = 0.35f;    // 超出稳定段每发消耗比例

    [Header("精度：移动姿态系数")]
    public float moveFactorSquatStand = 1.3f;   // 蹲下
    public float moveFactorStand = 1.0f;   // 站立
    public float moveFactorSquatWalk = 0.7f;   // 蹲走
    public float moveFactorWalk = 0.5f;   // 走路
    public float moveFactorRun = 0.25f;  // 奔跑

    //本地状态
    bool aimingShoulder;     // 据枪姿态
    bool aimingAds;          // 开镜姿态
    bool stateSquat;         // 蹲着
    bool stateMoving;        // 有移动输入
    bool stateRunning;       // 奔跑
    bool hasTarget;          // 是否有目标
    bool targetInRing;       // 目标是否在准星范围
    Vector3 targetPos;       // 目标世界坐标
    Vector3 aimDirection;    // 准星方向
    ulong ownerId;           // 持有者编号
    Vector3 aimOrigin;       // 准星射线起点

    float currentAngle;                                       // 当前精度圈
    int shotCount;                                            // 本次连发已射发数
    public float CurrentAngle => currentAngle;                // 对外读取
    public float OuterAngle => CurrentProfile().outerAngle;   // 当前姿态外圈
    public float InnerAngle => CurrentProfile().innerAngle;   // 当前姿态内圈
    public bool HasTarget => hasTarget;                       // 是否锁定
    public Vector3 TargetPos => targetPos;                    // 锁定点

    // 枪口位置
    public Vector3 MuzzlePosition =>
    shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 1f;

    // 按当前姿态取参数组
    AimProfile CurrentProfile()
        => aimingAds ? adsAim : (aimingShoulder ? shoulderAim : hipAim);

    // 注入准星方向
    public void SetAimDirection(Vector3 dir)
    {
        aimDirection = dir.normalized;
    }

    // 注入准星射线起点
    public void SetAimOrigin(Vector3 origin)
    {
        aimOrigin = origin;
    }

     // 朝向是否对准指定方向
    bool AimAligned(Vector3 dir)
    {
        Vector3 aim = dir;
        aim.y = 0f;

        // 垂直瞄准无水平朝向
        if (aim.sqrMagnitude <= 0.001f) return true;

        Vector3 body = transform.root.forward;
        body.y = 0f;

        // 允许偏差取当前外圈
        return Vector3.Angle(aim, body) <= CurrentProfile().outerAngle * 0.5f;
    }

    // 设置姿态，shoulder为据枪，adsOn为开镜
    public void SetAimState(bool shoulder, bool adsOn)
    {
        if (aimingShoulder == shoulder && aimingAds == adsOn) return;   //姿态没变

        // 姿态切换提示音
        if (shoulder && !aimingShoulder) Shoulder_event?.Invoke();
        if (adsOn && !aimingAds) Ads_event?.Invoke();

        aimingShoulder = shoulder;
        aimingAds = adsOn;
        currentAngle = CurrentProfile().outerAngle;   //立刻重置到新外圈
        shotCount = 0;                                //换姿态重新计稳定
    }

    // 设置移动状态
    public void SetMoveState(bool isSquat, bool isMoving, bool isRunning)
    {
        stateSquat = isSquat;
        stateMoving = isMoving;
        stateRunning = isRunning;
    }

    // 设置目标，has为是否有目标
    public void SetTarget(bool has, Vector3 worldPosition)
    {
        hasTarget = has;
        targetPos = worldPosition;
    }

    // 注入锁定与精度圈，客户端显示用
    public void SetAimShow(bool has, Vector3 worldPosition, float angle)
    {
        hasTarget = has;
        targetPos = worldPosition;
        currentAngle = angle;
    }

    // 注入完成
    public void Gun_Control_Init(ulong id)
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null) line.enabled = false;

        ownerId = id;
        shotCount = 0;
        currentAngle = hipAim.outerAngle;               //初始为腰射外圈
        originalLocalRot = transform.localRotation;     // 记录初始旋转

        // 旧实例缺字段时补默认弹夹数
        if (maxMagazineCount <= 0) maxMagazineCount = 6;
        if (magazineCount <= 0) magazineCount = maxMagazineCount;
        magazineCount = Mathf.Clamp(magazineCount, 1, maxMagazineCount);
        reloadMagazineSpent = false;
        Blood_Template_Init();

        Debug.Log("Gun_Control|Gun_Control_Init|完成初始化");
    }

    // 建爆浆粒子模板，隐藏备用
    void Blood_Template_Init()
    {
        Texture2D tex = Resources.Load<Texture2D>("爆浆/血点");
        Shader shader = Resources.Load<Shader>("爆浆/Blood_Particle");
        if (tex == null || shader == null)
        {
            Debug.LogError("Gun_Control|Blood_Template_Init|爆浆资源缺失");
            return;
        }

        GameObject go = new GameObject("BloodTemplate");
        go.transform.SetParent(transform, false);
        go.SetActive(false);

        bloodTemplate = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = bloodTemplate.main;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = bloodLife;
        main.startSpeed = bloodSpeed;
        main.startSize = bloodSize;
        main.startColor = bloodColor;
        main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = bloodTemplate.emission;
        emission.enabled = false;       // 手动发射，不自动播

        ParticleSystem.ShapeModule shape = bloodTemplate.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = bloodSpread;
        shape.radius = 0.04f;

        Material mat = new Material(shader);
        mat.mainTexture = tex;

        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = mat;
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // 命中敌人爆浆，dir 为弹道方向
    public void Gun_Blood_Performance(Vector3 hitPoint, Vector3 dir)
    {
        if (bloodTemplate == null)
        {
            Debug.LogError("Gun_Control|Gun_Blood_Performance|爆浆模板为空");
            return;
        }

        GameObject go = Instantiate(bloodTemplate.gameObject, hitPoint, Quaternion.LookRotation(-dir));
        go.SetActive(true);

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        ps.Emit(bloodCount);

        Destroy(go, bloodLife + 0.3f);
    }

    // 物理帧逻辑，射速计时与精度更新
    public void Gun_Fixed_Date()
    {
        fireTimer += Time.deltaTime;

        // 换弹计时，走完才装填
        if (reloadTimer > 0f)
        {
            Gun_Reload_Tick(Time.deltaTime);
            if (reloadTimer <= 0f)
            {
                ammo = maxAmmo;
                reloadMagazineSpent = false;
            }
        }

        UpdateAimAccuracy(Time.deltaTime);   // 更新精度
    }

    // 换弹计时推进，客机本机预测用
    public void Gun_Reload_Tick(float dt)
    {
        if (reloadTimer <= 0f) return;

        reloadTimer -= dt;
        if (reloadTimer <= 0f) reloadMagazineSpent = false;
    }

    // 物理帧表现，射线显示计时
    public void Gun_Fixed_Performance()
    {
        // 射线短暂显示后消失
        lineTimer -= Time.deltaTime;
        if (line != null && lineTimer <= 0f) line.enabled = false;
    }

    // 按目标与移动状态更新精度
    void UpdateAimAccuracy(float dt)
    {
        AimProfile p = CurrentProfile();

        // 判定目标是否还在当前外圈
        float distance = (targetPos - MuzzlePosition).magnitude;
        Vector3 toTarget = targetPos - aimOrigin;
        targetInRing = hasTarget && (distance < 1f ||
            Vector3.Angle(aimDirection, toTarget) <= p.outerAngle * 0.5f);   //外圈按直径算

        // 离开范围或朝向没对准准星都不收圈
        if (!targetInRing || !AimAligned(aimDirection))
        {
            currentAngle = p.outerAngle;
            return;
        }

        float speed = p.growSpeed * MoveFactor() * DistanceFactor(distance, p);
        currentAngle = Mathf.MoveTowards(currentAngle, p.innerAngle, speed * dt);   // 收缩到内圈

        // 精度收满即算重新稳定
        if (currentAngle <= p.innerAngle) shotCount = 0;
    }

    // 开火消耗精度，超出稳定弹药数消耗更大
    void Aim_Cost_Date()
    {
        AimProfile p = CurrentProfile();

        // 基准取精度总量，稳定段内按上限分摊
        float total = p.outerAngle - p.innerAngle;
        float cost = p.stableShots > 0 && shotCount < p.stableShots
            ? total * stableCost / p.stableShots : total * burstCost;

        currentAngle = Mathf.Min(currentAngle + cost, p.outerAngle);
        shotCount++;
    }

    // 移动姿态系数
    float MoveFactor()
    {
        if (!stateMoving) return stateSquat ? moveFactorSquatStand : moveFactorStand;   // 蹲下 / 站立
        if (stateRunning) return moveFactorRun;                                        // 奔跑
        if (stateSquat) return moveFactorSquatWalk;                                  // 蹲走
        return moveFactorWalk;                                                         // 走路
    }

    // 距离系数
    float DistanceFactor(float d, AimProfile p)
        => Mathf.Lerp(1f, p.farFactor, Mathf.InverseLerp(p.nearDistance, p.farDistance, d));

    // 精度圈内随机偏移
    Vector2 GetSpreadOffset()
        => UnityEngine.Random.insideUnitCircle * Mathf.Tan(currentAngle * Mathf.Deg2Rad * 0.5f);

    // 开火逻辑，aimDir为弹道方向，alignDir为对准判定方向
    public bool Gun_Shoot_Date(Vector3 aimDir, Vector3 alignDir, out Vector3 origin, out Vector3 dir,
        out bool isHit, out bool hitTarget, out Vector3 hitPoint, out Vector3 hitNormal,
        out int hitDamage, out bool killed, out Bounds hitBounds)
    {
        origin = MuzzlePosition;
        dir = transform.forward;
        isHit = false;
        hitTarget = false;
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        hitDamage = 0;
        killed = false;
        hitBounds = new Bounds();

        if (IsReloading) return false;                                         // 换弹中不能开火
        if (!AimAligned(alignDir)) return false;                               // 朝向没对准准星不能开火
        if ((fireTimer >= FireInterval && ammo > 0) == false) return false;    // 射速与弹药判定

        ammo--;
        fireTimer = 0f;
        Aim_Cost_Date();      // 开火消耗精度

        // 精度圈散布
        Vector2 offset = GetSpreadOffset();

        // 锁定直指目标，反向用准星
        Vector3 toTarget = targetPos - origin;
        dir = (targetInRing && Vector3.Dot(toTarget, aimDir) > 0f) ? toTarget.normalized : aimDir.normalized;
        dir += transform.right * offset.x + transform.up * offset.y;
        dir.Normalize();

        // 射线检测与伤害结算
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range))
        {
            isHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;

            // 不打自己
            if (!hit.collider.transform.IsChildOf(transform.root))
            {
                Idamage damageable = hit.collider.GetComponent<Idamage>();
                if (damageable != null)
                {
                    hitTarget = true;
                    hitDamage = damageable.Takedamage(damage, ownerId, transform.root.position, out killed);
                    hitBounds = hit.collider.bounds;
                }
            }
        }

        return true;    // 本次已开火
    }

    // 开火表现，参数为逻辑结果
    public void Gun_Shoot_Performance(Vector3 origin, Vector3 dir, bool isHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        Gun_Shoot_Local_Performance(origin, dir);
        Gun_Shoot_Line_Performance(origin, dir, isHit, hitPoint, hitNormal);
    }

    // 本机开火表现，枪口特效与开火事件
    public void Gun_Shoot_Local_Performance(Vector3 origin, Vector3 dir)
    {
        // 通知外部播放表现，音效等
        Shoot_event?.Invoke();

        // 在开火点生成对象
        if (fireEffect != null) Instantiate(fireEffect, origin, Quaternion.LookRotation(dir));
    }

    // 弹道与命中表现
    public void Gun_Shoot_Line_Performance(Vector3 origin, Vector3 dir, bool isHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 end = origin + dir * range;    // 默认终点
        if (isHit)
        {
            end = hitPoint;                    // 命中落点

            // 在落点生成对象
            if (hitEffect != null)
            {
                Vector3 bounceDir = Vector3.Reflect(dir, hitNormal);
                if (bounceDir.sqrMagnitude <= 0.001f) bounceDir = -hitNormal;   // 极端角度兜底
                Instantiate(hitEffect, hitPoint, Quaternion.LookRotation(bounceDir));
            }
        }

        // 渲染射线
        if (line != null)
        {
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            line.enabled = true;
            lineTimer = 0.05f;
        }
    }

    // 换弹，起换弹计时并丢弃当前弹夹，装填在计时结束
    public void Gun_Reload_Date()
    {
        if (IsReloading) return;        //换弹中不重复触发
        if (ammo == maxAmmo) return;    //满弹不换
        if (magazineCount <= 1) return; //没有备用弹夹

        magazineCount--;
        reloadMagazineSpent = true;
        reloadTimer = reloadTime;
        Reload_event?.Invoke();     //换弹音效
    }

    // 取消换弹
    public void Gun_Reload_Cancel()
    {
        // 换弹中断时归还已丢掉的弹夹
        if (reloadMagazineSpent)
        {
            magazineCount = Mathf.Min(magazineCount + 1, maxMagazineCount);
            reloadMagazineSpent = false;
        }

        reloadTimer = 0f;
    }

    // 手臂IK开关，on为启用，按层序号取
    public void Arm_Rig_Date(bool on)
    {
        if (armRig == null) return;
        if (armRigLayer < 0 || armRigLayer >= armRig.layers.Count) return;
        if (armRig.layers[armRigLayer].active == on) return;    //状态没变

        armRig.layers[armRigLayer].active = on;
        armRig.Build();     //改层开关后重建图
    }

    // 是否有弹药
    public bool HasAmmo() => ammo > 0;

    // 空仓，没子弹扣扳机
    public void Gun_Empty_Date() => Empty_event?.Invoke();

    // 目标是否在射程内
    public bool InRange(Transform target) =>
        target != null && Vector3.Distance(transform.position, target.position) <= range;

    // 举枪朝向瞄准落点
    public void Gun_Aim_Performance(Vector3 aimPoint)
    {
        if (!isRaised)
        {
            isRaised = true;
            Raise_event?.Invoke();      //举枪
        }

        if (aimPoint == Vector3.zero) return;   //无落点不转向

        Vector3 dir = aimPoint - transform.position;
        if (dir.sqrMagnitude <= 0.001f) return;   // 落点与枪重叠

        Quaternion worldLook = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion targetRot = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * worldLook
            : worldLook;

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * aimSmooth);
    }

    // 收枪
    public void Gun_AimDown_Performance()
    {
        if (isRaised)
        {
            isRaised = false;
            Lower_event?.Invoke();      //放枪
        }

        transform.localRotation = Quaternion.Slerp(transform.localRotation, originalLocalRot, Time.deltaTime * aimSmooth);
    }
}
