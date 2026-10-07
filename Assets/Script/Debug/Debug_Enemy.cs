using UnityEngine;
using Unity.Netcode;

// 临时调试敌人，测完删除
public class Debug_Enemy : MonoBehaviour
{
    GameObject enemy;     // 调试敌人
    Transform modelRoot;  // 外观模型
    Player_Main owner;    // 已生成过的关卡
    bool needFit;         // 待修正碰撞体
    static Material lineMaterial;   // 弹道材质

    // 开火参数
    public float fireRange = 30f;     // 开火距离
    public int fireDamage = 10;       // 单发伤害
    LineRenderer fireLine;            // 弹道显示
    float lineTimer;                  // 弹道计时

    // 进场景自动挂载
    [RuntimeInitializeOnLoadMethod]
    static void Debug_Enemy_Init()
    {
        GameObject go = new GameObject("Debug_Enemy");
        DontDestroyOnLoad(go);
        go.AddComponent<Debug_Enemy>();
    }

    // 关卡就绪后生成
    void Update()
    {
        Player_Main main = Player_Main.player_Main;
        if (main == null || !main.isInit) return;

        if (owner != main)
        {
            owner = main;
            Spawn();
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            Remove();
            Spawn();
        }
        else if (Input.GetKeyDown(KeyCode.L)) Remove();

        // 生成后按模型尺寸修正碰撞体
        if (needFit && enemy != null)
        {
            needFit = false;
            FitCollider();
        }

        // 弹道计时隐藏
        if (fireLine != null && fireLine.enabled)
        {
            lineTimer -= Time.deltaTime;
            if (lineTimer <= 0f) fireLine.enabled = false;
        }

        // V键向前开枪
        if (Input.GetKeyDown(KeyCode.V)) Fire();
    }

    // 在出生点前方生成敌人
    void Spawn()
    {
        Transform[] points = Player_Main.player_Main.respawnPoints;
        if (points == null || points.Length == 0 || points[0] == null)
        {
            Debug.LogError("Debug_Enemy|Spawn|复活点未绑定");
            return;
        }

        Transform point = points[0];
        Vector3 pos = point.position + point.forward * 15f;

        // 落到地面
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 50f))
            pos = ground.point;

        enemy = new GameObject("Debug_Enemy_Target");
        enemy.layer = 8;      // Enemy 层
        enemy.transform.SetPositionAndRotation(pos, point.rotation * Quaternion.Euler(0f, 180f, 0f));

        // 复用本机玩家模型作为外观
        Player_Control player = Player_Main.player_Main.oPlayer_Control;
        Animator model = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (model != null)
        {
            Transform clone = Instantiate(model.transform, enemy.transform);
            foreach (Collider c in clone.GetComponentsInChildren<Collider>(true)) Destroy(c);
            modelRoot = clone;
        }
        else
        {
            // 没有模型时用胶囊代替
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Debug_Enemy_Body";
            body.transform.SetParent(enemy.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Destroy(body.GetComponent<Collider>());
            modelRoot = body.transform;
        }

        CapsuleCollider col = enemy.AddComponent<CapsuleCollider>();
        col.isTrigger = false;                   // 实体碰撞
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.4f;

        enemy.AddComponent<Object_System>();     // 受击掉血

        // 敌人身上挂光源
        GameObject lamp = new GameObject("Debug_Enemy_Lamp");
        lamp.transform.SetParent(enemy.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 1.8f, 0f);

        Light glow = lamp.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.6f);
        glow.range = 8f;
        glow.intensity = 3f;

        // 弹道显示
        fireLine = enemy.AddComponent<LineRenderer>();
        fireLine.startWidth = 0.03f;
        fireLine.endWidth = 0.03f;
        fireLine.startColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        fireLine.endColor = fireLine.startColor;
        fireLine.enabled = false;

        if (lineMaterial == null)
        {
            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader == null) Debug.LogError("Debug_Enemy|Spawn|未找到弹道着色器");
            else lineMaterial = new Material(lineShader);
        }
        if (lineMaterial != null) fireLine.material = lineMaterial;

        needFit = true;

        Debug.Log("Debug_Enemy|Spawn|敌人已生成，V 开枪，P 重新生成，L 移除");
    }

    // 按模型尺寸修正碰撞体
    void FitCollider()
    {
        if (modelRoot == null)
        {
            Debug.LogError("Debug_Enemy|FitCollider|外观模型为空");
            return;
        }

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        CapsuleCollider col = enemy.GetComponent<CapsuleCollider>();
        col.center = enemy.transform.InverseTransformPoint(bounds.center);
        col.height = bounds.size.y;
        col.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f, 0.25f, 0.5f);   // 半径限幅

        Debug.Log($"Debug_Enemy|FitCollider|高{col.height:F2} 半径{col.radius:F2} 中心{col.center.y:F2}");
    }

    // 向前开一枪
    void Fire()
    {
        if (enemy == null) return;

        CapsuleCollider col = enemy.GetComponent<CapsuleCollider>();
        Vector3 muzzle = col != null
            ? enemy.transform.TransformPoint(col.center)
            : enemy.transform.position + Vector3.up * 1f;

        Vector3 dir = enemy.transform.forward;
        Vector3 origin = muzzle + dir * 0.6f;
        Vector3 end = origin + dir * fireRange;

        bool isHit = Physics.Raycast(origin, dir, out RaycastHit hit, fireRange);
        if (isHit) end = hit.point;

        ShowLine(origin, end);

        if (!isHit) return;
        if (hit.collider.transform.IsChildOf(enemy.transform)) return;

        // 近处落点触发本机压制
        if (Player_Main.player_Main == null) Debug.LogError("Debug_Enemy|Fire|Player_Main 为空");
        else Player_Main.player_Main.Suppress_Check_Local(hit.point);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("Debug_Enemy|Fire|NetworkManager 为空");
            return;
        }
        if (!NetworkManager.Singleton.IsServer) return;   //主机结算伤害

        // 命中可伤害目标才结算
        Idamage damageable = hit.collider.GetComponent<Idamage>();
        if (damageable != null) damageable.Takedamage(fireDamage, enemy.transform.position);
    }

    // 显示弹道
    void ShowLine(Vector3 from, Vector3 to)
    {
        if (fireLine == null) return;

        fireLine.positionCount = 2;
        fireLine.SetPosition(0, from);
        fireLine.SetPosition(1, to);
        fireLine.enabled = true;
        lineTimer = 0.05f;
    }

    // 移除敌人
    void Remove()
    {
        if (enemy != null) Destroy(enemy);

        enemy = null;
        modelRoot = null;
        fireLine = null;
        needFit = false;
    }
}
