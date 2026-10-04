using UnityEngine;

// 临时调试敌人，测完删除
public class Debug_Enemy : MonoBehaviour
{
    GameObject enemy;     // 调试敌人
    Player_Main owner;    // 已生成过的关卡
    bool needFit;         // 待修正碰撞体

    // 进场景自动挂载
    [RuntimeInitializeOnLoadMethod]
    static void Debug_Enemy_Init()
    {
        GameObject go = new GameObject("Debug_Enemy");
        DontDestroyOnLoad(go);
        go.AddComponent<Debug_Enemy>();
    }

    // 关卡就绪后生成，P 重新生成，L 移除
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
        }
        else
        {
            // 没有模型时用胶囊代替
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Debug_Enemy_Body";
            body.transform.SetParent(enemy.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Destroy(body.GetComponent<Collider>());
        }

        CapsuleCollider col = enemy.AddComponent<CapsuleCollider>();
        col.isTrigger = false;                   // 实体碰撞
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.4f;

        enemy.AddComponent<Object_System>();     // 受击掉血

        // 敌人身上挂光，暗处也能看清
        GameObject lamp = new GameObject("Debug_Enemy_Lamp");
        lamp.transform.SetParent(enemy.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 1.8f, 0f);

        Light glow = lamp.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.6f);
        glow.range = 8f;
        glow.intensity = 3f;

        needFit = true;

        Debug.Log("Debug_Enemy|Spawn|敌人已生成，P 重新生成，L 移除");
    }

    // 按模型尺寸修正碰撞体
    void FitCollider()
    {
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        CapsuleCollider col = enemy.GetComponent<CapsuleCollider>();
        col.center = enemy.transform.InverseTransformPoint(bounds.center);
        col.height = bounds.size.y;
        col.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f, 0.25f, 0.5f);   // 限幅，别变成隐形墙

        Debug.Log($"Debug_Enemy|FitCollider|高{col.height:F2} 半径{col.radius:F2} 中心{col.center.y:F2}");
    }

    // 移除敌人
    void Remove()
    {
        if (enemy != null) Destroy(enemy);

        enemy = null;
        needFit = false;
    }
}
