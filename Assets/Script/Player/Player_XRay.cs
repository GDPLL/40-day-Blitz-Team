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
    [Range(0.5f, 8f)] public float rimPower = 2.5f;            // 边缘锐度
    [Range(0f, 4f)] public float rimStrength = 1.6f;           // 边缘强度
    [Range(0f, 1f)] public float fillAlpha = 0.5f;             // 内部不透明度
    public float maxDistance = 0f;                             // 最远显示距离，0 为不限
    public float chestHeight = 1.2f;                           // 遮挡检测高度
    public float checkRadius = 0.35f;                          // 遮挡检测球半径

    Camera viewCamera;                                         // 本机相机
    Camera xrayCamera;                                         // 红雾相机，清深度后单画副本
    int originMask;                                            // 主相机原始层遮罩
    LayerMask blockMask;                                       // 遮挡判定层
    Material xrayMaterial;                                     // 共用的透视材质
    readonly Dictionary<Transform, List<GameObject>> ghosts = new Dictionary<Transform, List<GameObject>>();   // 目标与其红雾副本
    readonly Dictionary<Transform, bool> known = new Dictionary<Transform, bool>();   // 目标上次遮挡状态
    readonly List<Transform> want = new List<Transform>();     // 本帧应透视目标

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

        ApplyMaterialParams();

        // 红雾相机跟随主相机的视角变化
        if (xrayCamera != null && viewCamera != null)
        {
            xrayCamera.fieldOfView = viewCamera.fieldOfView;
            xrayCamera.nearClipPlane = viewCamera.nearClipPlane;
            xrayCamera.farClipPlane = viewCamera.farClipPlane;
        }

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
        if (xrayCamera != null && xrayCamera.enabled != (shown > 0)) xrayCamera.enabled = shown > 0;
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
    }

    // 写入材质参数
    void ApplyMaterialParams()
    {
        xrayMaterial.SetColor("_FogColor", fogColor);
        xrayMaterial.SetFloat("_Intensity", intensity);
        xrayMaterial.SetFloat("_RimPower", rimPower);
        xrayMaterial.SetFloat("_RimStrength", rimStrength);
        xrayMaterial.SetFloat("_FillAlpha", fillAlpha);
        xrayMaterial.SetFloat("_MaxDistance", maxDistance);
    }

    // 给目标建红雾副本，renderers 为目标全部渲染器
    void Collect(Transform target, Renderer[] renderers)
    {
        List<GameObject> list = new List<GameObject>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer skin = renderers[i] as SkinnedMeshRenderer;
            if (skin != null)
            {
                if (skin.sharedMesh == null) continue;
                list.Add(BuildSkinGhost(skin));
                continue;
            }

            MeshRenderer mesh = renderers[i] as MeshRenderer;
            if (mesh == null) continue;

            GameObject ghost = BuildMeshGhost(mesh);
            if (ghost != null) list.Add(ghost);
        }

        ghosts[target] = list;
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
        if (filter == null || filter.sharedMesh == null) return null;

        GameObject go = new GameObject("XRay_Ghost");
        go.layer = ghostLayer;
        go.transform.SetParent(source.transform, false);
        go.SetActive(false);

        go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = RedMaterials(source.sharedMaterials.Length);

        return go;
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
    }

    // 按遮挡刷新副本显隐，返回是否显示
    bool RefreshVisible(Transform target)
    {
        if (!ghosts.TryGetValue(target, out List<GameObject> list)) return false;

        bool hidden = IsHidden(viewCamera, blockMask, target);
        if (known.TryGetValue(target, out bool last) && last == hidden) return hidden;

        known[target] = hidden;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) list[i].SetActive(hidden);
        }

        return hidden;
    }

    // 判断目标是否被挡住，cam 为观察相机 mask 为遮挡层
    bool IsHidden(Camera cam, LayerMask mask, Transform target)
    {
        if (cam == null)
        {
            Debug.LogError("Player_XRay|IsHidden|相机为空");
            return false;
        }
        if (target == null)
        {
            Debug.LogError("Player_XRay|IsHidden|目标为空");
            return false;
        }

        Vector3 start = cam.transform.position;
        Vector3 end = target.position + Vector3.up * chestHeight;
        Vector3 dir = end - start;
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
