using UnityEngine;

public class Gun_Control : MonoBehaviour
{
    public Transform shootPoint;   // 枪口
    public LineRenderer line;      // 射线显示
    public AudioSource audioSource;   // 音源

    public int ammo = 30;          // 弹药
    public int maxAmmo = 30;       // 弹匣容量
    public float fireRate = 10f;   // 射速
    public float range = 100f;     // 射程
    public int damage = 34;        // 单发伤害

    public GameObject hitEffect;   // 命中特效
    public GameObject fireEffect;  // 枪口特效

    [Header("后坐力")]
    public float recoilX = 0.03f;            // 随机后坐力横向幅度
    public float recoilY = 0.06f;            // 随机后坐力上抬幅度
    public float maxRecoil = 0.5f;           // 后坐力上限
    public float recoilRecoverFactor = 2f;   // 后坐力恢复系数

    // 后坐力状态
    Vector2 recoil;                          // 当前累计后坐力
    float recoilRecoverSpeed;                // 恢复速率
    float originalRecoilX, originalRecoilY;  // 原始后坐力

    [Header("举枪 / 收枪")]
    public float aimSmooth = 10f;            // 插值速度
    Quaternion originalLocalRot;             // 初始局部旋转

    // 计时
    float fireTimer;               // 射速计时
    float lineTimer;               // 射线显示计时
    float lastShotTime;            // 上次射击时间

    // 一种姿态对应的一组参数
    // 外圈、内圈与增长速度
    [System.Serializable]
    public class AimProfile
    {
        public float outerAngle = 20f;   // 外圈
        public float innerAngle = 5f;   // 内圈
        public float growSpeed = 8f;   // 精度增长速度

        public AimProfile(float outer, float inner, float speed)
        {
            outerAngle = outer;
            innerAngle = inner;
            growSpeed = speed;
        }
    }

    [Header("精度：各姿态参数")]
    AimProfile hipAim = new AimProfile(15f, 5f, 4f);   // 腰射
    AimProfile shoulderAim = new AimProfile(15f, 4f, 5f);   // 据枪
    AimProfile adsAim = new AimProfile(15f, 3f, 6f);   // 开镜

    [Header("瞄准")]
    public float aimAlignAngle = 8f;   // 朝向与准星最大夹角

    [Header("精度：距离影响")]
    public float nearDistance = 5f;      // 满速距离
    public float farDistance = 40f;     // 最低速距离
    public float farFactor = 0.6f;   // 远距离系数

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

    float currentAngle;                                       // 当前精度圈
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

     // 朝向是否对准准星
    bool AimAligned()
    {
        Vector3 aim = aimDirection;
        aim.y = 0f;

        Vector3 body = transform.root.forward;
        body.y = 0f;

        return Vector3.Angle(aim, body) <= aimAlignAngle;
    }

    // 设置姿态，shoulder为据枪，adsOn为开镜
    public void SetAimState(bool shoulder, bool adsOn)
    {
        aimingShoulder = shoulder;
        aimingAds = adsOn;
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
    public void Gun_Control_Init()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null) line.enabled = false;
        originalRecoilX = recoilX;   // 记录原始后坐力
        originalRecoilY = recoilY;

        currentAngle = hipAim.outerAngle;               //初始为腰射外圈
        originalLocalRot = transform.localRotation;     // 记录初始旋转

        Debug.Log("Gun_Control|Gun_Control_Init|完成初始化");
    }

    // 物理帧逻辑，计时与后坐力恢复
    public void Gun_Fixed_Date()
    {
        fireTimer += Time.deltaTime;

        UpdateAimAccuracy(Time.deltaTime);   // 更新精度

        // 后坐力恢复
        if (recoil.sqrMagnitude > 0.0001f)
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, recoilRecoverSpeed * Time.deltaTime);
        else
            recoil = Vector2.zero;
    }

    // 物理帧表现，射线与音效计时
    public void Gun_Fixed_Performance()
    {
        // 射线短暂显示后消失
        lineTimer -= Time.deltaTime;
        if (line != null && lineTimer <= 0f) line.enabled = false;

        // 长时间未射击停止音效
        if (audioSource != null && audioSource.isPlaying && Time.time - lastShotTime > 0.2f)
            audioSource.Stop();
    }

    // 按目标与移动状态更新精度
    void UpdateAimAccuracy(float dt)
    {
        AimProfile p = CurrentProfile();

        // 判定目标是否还在当前外圈
        targetInRing = hasTarget &&
            Vector3.Angle(aimDirection, targetPos - MuzzlePosition) <= p.outerAngle * 0.5f;   //外圈按直径算

        // 离开范围或朝向没对准准星都不收圈
        if (!targetInRing || !AimAligned())
        {
            currentAngle = p.outerAngle;
            return;
        }

        float distance = (targetPos - MuzzlePosition).magnitude;
        float speed = p.growSpeed * MoveFactor() * DistanceFactor(distance);
        currentAngle = Mathf.MoveTowards(currentAngle, p.innerAngle, speed * dt);   // 收缩到内圈
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
    float DistanceFactor(float d)
        => Mathf.Lerp(2f, farFactor, Mathf.InverseLerp(nearDistance, farDistance, d));

    // 精度圈内随机偏移
    Vector2 GetSpreadOffset()
        => Random.insideUnitCircle * Mathf.Tan(currentAngle * Mathf.Deg2Rad * 0.5f);

    // 开火逻辑，返回是否成功
    public bool Gun_Shoot_Date(Vector3 aimDir, out Vector3 origin, out Vector3 dir,
        out bool isHit, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        origin = MuzzlePosition;
        dir = transform.forward;
        isHit = false;
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;

        SetAimDirection(aimDir);      // 更新准星方向

        if (!AimAligned()) return false;                                       // 朝向没对准准星不能开火
        if ((fireTimer >= 1f / fireRate && ammo > 0) == false) return false;   // 射速与弹药判定

        ammo--;
        fireTimer = 0f;

        // 累计后坐力
        recoil += new Vector2(Random.Range(-recoilX, recoilX), Random.Range(0f, recoilY));
        recoil = Vector2.ClampMagnitude(recoil, maxRecoil);   // 限制后坐力上限
        // 计算恢复速率
        recoilRecoverSpeed = recoil.magnitude / (recoilRecoverFactor / fireRate);

        // 后坐力叠加精度散布
        Vector2 offset = recoil + GetSpreadOffset();

        // 锁定圈内时子弹直指目标
        dir = targetInRing ? (targetPos - origin).normalized : aimDir.normalized;
        dir += transform.right * offset.x + transform.up * offset.y;
        dir.Normalize();

        // 射线检测与伤害结算
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range))
        {
            isHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;

            Idamage damageable = hit.collider.GetComponent<Idamage>();
            if (damageable != null) damageable.Takedamage(damage);
        }

        return true;    // 本次已开火
    }

    // 开火表现，参数为逻辑结果
    public void Gun_Shoot_Performance(Vector3 origin, Vector3 dir, bool isHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        Gun_Shoot_Local_Performance(origin, dir);
        Gun_Shoot_Line_Performance(origin, dir, isHit, hitPoint, hitNormal);
    }

    // 本机开火表现，枪口特效与音效
    public void Gun_Shoot_Local_Performance(Vector3 origin, Vector3 dir)
    {
        lastShotTime = Time.time;   //记录本次开火时刻

        // 播放射击音效
        if (audioSource != null && !audioSource.isPlaying) audioSource.Play();

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

    // 换弹逻辑，弹匣补满
    public void Gun_Reload_Date()
    {
        ammo = maxAmmo;
    }

    // 是否有弹药
    public bool HasAmmo() => ammo > 0;

    // 目标是否在射程内
    public bool InRange(Transform target) =>
        target != null && Vector3.Distance(transform.position, target.position) <= range;

    // 切换后坐力减免，on为真时减少 70%
    public void SetRecoilReduction(bool on)
    {
        recoilX = originalRecoilX * (on ? 0.3f : 1f);
        recoilY = originalRecoilY * (on ? 0.3f : 1f);
    }

    // 举枪朝向瞄准落点
    public void Gun_Aim_Performance(Vector3 aimPoint)
    {
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
        transform.localRotation = Quaternion.Slerp(transform.localRotation, originalLocalRot, Time.deltaTime * aimSmooth);
    }
}
