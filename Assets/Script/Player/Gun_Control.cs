using UnityEngine;

public class Gun_Control : MonoBehaviour
{
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

    float fireTimer;               // 射速计时
    float lineTimer;               // 射线显示计时
    float lastShotTime;            // 上次射击时间

    public RectTransform uiFocuspos;      // 中心UI

    void Start()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null) line.enabled = false;
        originalRecoilX = recoilX;   // 记录原始后坐力（肩射减后坐力恢复用）
        originalRecoilY = recoilY;
    }

    void FixedUpdate()
    {
        fireTimer += Time.deltaTime;
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
        Vector3 origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 1f;
        Vector3 aim = target != null ? target.position + Vector3.up * 1f : origin + transform.forward * 10f;

        // 随机二维后坐力上抬累计（X 左右随机、Y 只上抬）
        recoil += new Vector2(Random.Range(-recoilX, recoilX), Random.Range(0f, recoilY));
        recoil = Vector2.ClampMagnitude(recoil, maxRecoil);   // 钳制到后坐力上限
        // 恢复速率：让当前累计后坐力在「射击间隙 × recoilRecoverFactor」内线性归零
        recoilRecoverSpeed = recoil.magnitude / (recoilRecoverFactor / fireRate);

        // 射击方向 = 基础朝向 + 后坐力偏移（水平 / 上抬），再归一化
        Vector3 dir = (aim - origin).normalized + transform.right * recoil.x + transform.up * recoil.y;
        dir.Normalize();

        // 在开火点生成对象
        if (fireEffect != null)
            Instantiate(fireEffect, origin, Quaternion.LookRotation(dir));

        Vector3 end = origin + dir * range;                         // 默认射程终点
        if (Physics.Raycast(origin, dir, out RaycastHit hit, range))
        {
            end = hit.point;                                        // 命中落点
            Debug.Log($"{end},{hit.collider.gameObject.name}");

            // 在落点生成对象
            if (hitEffect != null)
            {
                Vector3 bounceDir = Vector3.Reflect(dir, hit.normal);
                if (bounceDir.sqrMagnitude <= 0.001f) bounceDir = -hit.normal;   // 极端角度兜底
                Instantiate(hitEffect, hit.point, Quaternion.LookRotation(bounceDir));
            }

            // 获取命中对象上的 Idamage 接口，调用受伤逻辑并造成伤害
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
}
