using UnityEngine;

public class Gun_Control : MonoBehaviour
{
    [Header("朝向焦点")]
    public float focusDistance = 10f;   // 按住左键时，人物朝向相机射线前方 focusDistance 处的焦点

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

    RectTransform uiFocuspos;      // 中心UI

    // 一种姿态的精度参数
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
    AimProfile shoulderAim = new AimProfile(20f, 5f, 2f);   // 据枪
    AimProfile adsAim = new AimProfile(20f, 5f, 0.3f);   // 开镜

    [Header("精度：距离影响")]
    public float nearDistance = 5f;      // 满速距离
    public float farDistance = 40f;     // 最低速距离
    public float farFactor = 0.15f;   // 远距离系数

    [Header("瞄准")]
    public float aimAlignAngle = 8f;     // 朝向与准星的最大夹角

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
    Vector3 targetPos;       // 目标世界坐标
    bool targetInRing;       // 目标是否在瞄准范围

    float currentAngle;                             // 当前精度圈
    public float CurrentAngle => currentAngle;      // 对外读取
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

    // 取准星方向
    Vector3 AimDirection()
        => uiFocuspos != null
            ? Camera.main.ScreenPointToRay(uiFocuspos.position).direction
            : transform.forward;

    // 人物朝向是否对准准星
    bool AimAligned()
        => Vector3.Angle(AimDirection(), transform.root.forward) <= aimAlignAngle;

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
    // 记录初始参数
    void Start()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null) line.enabled = false;
        originalRecoilX = recoilX;   // 记录原始后坐力
        originalRecoilY = recoilY;

        currentAngle = hipAim.outerAngle;               //初始为腰射外圈
        originalLocalRot = transform.localRotation;     // 记录初始旋转
    }

    // 注入准星UI
    public void Gun_Control_Init(RectTransform rectTransform)
    {
        uiFocuspos = rectTransform;
    }

    // 物理帧更新射速计时与精度
    public void Gun_Control_FixedUpdate()
    {
        fireTimer += Time.deltaTime;

        UpdateAimAccuracy(Time.deltaTime);   // 更新精度

        // 射线短暂显示后消失
        lineTimer -= Time.deltaTime;
        if (line != null && lineTimer <= 0f) line.enabled = false;

        // 长时间未射击停止音效
        if (audioSource != null && audioSource.isPlaying && Time.time - lastShotTime > 0.2f)
            audioSource.Stop();

        // 后坐力恢复
        if (recoil.sqrMagnitude > 0.0001f)
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, recoilRecoverSpeed * Time.deltaTime);
        else
            recoil = Vector2.zero;
    }

    // 按目标与移动状态更新精度
    void UpdateAimAccuracy(float dt)
    {
        AimProfile p = CurrentProfile();

        // 判定目标是否还在当前外圈
        targetInRing = hasTarget &&
            Vector3.Angle(AimDirection(), targetPos - Camera.main.transform.position) <= p.outerAngle * 0.5f;   //外圈按直径算

        // 离开范围丢失精度
        if (!targetInRing)
        {
            currentAngle = p.outerAngle;
            return;
        }

        // 朝向没对准准星，精度不收缩
        if (!AimAligned())
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
        => Mathf.Lerp(1f, farFactor, Mathf.InverseLerp(nearDistance, farDistance, d));

    // 精度圈内随机偏移
    Vector2 GetSpreadOffset()
        => Random.insideUnitCircle * Mathf.Tan(currentAngle * Mathf.Deg2Rad * 0.5f);

    // 沿枪口方向射击，返回是否开火
    public bool Shoot() => Shoot(null);

    // 朝目标射击，目标为空则沿枪口方向
    public bool Shoot(Transform target)
    {
        // 朝向没对准准星不能开火
        if (target == null && !AimAligned()) return false;

        if ((fireTimer >= 1f / fireRate && ammo > 0) == false) return false;
        ammo--;
        fireTimer = 0f;
        lastShotTime = Time.time;

        // 播放射击音效
        if (audioSource != null && !audioSource.isPlaying) audioSource.Play();

        Vector3 origin = MuzzlePosition;
        Vector3 aim = target != null ? target.position + Vector3.up * 1f : origin + AimDirection() * 10f;

        // 累计后坐力
        recoil += new Vector2(Random.Range(-recoilX, recoilX), Random.Range(0f, recoilY));
        recoil = Vector2.ClampMagnitude(recoil, maxRecoil);   // 限制后坐力上限
        // 计算恢复速率
        recoilRecoverSpeed = recoil.magnitude / (recoilRecoverFactor / fireRate);

        // 后坐力叠加精度散布
        Vector2 offset = recoil + GetSpreadOffset();

        // 计算射击方向
        // 锁定时中心指向目标
        Vector3 dir = (target == null && targetInRing)
            ? (targetPos - MuzzlePosition).normalized
            : (aim - origin).normalized;

        // 叠加后坐力与散布
        dir += transform.right * offset.x + transform.up * offset.y;
        dir.Normalize();

        // 在开火点生成对象
        if (fireEffect != null)
            Instantiate(fireEffect, origin, Quaternion.LookRotation(dir));

        Vector3 end = origin + dir * range;                         // 默认终点
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

            // 对命中对象造成伤害
            // TODO 联机后移到主机裁决
            Idamage damageable = hit.collider.GetComponent<Idamage>();
            if (damageable != null)
            {
                damageable.Takedamage(damage);
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

        return true;    // 本次已开火
    }

    // 换弹
    public void Reload()
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

    // 举枪瞄向准星
    public void AimAt()
    {
        Ray ray = Camera.main.ScreenPointToRay(uiFocuspos.position);
        Vector3 Point;
        if (TryGetAimPoint(ray, out Vector3 hitPoint))
            Point = hitPoint;                    // 瞄准碰撞落点
        else
            Point = ray.GetPoint(focusDistance); // 回到固定焦点距离
        Quaternion worldLook = Quaternion.LookRotation(Point - transform.position, Vector3.up);
        Quaternion targetRot = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * worldLook
            : worldLook;

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * aimSmooth);
    }

    // 收枪
    public void AimDown()
    {
        transform.localRotation = Quaternion.Slerp(transform.localRotation, originalLocalRot, Time.deltaTime * aimSmooth);
    }

    // 取射线命中点，跳过自身
    bool TryGetAimPoint(Ray ray, out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player")) continue;   // 跳过自身
            aimPoint = hit.point;
            return true;
        }
        return false;
    }

    // 临时调试区，测完整块删掉

    GameObject debugEnemy;
    GameObject debugWall;
    GameObject debugMarker;
    Vector3 debugEnemyStart;
    float debugStrafe;
    bool debugNeedFit;

    // 生成调试敌人
    void StartDebugAim()
    {
        RemoveDebugAim();

        Transform root = transform.root;
        Animator src = root.GetComponentInChildren<Animator>();
        if (src == null)
        {
            Debug.LogWarning("[调试] 没找到玩家模型，无法生成敌人");
            return;
        }

        Vector3 pos = root.position + root.forward * 15f;

        // 落到地面上
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 30f))
            pos = ground.point;

        // 敌人外壳，碰撞体和 Enemy 层
        debugEnemy = new GameObject("DebugEnemy");
        debugEnemy.layer = 8;
        debugEnemy.transform.position = pos;
        debugEnemy.transform.rotation = root.rotation * Quaternion.Euler(0f, 180f, 0f);
        debugEnemyStart = pos;

        // 克隆玩家模型作为外观，去掉模型自带碰撞体
        Transform model = Instantiate(src.transform, debugEnemy.transform);
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);

        // 碰撞体尺寸先照抄玩家自身，下一帧再按模型修正
        CapsuleCollider col = debugEnemy.AddComponent<CapsuleCollider>();
        col.isTrigger = true;                 // 触发器，不挡路

        CapsuleCollider srcCol = root.GetComponent<CapsuleCollider>();
        if (srcCol != null)
        {
            col.center = srcCol.center;
            col.height = srcCol.height;
            col.radius = srcCol.radius;
        }
        else
        {
            col.center = new Vector3(0f, 1.5f, 0f);
            col.height = 3f;
            col.radius = 0.8f;
        }

        debugNeedFit = true;

        // 挡墙，J 键抬起放下，高度取身高一半
        float wallHeight = col.height * 0.5f;
        debugWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debugWall.name = "DebugWall";
        debugWall.transform.position = pos - root.forward * 1.5f + Vector3.up * wallHeight * 0.5f;
        debugWall.transform.localScale = new Vector3(2.5f, wallHeight, 0.3f);
        debugWall.GetComponent<Collider>().isTrigger = true;   // 触发器，不挡路

        // 锁定点标记，去掉碰撞体避免影响射线
        debugMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugMarker.name = "DebugMarker";
        debugMarker.transform.localScale = Vector3.one * 0.15f;
        Destroy(debugMarker.GetComponent<Collider>());
        debugMarker.SetActive(false);

        Debug.Log($"[调试] 敌人已生成，碰撞体 高{col.height:F2} 半径{col.radius:F2} 中心{col.center.y:F2}，P 键重新生成，L 键移除，J 键抬起放下挡墙，K 键左右移动");
    }

    // 移除调试敌人
    void RemoveDebugAim()
    {
        if (debugEnemy != null) Destroy(debugEnemy);
        if (debugWall != null) Destroy(debugWall);
        if (debugMarker != null) Destroy(debugMarker);

        debugEnemy = null;
        debugWall = null;
        debugMarker = null;
    }

    // 按模型实际尺寸修正碰撞体
    void FitDebugCollider()
    {
        Renderer[] renderers = debugEnemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        CapsuleCollider col = debugEnemy.GetComponent<CapsuleCollider>();
        col.center = debugEnemy.transform.InverseTransformPoint(bounds.center);
        col.height = bounds.size.y;
        col.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;

        // 挡墙高度跟着身高走
        if (debugWall != null)
        {
            float h = col.height * 0.5f;
            debugWall.transform.localScale = new Vector3(2.5f, h, 0.3f);

            Vector3 wp = debugWall.transform.position;
            wp.y = debugEnemyStart.y + h * 0.5f;
            debugWall.transform.position = wp;
        }

        Debug.Log($"[调试] 碰撞体修正为 高{col.height:F2} 半径{col.radius:F2} 中心{col.center.y:F2}");
    }

    // 调试敌人与标记更新
    void Update()
    {
        if (uiFocuspos == null) return;   // 只处理本地玩家，非本地玩家拿不到准星UI

        // 生成后下一帧按模型尺寸修正碰撞体
        if (debugNeedFit)
        {
            debugNeedFit = false;
            FitDebugCollider();
        }

        // P 生成调试敌人，L 移除
        if (Input.GetKeyDown(KeyCode.P)) StartDebugAim();
        if (Input.GetKeyDown(KeyCode.L)) RemoveDebugAim();

        if (debugEnemy == null) return;

        // J 抬起放下挡墙
        if (Input.GetKeyDown(KeyCode.J))
        {
            Vector3 p = debugWall.transform.position;
            float h = debugWall.transform.localScale.y;
            p.y += p.y > h ? -h : h;
            debugWall.transform.position = p;
        }

        // K 敌人左右移动
        if (Input.GetKey(KeyCode.K))
        {
            debugStrafe += Time.deltaTime;
            debugEnemy.transform.position = debugEnemyStart
                + debugEnemy.transform.right * (Mathf.PingPong(debugStrafe * 2f, 6f) - 3f);
        }

        // 标记锁定点
        debugMarker.SetActive(hasTarget);
        if (hasTarget)
        {
            debugMarker.transform.position = targetPos;
            debugMarker.GetComponent<Renderer>().material.color = targetInRing ? Color.green : Color.yellow;
        }

        // 灰线为准星方向，青线为锁定方向
        Debug.DrawLine(MuzzlePosition, MuzzlePosition + AimDirection() * 15f, Color.gray);
        if (hasTarget) Debug.DrawLine(MuzzlePosition, targetPos, Color.cyan);
    }

    // 画出调试敌人的碰撞体
    void OnDrawGizmos()
    {
        if (debugEnemy == null) return;

        CapsuleCollider col = debugEnemy.GetComponent<CapsuleCollider>();
        if (col == null) return;

        Gizmos.color = Color.red;
        Gizmos.matrix = debugEnemy.transform.localToWorldMatrix;

        float radius = col.radius;
        float half = Mathf.Max(col.height * 0.5f - radius, 0f);
        Vector3 top = col.center + Vector3.up * half;
        Vector3 bottom = col.center - Vector3.up * half;

        // 上下半球
        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);

        // 连接线
        Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
        Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
        Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
        Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
    }

    // 调试信息显示
    void OnGUI()
    {
        if (uiFocuspos == null) return;   // 只画本地玩家

        GUI.Label(new Rect(10f, 10f, 1200f, 22f),
            $"锁定 {hasTarget}   在圈内 {targetInRing}   精度 {currentAngle:F2}度   " +
            $"外圈 {CurrentProfile().outerAngle:F1}   内圈 {CurrentProfile().innerAngle:F1}   增速 {CurrentProfile().growSpeed:F1}");
    }
}
