using UnityEngine;

public class Gun_Control : MonoBehaviour
{
    [Header("朝向焦点")]
    public float focusDistance = 10f;   // 按住左键时，人物朝向相机射线前方 focusDistance 处的焦点

    public Transform shootPoint;   // 发射点（枪口）
    public LineRenderer line;      // 射线显示
    public AudioSource audioSource;

    public int ammo = 30;          // 弹药
    public int maxAmmo = 30;       // 弹匣容量
    public float fireRate = 10f;   // 射速（发/秒）
    public float range = 100f;     // 射程
    public int damage = 34;        // 单发伤害

    public GameObject hitEffect;   // 命中落点生成的对象（如溅射粒子）
    public GameObject fireEffect;  // 开火点生成的对象（如枪口火焰）

    [Header("后坐力")]
    public float recoilX = 0.03f;            // 随机后坐力横向幅度
    public float recoilY = 0.06f;            // 随机后坐力上抬幅度（只上抬）
    public float maxRecoil = 0.5f;           // 后坐力累计上限（钳制幅度）
    public float recoilRecoverFactor = 2f;   // 后坐力在「射击间隙 × 该值」后恢复为零

    Vector2 recoil;                          // 当前累计后坐力偏移
    float recoilRecoverSpeed;                // 后坐力恢复速率
    float originalRecoilX, originalRecoilY;  // 原始后坐力（肩射减后坐力后恢复用）

    [Header("举枪 / 收枪")]
    public float aimSmooth = 10f;            // 举枪、收枪的插值速度
    Quaternion originalLocalRot;             // 初始局部旋转（收枪目标）

    float fireTimer;               // 射速计时
    float lineTimer;               // 射线显示计时
    float lastShotTime;            // 上次射击时间

    RectTransform uiFocuspos;      // 中心UI

    // 一种姿态对应的一组参数
    [System.Serializable]
    public class AimProfile
    {
        public float outerAngle = 8f;   // 外圈：瞄准范围
        public float innerAngle = 2f;   // 内圈：该姿态能达到的最大精度
        public float growSpeed = 8f;   // 基础精度增长速度

        public AimProfile(float outer, float inner, float speed)
        {
            outerAngle = outer;
            innerAngle = inner;
            growSpeed = speed;
        }
    }

    [Header("精度：各姿态参数")]
    AimProfile hipAim = new AimProfile(8f, 4f, 4f);   // 腰射
    AimProfile shoulderAim = new AimProfile(4f, 1f, 2f);   // 据枪
    AimProfile adsAim = new AimProfile(2f, 0.3f, 0.3f);   // 开镜

    [Header("精度：距离影响")]
    public float nearDistance = 5f;      // 这个距离以内满速
    public float farDistance = 40f;     // 超过这个距离最低速
    public float farFactor = 0.15f;   // 远距离的系数

    [Header("精度：移动姿态系数（越激进越慢）")]
    public float moveFactorSquatStand = 1.3f;   // 蹲下
    public float moveFactorStand = 1.0f;   // 站立
    public float moveFactorSquatWalk = 0.7f;   // 蹲走
    public float moveFactorWalk = 0.5f;   // 走路
    public float moveFactorRun = 0.25f;  // 奔跑

    //本地状态
    bool aimingShoulder;     // 据枪姿态
    bool aimingAds;          // 开镜姿态（输入还没做，外界先传 false）
    bool stateSquat;         // 蹲着
    bool stateMoving;        // 有移动输入
    bool stateRunning;       // 奔跑
    bool hasTarget;          // 瞄准范围内是否有目标
    Vector3 targetPos;       // 目标世界坐标

    float currentAngle;                             // 当前实际精度圈（度）
    public float CurrentAngle => currentAngle;      // 给 UI 圈用
    public float OuterAngle   => CurrentProfile().outerAngle;

    // 按当前姿态取参数组
    AimProfile CurrentProfile()
        => aimingAds ? adsAim : (aimingShoulder ? shoulderAim : hipAim);

        public void SetAimState(bool shoulder, bool adsOn)
    {
        aimingShoulder = shoulder;
        aimingAds      = adsOn;
    }

    public void SetMoveState(bool isSquat, bool isMoving, bool isRunning)
    {
        stateSquat   = isSquat;
        stateMoving  = isMoving;
        stateRunning = isRunning;
    }

    public void SetTarget(bool has, Vector3 worldPosition)
    {
        hasTarget = has;
        targetPos = worldPosition;
    }
    void Start()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null) line.enabled = false;
        originalRecoilX = recoilX;   // 记录原始后坐力（肩射减后坐力恢复用）
        originalRecoilY = recoilY;

        currentAngle = hipAim.outerAngle;               //开局按腰射外圈起步
        originalLocalRot = transform.localRotation;     // 记录初始局部旋转（收枪目标）
    }

    public void Gun_Control_Init(RectTransform rectTransform)
    {
        uiFocuspos =rectTransform;
    }
    public void Gun_Control_FixedUpdate()
    {
        fireTimer += Time.deltaTime;

        UpdateAimAccuracy(Time.deltaTime);   // 精度每物理帧更新一次

        // 射线短暂显示后消失
        lineTimer -= Time.deltaTime;
        if (line != null && lineTimer <= 0f) line.enabled = false;

        // 停止射击声：超过一段时间未射击
        if (audioSource != null && audioSource.isPlaying && Time.time - lastShotTime > 0.2f)
            audioSource.Stop();

        // 后坐力恢复：在「射击间隙 × recoilRecoverFactor」后恢复为零
        if (recoil.sqrMagnitude > 0.0001f)
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, recoilRecoverSpeed * Time.deltaTime);
        else
            recoil = Vector2.zero;
    }

    // ===================== 新增：精度更新=====================
    void UpdateAimAccuracy(float dt)
    {
        AimProfile p = CurrentProfile();

        // 判定目标是否还在"当前姿态的外圈"里
        // 目标坐标由外界给，这里只做夹角计算，不去场景里找东西
        bool inRange = false;
        if (hasTarget)
        {
            Vector3 to = targetPos - MuzzlePosition;
            inRange = Vector3.Angle(transform.forward, to) <= p.outerAngle;
        }

        if (!inRange)
        {
            currentAngle = p.outerAngle;   // 离开瞄准范围：立刻丢失全部精度
            return;
        }

        float distance = (targetPos - MuzzlePosition).magnitude;
        float speed = p.growSpeed * MoveFactor() * DistanceFactor(distance);
        currentAngle = Mathf.MoveTowards(currentAngle, p.innerAngle, speed * dt);   // 从外圈收缩到内圈
    }

    float MoveFactor()
    {
        if (!stateMoving) return stateSquat ? moveFactorSquatStand : moveFactorStand;   // 蹲下 / 站立
        if (stateRunning) return moveFactorRun;                                        // 奔跑
        if (stateSquat) return moveFactorSquatWalk;                                  // 蹲走
        return moveFactorWalk;                                                         // 走路
    }

    float DistanceFactor(float d)
        => Mathf.Lerp(1f, farFactor, Mathf.InverseLerp(nearDistance, farDistance, d));

    // 在当前精度圈内均匀随机一个偏移量
    // 说明：这里把 currentAngle 当作"圈的直径角"，所以取一半作为最大偏移；
    //       如果以后把 currentAngle 定义成半径，就把 0.5f 去掉
    Vector2 GetSpreadOffset()
        => Random.insideUnitCircle * Mathf.Tan(currentAngle * Mathf.Deg2Rad * 0.5f);

    // ===================== 原有：射击 =====================
    // 射击：沿枪口正前方（玩家用），返回是否真正开火
    public bool Shoot() => Shoot(null);

    // 射击：朝目标开火（敌人用，target 为瞄准目标；为空时沿枪口正前方），返回是否真正开火
    public bool Shoot(Transform target)
    {
        if ((fireTimer >= 1f / fireRate && ammo > 0) == false) return false;
        ammo--;
        fireTimer = 0f;
        lastShotTime = Time.time;

        // 射击声：持续发声（未播放时启动）
        if (audioSource != null && !audioSource.isPlaying) audioSource.Play();

        Vector3 origin = MuzzlePosition;   // 【改】原来写在下面的算式提成了属性
        Vector3 aim = target != null ? target.position + Vector3.up * 1f : origin + transform.forward * 10f;

        // 随机二维后坐力上抬累计（X 左右随机、Y 只上抬）
        recoil += new Vector2(Random.Range(-recoilX, recoilX), Random.Range(0f, recoilY));
        recoil = Vector2.ClampMagnitude(recoil, maxRecoil);   // 钳制到后坐力上限
        // 恢复速率：让当前累计后坐力在「射击间隙 × recoilRecoverFactor」内线性归零
        recoilRecoverSpeed = recoil.magnitude / (recoilRecoverFactor / fireRate);

        // 【新增】精度散布，与后坐力叠加
        Vector2 offset = recoil + GetSpreadOffset();

        // 射击方向 = 基础朝向 + 偏移（后坐力 + 精度散布），再归一化
        Vector3 dir = (aim - origin).normalized + transform.right * offset.x + transform.up * offset.y;
        dir.Normalize();

        // 在开火点生成对象
        if (fireEffect != null)
            Instantiate(fireEffect, origin, Quaternion.LookRotation(dir));

        Vector3 end = origin + dir * range;                         // 默认射程终点
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range))
        {
            end = hit.point;                                        // 命中落点

            // 在落点生成对象
            if (hitEffect != null)
            {
                Vector3 bounceDir = Vector3.Reflect(dir, hit.normal);
                if (bounceDir.sqrMagnitude <= 0.001f) bounceDir = -hit.normal;   // 极端角度兜底
                Instantiate(hitEffect, hit.point, Quaternion.LookRotation(bounceDir));
            }

            // 获取命中对象上的 Idamage 接口，调用受伤逻辑并造成伤害
            // TODO 联机前要移出本脚本：现在这样每个客户端都会各自扣一次血
            Idamage damageable = hit.collider.GetComponent<Idamage>();
            if (damageable != null)
            {
                damageable.Takedamage(damage);
            }
        }

        // 在发射点与落点之间渲染射线
        if (line != null)
        {
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            line.enabled = true;
            lineTimer = 0.05f;
        }

        return true;    // 本次成功开火
    }

    // 换弹：直接补满弹药，对外调用
    public void Reload()
    {
        ammo = maxAmmo;
    }

    // 是否有弹药（对外检测）
    public bool HasAmmo() => ammo > 0;

    // 目标是否在射程内（对外检测）
    public bool InRange(Transform target) =>
        target != null && Vector3.Distance(transform.position, target.position) <= range;

    // 切换后坐力减免：on=true 时后坐力减少 70%（肩射时由 Player_Control 调用）
    public void SetRecoilReduction(bool on)
    {
        recoilX = originalRecoilX * (on ? 0.3f : 1f);
        recoilY = originalRecoilY * (on ? 0.3f : 1f);
    }

    // 举枪：枪口指向世界坐标 aimPoint
    public void AimAt()
    {
        Ray ray = Camera.main.ScreenPointToRay(uiFocuspos.position);
        Vector3 Point;
        if (TryGetAimPoint(ray, out Vector3 hitPoint))
            Point = hitPoint;                    // 命中对象：瞄准碰撞落点
        else
            Point = ray.GetPoint(focusDistance); // 未命中：回到固定焦点距离
        Quaternion worldLook = Quaternion.LookRotation(Point - transform.position, Vector3.up);
        Quaternion targetRot = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * worldLook
            : worldLook;

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * aimSmooth);
    }

    // 收枪：回到初始局部旋转
    public void AimDown()
    {
        transform.localRotation = Quaternion.Slerp(transform.localRotation, originalLocalRot, Time.deltaTime * aimSmooth);
    }

    bool TryGetAimPoint(Ray ray, out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player")) continue;   // 跳过玩家自身
            aimPoint = hit.point;
            return true;
        }
        return false;
    }
}
