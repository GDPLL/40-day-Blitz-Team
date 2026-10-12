using System.Collections.Generic;
using UnityEngine;

// 透视表现，由 Player_Control 驱动
public class Player_XRay : MonoBehaviour
{
    [Header("透视开关")]
    public bool enableXRay = true;          // 总开关
    [Range(0, 31)] public int ghostLayer = 9;   // 红雾副本专用层，勿共用

    [Header("红雾外观")]
    public Color fogColor = new Color(1f, 0.05f, 0.05f, 1f);   // 红雾颜色
    [Range(0f, 8f)] public float intensity = 1.2f;             // 颜色亮度
    [Range(0.5f, 8f)] public float rimPower = 1.6f;            // 边缘锐度
    [Range(0f, 4f)] public float rimStrength = 0.7f;           // 边缘衰减
    [Range(0f, 1f)] public float fillAlpha = 0.7f;             // 内部不透明度
    public float maxDistance = 0f;                             // 最远显示距离，0 为不限
    public float checkRadius = 0.35f;                          // 遮挡检测球半径

    [Header("距离表现")]
    public float refDistance = 50f;                     // 基准距离，50 米对应 1 秒
    public float pingCurve = 1.5f;                      // 间隔曲线指数
    public float showTime = 0.25f;                      // 单次显示时长
    [Range(0f, 1f)] public float nearDensity = 0.4f;    // 近距离雾气浓度
    public float growStep = 1f;                         // 每基准距离外扩米数，0 为不外扩
    public float growMax = 3.5f;                        // 外扩上限米数

    [Header("远处雾团")]
    [Range(0f, 200f)] public float cardDistance = 40f;  // 超过此距离显示雾团
    public float cardScale = 0.12f;                     // 雾团尺寸与距离比例
    public float cardMax = 18f;                         // 雾团尺寸上限米数

    [Header("雾气细节")]
    [Range(0f, 3f)] public float minPartSize = 1f;      // 小于此尺寸的部件不建副本
    [Range(0f, 2f)] public float heightFade = 0.15f;    // 上下衰减，越大头顶越淡
    [Range(0.2f, 10f)] public float noiseScale = 3.5f;  // 雾团大小，越小团越大
    [Range(0f, 3f)] public float noiseSpeed = 0.35f;    // 飘动速度
    [Range(0.5f, 4f)] public float cardSoft = 1.5f;     // 雾团边缘软硬

    Camera viewCamera;                                         // 本机相机
    Camera xrayCamera;                                         // 红雾相机，清深度后单画副本
    int originMask;                                            // 主相机原始层遮罩
    LayerMask blockMask;                                       // 遮挡判定层
    Material xrayMaterial;                                     // 共用的透视材质
    Material cardMaterial;                                     // 雾团材质
    Mesh cardMesh;                                             // 雾团面片
    readonly Dictionary<Transform, List<GameObject>> ghosts = new Dictionary<Transform, List<GameObject>>();   // 目标与其红雾副本
    readonly Dictionary<Transform, bool> known = new Dictionary<Transform, bool>();   // 目标上次遮挡状态
    readonly List<Transform> want = new List<Transform>();     // 本帧应透视目标
    readonly Dictionary<Transform, float> pingTimer = new Dictionary<Transform, float>();   // 目标扫描计时
    MaterialPropertyBlock fogBlock;                            // 雾气参数块

    readonly Dictionary<Transform, Renderer[]> sources = new Dictionary<Transform, Renderer[]>();   // 目标源渲染器

    // 初始化材质与红雾相机，cam 为本机相机 mask 为敌人层
    public void Player_XRay_Init(Camera cam, LayerMask mask)
    {
        if (cam == null)
        {
            Debug.LogError("Player_XRay|Player_XRay_Init|相机为空");
            return;
        }
        if (ghostLayer < 0 || ghostLayer > 31)
        {
            Debug.LogError("Player_XRay|Player_XRay_Init|红雾层号超范围");
            return;
        }
        if (refDistance <= 0f || showTime <= 0f || growMax < 0f || pingCurve <= 0f || cardScale < 0f || cardMax < 0f)
        {
            Debug.LogError("Player_XRay|Player_XRay_Init|距离表现参数非法");
            return;
        }

        Shader shader = Resources.Load<Shader>("XRay_Fog");
        if (shader == null) shader = Shader.Find("XRay/RedFog");
        if (shader == null)
        {
            Debug.LogError("Player_XRay|Player_XRay_Init|未找到 XRay_Fog 着色器");
            return;
        }

        viewCamera = cam;
        blockMask = ~mask;                  // 敌人层不参与遮挡判定
        xrayMaterial = new Material(shader);
        xrayMaterial.renderQueue = 3010;
        fogBlock = new MaterialPropertyBlock();    // 原生对象只能在这里创建

        cardMaterial = new Material(shader);
        cardMaterial.EnableKeyword("_FOG_CARD");   // 切成面片模式
        cardMaterial.renderQueue = 3010;
        cardMesh = BuildCardMesh();

        // 红雾相机，清深度后单画副本
        GameObject go = new GameObject("XRay_Camera");
        go.transform.SetParent(cam.transform, false);

        xrayCamera = go.AddComponent<Camera>();
        xrayCamera.clearFlags = CameraClearFlags.Depth;
        xrayCamera.cullingMask = 1 << ghostLayer;
        xrayCamera.depth = cam.depth + 1;
        xrayCamera.nearClipPlane = cam.nearClipPlane;
        xrayCamera.farClipPlane = cam.farClipPlane;
        xrayCamera.fieldOfView = cam.fieldOfView;

        originMask = cam.cullingMask;
        cam.cullingMask = originMask & ~(1 << ghostLayer);   // 主相机不画副本

        Debug.Log("Player_XRay|Player_XRay_Init|完成初始化");
    }

    // 刷新透视，players 为全部玩家 localClientId 为本机编号
    public void Player_XRay_Show(List<Player_Control> players, ulong localClientId)
    {
        if (xrayMaterial == null)
        {
            Debug.LogError("Player_XRay|Player_XRay_Show|透视材质为空");
            return;
        }
        if (players == null)
        {
            Debug.LogError("Player_XRay|Player_XRay_Show|玩家列表为空");
            return;
        }

        if (xrayCamera == null || viewCamera == null)
        {
            Debug.LogError("Player_XRay|Player_XRay_Show|相机为空");
            return;
        }

        // 红雾相机跟随主相机的视角变化
        xrayCamera.fieldOfView = viewCamera.fieldOfView;
        xrayCamera.nearClipPlane = viewCamera.nearClipPlane;
        xrayCamera.farClipPlane = viewCamera.farClipPlane;

        want.Clear();
        if (enableXRay)
        {
            for (int i = 0; i < players.Count; i++)
            {
                Player_Control player = players[i];
                if (player == null) continue;
                if (player.OwnerClientId == localClientId) continue;   // 自己不看自己
                want.Add(player.transform);
            }
        }

        // 新目标建红雾副本
        for (int i = 0; i < want.Count; i++)
        {
            if (ghosts.ContainsKey(want[i])) continue;
            Collect(want[i], want[i].GetComponentsInChildren<Renderer>(true));
        }

        DropStale();

        // 按遮挡刷新副本显隐
        int shown = 0;
        for (int i = 0; i < want.Count; i++)
        {
            if (RefreshVisible(want[i])) shown++;
        }

        // 无副本显示时停掉红雾相机
        if (xrayCamera.enabled != (shown > 0)) xrayCamera.enabled = shown > 0;
    }

    // 销毁全部副本与红雾相机
    public void Player_XRay_Clear()
    {
        List<Transform> keys = new List<Transform>(ghosts.Keys);
        for (int i = 0; i < keys.Count; i++) Drop(keys[i]);

        if (xrayCamera != null)
        {
            Destroy(xrayCamera.gameObject);
            xrayCamera = null;
        }
        if (viewCamera != null)
        {
            viewCamera.cullingMask = originMask;
            viewCamera = null;
        }
        if (xrayMaterial != null)
        {
            Destroy(xrayMaterial);
            xrayMaterial = null;
        }
        if (cardMaterial != null)
        {
            Destroy(cardMaterial);
            cardMaterial = null;
        }
        if (cardMesh != null)
        {
            Destroy(cardMesh);
            cardMesh = null;
        }
    }

    // 给目标建红雾副本，renderers 为目标全部渲染器
    void Collect(Transform target, Renderer[] renderers)
    {
        List<GameObject> list = new List<GameObject>();

        for (int i = 0; i < renderers.Length; i++)
        {
            // 小于此尺寸的部件不建副本
            if (renderers[i].bounds.size.magnitude < minPartSize) continue;

            SkinnedMeshRenderer skin = renderers[i] as SkinnedMeshRenderer;
            if (skin != null)
            {
                if (skin.sharedMesh == null)
                {
                    Debug.LogError("Player_XRay|Collect|蒙皮网格为空");
                    continue;
                }
                list.Add(BuildSkinGhost(skin));
                continue;
            }

            MeshRenderer mesh = renderers[i] as MeshRenderer;
            if (mesh == null) continue;

            GameObject ghost = BuildMeshGhost(mesh);
            if (ghost != null) list.Add(ghost);
        }

        if (list.Count == 0) Debug.LogError("Player_XRay|Collect|没有可用渲染器");

        // 远处雾团面片
        Vector3 center = BodyCenter(target, renderers);
        sources[target] = renderers;

        GameObject card = new GameObject("XRay_Card");
        card.layer = ghostLayer;
        card.transform.SetParent(target, false);
        card.transform.localPosition = center;
        card.SetActive(false);

        card.AddComponent<MeshFilter>().sharedMesh = cardMesh;
        card.AddComponent<MeshRenderer>().sharedMaterial = cardMaterial;

        list.Add(card);

        ghosts[target] = list;
        known[target] = false;      // 首次扫描前不显示
        pingTimer[target] = 0f;
    }

    // 取模型包围盒中点，返回局部坐标
    Vector3 BodyCenter(Transform target, Renderer[] renderers)
    {
        if (renderers.Length == 0) return new Vector3(0f, 1.2f, 0f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return target.InverseTransformPoint(bounds.center);
    }

    // 皮肤网格副本，共用源骨骼
    GameObject BuildSkinGhost(SkinnedMeshRenderer source)
    {
        GameObject go = new GameObject("XRay_Ghost");
        go.layer = ghostLayer;
        go.transform.SetParent(source.transform, false);
        go.SetActive(false);

        SkinnedMeshRenderer ghost = go.AddComponent<SkinnedMeshRenderer>();
        ghost.sharedMesh = source.sharedMesh;
        ghost.bones = source.bones;
        ghost.rootBone = source.rootBone;
        ghost.updateWhenOffscreen = true;
        ghost.sharedMaterials = RedMaterials(source.sharedMaterials.Length);

        return go;
    }

    // 普通网格副本
    GameObject BuildMeshGhost(MeshRenderer source)
    {
        MeshFilter filter = source.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            Debug.LogError("Player_XRay|BuildMeshGhost|网格为空");
            return null;
        }

        GameObject go = new GameObject("XRay_Ghost");
        go.layer = ghostLayer;
        go.transform.SetParent(source.transform, false);
        go.SetActive(false);

        go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = RedMaterials(source.sharedMaterials.Length);

        return go;
    }

    // 生成雾团面片，正反两面
    Mesh BuildCardMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "XRay_Card";

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f)
        };
        mesh.triangles = new int[]
        {
            0, 2, 1, 2, 3, 1,
            0, 1, 2, 2, 1, 3
        };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 40f);   // 放大包围盒，避免面片被剔除

        return mesh;
    }

    // 生成全红雾材质组，count 为子网格数
    Material[] RedMaterials(int count)
    {
        if (count < 1) count = 1;

        Material[] mats = new Material[count];
        for (int i = 0; i < count; i++) mats[i] = xrayMaterial;
        return mats;
    }

    // 移除失效目标的副本
    void DropStale()
    {
        List<Transform> stale = null;
        foreach (var kv in ghosts)
        {
            if (want.Contains(kv.Key)) continue;
            if (stale == null) stale = new List<Transform>();
            stale.Add(kv.Key);
        }

        if (stale == null) return;
        for (int i = 0; i < stale.Count; i++) Drop(stale[i]);
    }

    // 销毁目标的副本
    void Drop(Transform target)
    {
        if (!ghosts.TryGetValue(target, out List<GameObject> list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) Destroy(list[i]);
        }

        ghosts.Remove(target);
        known.Remove(target);
        pingTimer.Remove(target);
        sources.Remove(target);
    }

    // 按距离与遮挡刷新副本，返回是否显示
    bool RefreshVisible(Transform target)
    {
        if (!ghosts.TryGetValue(target, out List<GameObject> list)) return false;
        if (target == null)
        {
            Debug.LogError("Player_XRay|RefreshVisible|目标为空");
            return false;
        }
        float dist = Vector3.Distance(viewCamera.transform.position, target.position);
        float unit = dist / refDistance;                  // 50 米为 1 单位
        float interval = Mathf.Pow(unit, pingCurve);      // 指数越大远处越慢
        float timer = pingTimer[target] + Time.deltaTime;

        // 到点扫一次遮挡
        if (timer >= interval)
        {
            timer -= interval;

            // 按扫描时的姿势重采中点
            if (sources.TryGetValue(target, out Renderer[] sourceList))
                known[target] = IsHidden(viewCamera, blockMask, target.TransformPoint(BodyCenter(target, sourceList)));
            else
                Debug.LogError("Player_XRay|RefreshVisible|源渲染器缺失");
        }
        pingTimer[target] = timer;

        // 没扫到遮挡就不显示
        if (!known[target])
        {
            SetGhostShow(list, false);
            return false;
        }

        // 单次显示时间内淡入淡出
        float fade = FlashFade(timer);
        if (fade <= 0f)
        {
            SetGhostShow(list, false);
            return false;
        }

        ApplyFog(list, dist, fade, target.position);
        SetGhostShow(list, true);
        return true;
    }

    // 单次显示内的淡入淡出，t 为本次已显示时长
    float FlashFade(float t)
    {
        if (t >= showTime) return 0f;

        float half = showTime * 0.5f;
        float k = t < half ? t / half : (showTime - t) / half;
        return Mathf.Clamp01(k);
    }

    // 写入雾气参数，dist 为相机距离 fade 为透明度 origin 为目标位置
    void ApplyFog(List<GameObject> list, float dist, float fade, Vector3 origin)
    {
        float unit = dist / refDistance;

        fogBlock.SetColor("_FogColor", fogColor);
        fogBlock.SetFloat("_Intensity", intensity);
        fogBlock.SetFloat("_RimPower", rimPower);
        fogBlock.SetFloat("_RimStrength", rimStrength);
        fogBlock.SetFloat("_FillAlpha", fillAlpha);
        fogBlock.SetFloat("_MaxDistance", maxDistance);
        fogBlock.SetVector("_MistOrigin", origin);
        fogBlock.SetFloat("_Expand", Mathf.Min(growStep * unit, growMax));
        fogBlock.SetFloat("_CardSize", Mathf.Min(dist * cardScale, cardMax));
        fogBlock.SetFloat("_CardFade", Mathf.Clamp01((dist - cardDistance) / Mathf.Max(cardDistance, 1f)));
        fogBlock.SetFloat("_Density", Mathf.Lerp(nearDensity, 1f, Mathf.Clamp01(unit)));
        fogBlock.SetFloat("_Fade", fade);
        fogBlock.SetFloat("_HeightFade", heightFade);
        fogBlock.SetFloat("_NoiseScale", noiseScale);
        fogBlock.SetFloat("_NoiseSpeed", noiseSpeed);
        fogBlock.SetFloat("_CardSoft", cardSoft);

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;

            Renderer renderer = list[i].GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("Player_XRay|ApplyFog|渲染器为空");
                continue;
            }

            renderer.SetPropertyBlock(fogBlock);
        }
    }

    // 设置副本显隐
    void SetGhostShow(List<GameObject> list, bool show)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            if (list[i].activeSelf != show) list[i].SetActive(show);
        }
    }

    // 判断遮挡，cam 为观察相机 mask 为遮挡层 point 为检查点
    bool IsHidden(Camera cam, LayerMask mask, Vector3 point)
    {
        if (cam == null)
        {
            Debug.LogError("Player_XRay|IsHidden|相机为空");
            return false;
        }

        Vector3 start = cam.transform.position;
        Vector3 dir = point - start;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
        {
            Debug.LogError("Player_XRay|IsHidden|目标与相机重合");
            return false;
        }

        return Physics.SphereCast(start, checkRadius, dir / dist, out RaycastHit _,
            dist, mask, QueryTriggerInteraction.Ignore);
    }
}
