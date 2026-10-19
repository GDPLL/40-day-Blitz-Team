using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Unity.Netcode;

public class Gun_Control : NetworkBehaviour
{
    Player_Control Own_Control; //父级
    public void Owner_Init(Player_Control control) { Own_Control = control; }

    public Transform shootPoint;   // 枪口
    public LineRenderer line;      // 射线显示

    [Header("位置绑定")]
    public Transform gunRoot;      // 枪体节点，朝向与散布基准
    public Transform bodyRoot;     // 身体节点，朝向判定与伤害来源

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

    [Header("枪口特效对象池")]
    public int fireEffectPrewarm = 12;      // 预热数量
    public float fireEffectLife = 2f;       // 存活时长

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

    // 位置绑定兜底，未拖拽时取自身
    Transform Gun_Root => gunRoot != null ? gunRoot : transform;
    Transform Body_Root => bodyRoot != null ? bodyRoot : transform.root;

    // 枪口位置
    public Vector3 MuzzlePosition =>
        shootPoint != null ? shootPoint.position : Gun_Root.position + Vector3.up * 1f;

    // 按当前姿态取参数组
    AimProfile CurrentProfile()
        => aimingAds ? adsAim : (aimingShoulder ? shoulderAim : hipAim);



    // 输入驱动，由Control每帧调用
    public void Gun_Update()
    {
        Player_Control c = Own_Control;
        if (c == null) return;
        if (!c.IsServer && !c.IsOwner) return;      //客机只看别人表现
        if (c.Con_player_HostNetworkEvent == null) return;
        if (c.Con_ObjectSystem != null && c.Con_ObjectSystem.isDead) return;
        if (c.Con_net_State != null &&
            (c.Con_net_State.RespawnWait > 0f || c.Con_net_State.Is_Out)) return;

        // 主机取网络包，客机本机取本地包
        InputPacket packet = c.IsServer
            ? c.Con_player_HostNetworkEvent.Packet
            : (c.Con_input_Manage != null ? c.Con_input_Manage.LastPacket : default);

        // 换弹输入，重复触发由内部过滤
        if (packet.reload) Gun_Reload_Date();

        // 左键开火，按住连发由射速节流
        if (packet.mouse1)
        {
            bool isPress = !fireHeld;    //按下那一帧

            if (isPress && !HasAmmo()) Gun_Empty_Date();

            if (c.IsServer) Gun_Fire_Date(packet);
            else Gun_Fire_Local();
        }

        fireHeld = packet.mouse1;
    }

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

        Vector3 body = Body_Root.forward;
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

    // 注入锁定目标，由锁定模块推送
    public void Target_Push(bool has, Vector3 worldPosition)
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
        if (line == null) line = Gun_Root.GetComponent<LineRenderer>();
        if (line != null) line.enabled = false;

        ownerId = id;
        shotCount = 0;
        currentAngle = hipAim.outerAngle;               //初始为腰射外圈
        originalLocalRot = Gun_Root.localRotation;      // 记录初始旋转

        // 旧实例缺字段时补默认弹夹数
        if (maxMagazineCount <= 0) maxMagazineCount = 6;
        if (magazineCount <= 0) magazineCount = maxMagazineCount;
        magazineCount = Mathf.Clamp(magazineCount, 1, maxMagazineCount);
        reloadMagazineSpent = false;

        Blood_Template_Init();

        // 提前建好枪口特效，避免第一次开火集中实例化
        if (fireEffect != null) Object_Pool_Manager.Prewarm(fireEffect, fireEffectPrewarm);

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
        go.transform.SetParent(Gun_Root, false);
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
        dir = Gun_Root.forward;
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
        dir += Gun_Root.right * offset.x + Gun_Root.up * offset.y;
        dir.Normalize();

        // 射线检测与伤害结算
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range))
        {
            isHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;

            // 不打自己
            if (!hit.collider.transform.IsChildOf(Body_Root))
            {
                Idamage damageable = hit.collider.GetComponent<Idamage>();
                if (damageable != null)
                {
                    hitTarget = true;
                    hitDamage = damageable.Takedamage(damage, ownerId, Body_Root.position, out killed);
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

        // 枪口特效走对象池
        if (fireEffect != null)
            Object_Pool_Manager.Spawn(fireEffect, origin, Quaternion.LookRotation(dir), fireEffectLife);
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

    // 复活恢复满弹夹与满弹匣
    public void Gun_Respawn_Reset()
    {
        ammo = maxAmmo;
        magazineCount = maxMagazineCount;
        reloadTimer = 0f;
        reloadMagazineSpent = false;
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
        target != null && Vector3.Distance(Gun_Root.position, target.position) <= range;

    // 举枪朝向瞄准落点
    public void Gun_Aim_Performance(Vector3 aimPoint)
    {
        if (!isRaised)
        {
            isRaised = true;
            Raise_event?.Invoke();      //举枪
        }

        if (aimPoint == Vector3.zero) return;   //无落点不转向

        Vector3 dir = aimPoint - Gun_Root.position;
        if (dir.sqrMagnitude <= 0.001f) return;   // 落点与枪重叠

        Quaternion worldLook = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion targetRot = Gun_Root.parent != null
            ? Quaternion.Inverse(Gun_Root.parent.rotation) * worldLook
            : worldLook;

        Gun_Root.localRotation = Quaternion.Slerp(Gun_Root.localRotation, targetRot, Time.deltaTime * aimSmooth);
    }

    // 收枪
    public void Gun_AimDown_Performance()
    {
        if (isRaised)
        {
            isRaised = false;
            Lower_event?.Invoke();      //放枪
        }

        Gun_Root.localRotation = Quaternion.Slerp(Gun_Root.localRotation, originalLocalRot, Time.deltaTime * aimSmooth);
    }

    const int KillScore = 100;      //击杀加分
    float fireTime;                 //本机开火计时
    bool fireHeld;                  //左键按住


    // 客机本机开火预测，按射速节流
    void Gun_Fire_Local()
    {
        if (IsReloading) return;                    //换弹中不能开火
        if (!HasAmmo()) return;
        if (Time.time - fireTime < FireInterval) return;   //未到射速

        fireTime = Time.time;

        Gun_Shoot_Local_Performance(MuzzlePosition, Gun_Root.forward);

        if (Own_Control.Con_player_camera != null)
            Own_Control.Con_player_camera.Camera_Shoot_Performance_Local();
    }

    // 主机开火，算伤害并下发表现
    void Gun_Fire_Date(InputPacket packet)
    {
        // 枪口瞄准方向由落点推算,落点-开火点
        Vector3 aimDir = packet.aimPoint - MuzzlePosition;

        Vector3 flatAim = new Vector3(aimDir.x, 0f, aimDir.z);
        Vector3 flatView = new Vector3(packet.viewDir.x, 0f, packet.viewDir.z);

        if (flatAim.sqrMagnitude <= 0.001f || Vector3.Dot(flatAim, flatView) <= 0f)
            aimDir = packet.viewDir;

        // 对准判定取准星朝向，只用于本次判定
        Vector3 alignDir = packet.viewDir;

        if (!Gun_Shoot_Date(aimDir, alignDir, out Vector3 origin, out Vector3 dir,
            out bool isHit, out bool hitTarget, out Vector3 hitPoint, out Vector3 hitNormal,
            out int hitDamage, out bool killed, out Bounds hitBounds)) return;

        Gun_Shoot_Performance(origin, dir, isHit, hitPoint, hitNormal);

        // 命中敌人的爆浆
        if (hitTarget) Gun_Blood_Performance(hitPoint, aimDir);

        Player_Control c = Own_Control;

        // 本机受近处落点压制
        if (isHit && !c.IsOwner)
        {
            if (Player_Main.player_Main == null) Debug.LogError("Gun_Control|Gun_Fire_Date|Player_Main 为空");
            else Player_Main.player_Main.Suppress_Check_Local(hitPoint);
        }

        // 本机开火震屏
        if (c.IsOwner && c.Con_player_camera != null)
            c.Con_player_camera.Camera_Shoot_Performance_Local();

        // 命中积分，击杀分单独计算
        int killScore = killed ? KillScore : 0;
        if (hitTarget && c.Con_combat != null) c.Con_combat.Score_Add(hitDamage + killScore);

        // 本机命中反馈
        if (c.IsOwner && hitTarget && c.Con_hitFeedback_UI != null)
            c.Con_hitFeedback_UI.Hit_Result_Performance_Local(hitDamage, killScore, hitPoint, hitBounds.center, hitBounds.size);

        // 本机命中音效
        if (c.IsOwner && hitTarget && c.Con_player_Audio != null) c.Con_player_Audio.Hit_Show();

        Gun_Shoot_ClientRpc(origin, dir, isHit, hitTarget, hitPoint, hitNormal, hitDamage, killScore,
            hitBounds.center, hitBounds.size);
    }

    // 客户端开火表现
    [ClientRpc]
    void Gun_Shoot_ClientRpc(Vector3 origin, Vector3 dir, bool isHit, bool hitTarget, Vector3 hitPoint, Vector3 hitNormal,
        int hitDamage, int killScore, Vector3 hitCenter, Vector3 hitSize)
    {
        if (IsServer) return;   //主机已播放

        Player_Control c = Own_Control;

        // 本机受近处落点压制
        if (isHit && !c.IsOwner)
        {
            if (Player_Main.player_Main == null) Debug.LogError("Gun_Control|Gun_Shoot_ClientRpc|Player_Main 为空");
            else Player_Main.player_Main.Suppress_Check_Local(hitPoint);
        }

        // 命中敌人的爆浆
        if (hitTarget) Gun_Blood_Performance(hitPoint, dir);

        // 本机已预测枪口与震屏，只补弹道
        if (c.IsOwner)
        {
            Gun_Shoot_Line_Performance(origin, dir, isHit, hitPoint, hitNormal);

            // 本机命中反馈与音效
            if (hitTarget && c.Con_hitFeedback_UI != null)
                c.Con_hitFeedback_UI.Hit_Result_Performance_Local(hitDamage, killScore, hitPoint, hitCenter, hitSize);
            if (hitTarget && c.Con_player_Audio != null) c.Con_player_Audio.Hit_Show();
            return;
        }

        Gun_Shoot_Performance(origin, dir, isHit, hitPoint, hitNormal);
    }
}
