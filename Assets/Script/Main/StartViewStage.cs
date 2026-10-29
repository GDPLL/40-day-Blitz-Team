using UnityEngine;

// 开始场景四号展示区，只在本机创建摄像机与布景
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class StartViewStage : MonoBehaviour
{
    const int SlotCount = 4;
    const int CurrentLayoutVersion = 16;

    readonly Camera[] slotCameras = new Camera[SlotCount];
    readonly Transform[] playerAnchors = new Transform[SlotCount];
    readonly float[] playerLaterals = { -3f, -0.8f, 1.6f, 3.4f };
    readonly float[] playerHeights = { 1.28f, 0f, 0f, 0f };
    readonly float[] cameraLaterals = { -0.55f, 0.25f, 2.4f, 1f };
    readonly float[] cameraDepths = { 0.2f, -3.4f, 2.2f, 2.8f };
    readonly float[] cameraHeights = { 3.1f, 0.45f, 2f, 1.4f };
    readonly float[] lookLaterals = { 0f, -0.2f, 0.7f, 0f };
    readonly float[] lookHeights = { 2.05f, 1.15f, 1.6f, 1.05f };
    readonly float[] lookDepths = { 0f, 0f, -4f, -4f };
    readonly float[] cameraFovs = { 88f, 58f, 52f, 44f };

    Camera overviewCamera;
    Transform stageRoot;
    Vector3 anchor;
    Vector3 stageRight;
    Vector3 stageForward;
    int shownSlot = -1;
    bool isBuilt;
    [SerializeField, HideInInspector] int layoutVersion;

    public bool IsBuilt => isBuilt;

    // 编辑器重建并替换现有展示区
    public void RebuildLayout(Vector3 origin)
    {
        Transform oldRoot = transform.Find("开始场景_四号展示区");
        if (oldRoot != null) DestroyImmediate(oldRoot.gameObject);

        layoutVersion = 0;
        isBuilt = false;
        BuildStage(origin);
    }

    // 编辑模式生成场景对象，便于手动调整
    void OnEnable()
    {
        if (Application.isPlaying) return;

        Start_Main startMain = GetComponent<Start_Main>();
        if (startMain != null) BuildStage(startMain.StartVector3);
    }

    // 建立四机位与展示布景，origin为玩家排列中心
    public void BuildStage(Vector3 origin)
    {
        if (isBuilt) return;

        isBuilt = true;
        anchor = origin;
        overviewCamera = Camera.main;

        if (overviewCamera != null)
        {
            stageRight = overviewCamera.transform.right;
            stageForward = overviewCamera.transform.forward;
        }
        else
        {
            stageRight = Vector3.back;
            stageForward = Vector3.left;
            Debug.LogError("StartViewStage|BuildStage|未找到主摄像机");
        }

        stageRight.y = 0f;
        stageForward.y = 0f;
        stageRight.Normalize();
        stageForward.Normalize();

        Transform existingRoot = transform.Find("开始场景_四号展示区");
        if (existingRoot != null && layoutVersion < CurrentLayoutVersion)
        {
            DestroyImmediate(existingRoot.gameObject);
            existingRoot = null;
        }

        if (existingRoot != null)
        {
            stageRoot = existingRoot;
            BindExistingStage();
            return;
        }

        GameObject root = new GameObject("开始场景_四号展示区");
        stageRoot = root.transform;
        stageRoot.SetParent(transform, false);
        layoutVersion = CurrentLayoutVersion;

        BuildPlayerAnchors();
        BuildEnvironment();
        BuildCrate();
        BuildCover(2, 1.4f, false);
        BuildCover(3, 3.6f, true);
        BuildLights();

        for (int i = 0; i < SlotCount; i++)
            BuildCamera(i);

        Debug.Log("StartViewStage|BuildStage|四号展示区建立完成");
    }

    // 读取场景中已存在的机位和出生点
    void BindExistingStage()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            playerAnchors[i] = stageRoot.Find($"玩家{i + 1}出生点");
            Transform cameraTransform = stageRoot.Find($"玩家{i + 1}视角");
            slotCameras[i] = cameraTransform != null
                ? cameraTransform.GetComponent<Camera>() : null;

            if (playerAnchors[i] == null) BuildPlayerAnchor(i);
            if (slotCameras[i] == null) BuildCamera(i);

            // 旧生成对象朝向场景深处时，统一转回门一侧
            if (i > 0 && playerAnchors[i] != null
                && Vector3.Dot(playerAnchors[i].forward, stageForward) > 0.99f)
                playerAnchors[i].rotation =
                    Quaternion.LookRotation(-stageForward, Vector3.up)
                    * Quaternion.Euler(0f, -90f, 0f);
        }

        Debug.Log("StartViewStage|BindExistingStage|已接管场景展示对象");
    }

    // 建立可编辑的玩家出生点
    void BuildPlayerAnchors()
    {
        for (int i = 0; i < SlotCount; i++)
            BuildPlayerAnchor(i);
    }

    void BuildPlayerAnchor(int slot)
    {
        GameObject anchorObject = new GameObject($"玩家{slot + 1}出生点");
        playerAnchors[slot] = anchorObject.transform;
        playerAnchors[slot].SetParent(stageRoot, false);
        playerAnchors[slot].position = anchor
            + stageRight * playerLaterals[slot]
            + Vector3.up * playerHeights[slot];
        playerAnchors[slot].rotation = Quaternion.LookRotation(
            -stageForward, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);
    }

    // 按玩家编号显示对应视角
    public void ShowSlot(int slot)
    {
        if (!isBuilt)
        {
            Debug.LogError("StartViewStage|ShowSlot|展示区尚未建立");
            return;
        }

        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        if (shownSlot == slot) return;
        shownSlot = slot;

        if (overviewCamera != null) overviewCamera.enabled = false;

        for (int i = 0; i < slotCameras.Length; i++)
        {
            if (slotCameras[i] != null)
                slotCameras[i].enabled = i == slot;
        }

        Debug.Log($"StartViewStage|ShowSlot|切换到玩家{slot + 1}视角");
    }

    // 没有本机玩家时回到全景镜头
    public void ShowOverview()
    {
        shownSlot = -1;

        for (int i = 0; i < slotCameras.Length; i++)
        {
            if (slotCameras[i] != null)
                slotCameras[i].enabled = false;
        }

        if (overviewCamera != null) overviewCamera.enabled = true;
    }

    // 按席位返回玩家生成位置
    public Vector3 GetPlayerPosition(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        if (playerAnchors[slot] != null)
            return playerAnchors[slot].position;

        return anchor
            + stageRight * playerLaterals[slot]
            + Vector3.up * playerHeights[slot];
    }

    // 返回席位脚底高度，一号位为箱顶
    public float GetPlayerSurfaceHeight(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        if (playerAnchors[slot] != null)
            return playerAnchors[slot].position.y - (slot == 0 ? 0.05f : 0f);

        return anchor.y + (slot == 0 ? 1.23f : 0f);
    }

    // 全员面向门一侧
    public Quaternion GetPlayerRotation(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        if (playerAnchors[slot] != null)
            return playerAnchors[slot].rotation;

        if (stageForward.sqrMagnitude <= 0.001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(-stageForward, Vector3.up)
            * Quaternion.Euler(0f, -90f, 0f);
    }

    // 展示席位的水平瞄准方向
    public Vector3 GetAimDirection(int slot)
    {
        return -stageForward;
    }

    // 生成场景地面与背墙
    void BuildEnvironment()
    {
        Quaternion stageRotation = Quaternion.LookRotation(stageForward, Vector3.up);
        Material floorMaterial = LoadMaterial("模型/材质/水泥地", new Color(0.12f, 0.14f, 0.16f));
        Material wallMaterial = LoadMaterial("模型/材质/工地_工具钢", new Color(0.16f, 0.18f, 0.2f));
        Material edgeMaterial = LoadMaterial("模型/材质/工地_工具钢", new Color(0.24f, 0.27f, 0.3f));

        CreateCube("展示区地面",
            anchor + stageForward * 2f + Vector3.down * 0.08f,
            new Vector3(10f, 0.16f, 8f),
            stageRotation, floorMaterial);

        CreateCube("展示区背墙",
            anchor + stageForward * 4.8f + Vector3.up * 2.2f,
            new Vector3(12f, 4.4f, 0.25f),
            stageRotation, wallMaterial);

        CreateCube("背墙顶部压边",
            anchor + stageForward * 4.62f + Vector3.up * 3.85f,
            new Vector3(12f, 0.18f, 0.4f),
            stageRotation, edgeMaterial);

    }

    // 一号位箱顶
    void BuildCrate()
    {
        Quaternion stageRotation = Quaternion.LookRotation(stageForward, Vector3.up);
        Material crateMaterial = LoadMaterial("模型/材质/粗糙木板", new Color(0.3f, 0.21f, 0.12f));
        Material metalMaterial = LoadMaterial("模型/材质/钢铁银", new Color(0.3f, 0.32f, 0.34f));
        Vector3 center = anchor + stageRight * -3f + Vector3.up * 0.58f;

        CreateCube("一号位_箱体", center, new Vector3(2.3f, 1.15f, 2.1f),
            stageRotation, crateMaterial);
        CreateCube("一号位_顶部钢板", center + Vector3.up * 0.61f,
            new Vector3(2.4f, 0.08f, 2.2f), stageRotation, metalMaterial);

        for (int i = -1; i <= 1; i++)
        {
            CreateCube($"一号位_箱体横带_{i + 2}",
                center + stageRight * (i * 0.68f) + stageForward * -1.08f,
                new Vector3(0.12f, 0.92f, 0.04f),
                stageRotation, metalMaterial);
        }
    }

    // 三、四号位掩体，右侧使用交错堆叠
    void BuildCover(int slot, float lateral, bool staggered)
    {
        Quaternion stageRotation = Quaternion.LookRotation(stageForward, Vector3.up);
        Material coverMaterial = LoadMaterial("模型/材质/工地_工具钢", new Color(0.24f, 0.26f, 0.28f));
        Material topMaterial = LoadMaterial("模型/材质/工地_工具钢", new Color(0.34f, 0.36f, 0.38f));
        Vector3 center = anchor + stageRight * lateral - stageForward * 1.15f
            + Vector3.up * 0.2f;

        if (!staggered)
        {
            CreateCube("三号位_实心掩体", center + Vector3.up * 0.45f,
                new Vector3(2.2f, 1.3f, 0.58f),
                stageRotation, coverMaterial);
            CreateCube("三号位_顶部钢梁",
                center + Vector3.up * 1.15f,
                new Vector3(2.2f, 0.18f, 0.64f), stageRotation, topMaterial);
        }
        else
        {
            Vector3 frameBase = anchor + stageRight * lateral
                - stageForward * 1.15f;
            CreateCube("四号位_底部挡墙",
                frameBase + Vector3.up * 0.5f,
                new Vector3(2.2f, 1f, 0.58f), stageRotation, coverMaterial);
            CreateCube("四号位_左整框",
                frameBase + stageRight * -0.775f + Vector3.up * 0.8f,
                new Vector3(0.65f, 1.6f, 0.62f), stageRotation, coverMaterial);
            CreateCube("四号位_右整框",
                frameBase + stageRight * 0.775f + Vector3.up * 0.8f,
                new Vector3(0.65f, 1.6f, 0.62f), stageRotation, coverMaterial);
            CreateCube("四号位_顶部钢梁",
                frameBase + Vector3.up * 1.7f,
                new Vector3(2.2f, 0.2f, 0.66f), stageRotation, topMaterial);
        }
    }

    // 暗区轮廓光与场地主光
    void BuildLights()
    {
        CreatePointLight("展示区主光",
            anchor + stageForward * 1.5f + Vector3.up * 4.5f,
            new Color(0.9f, 0.82f, 0.68f), 2.2f, 9f);
        CreatePointLight("三号位轮廓光",
            anchor + stageRight * 1.6f + stageForward * 1.8f + Vector3.up * 1.7f,
            new Color(0.95f, 0.7f, 0.42f), 2.4f, 4f);
        CreatePointLight("四号位轮廓光",
            anchor + stageRight * 3.4f + stageForward * 1.8f + Vector3.up * 1.55f,
            new Color(0.95f, 0.7f, 0.42f), 2.1f, 4f);
    }

    // 建立固定视角，不挂玩家跟随控制
    void BuildCamera(int slot)
    {
        if (slotCameras[slot] != null) return;

        Vector3 playerPosition = GetPlayerPosition(slot);
        Vector3 cameraPosition = playerPosition
            + stageRight * cameraLaterals[slot]
            + stageForward * cameraDepths[slot]
            + Vector3.up * cameraHeights[slot];
        Vector3 lookPosition = playerPosition
            + stageRight * lookLaterals[slot]
            + stageForward * lookDepths[slot]
            + Vector3.up * lookHeights[slot];

        GameObject cameraObject = new GameObject($"玩家{slot + 1}视角");
        cameraObject.transform.SetParent(stageRoot, false);
        cameraObject.transform.position = cameraPosition;
        cameraObject.transform.rotation = Quaternion.LookRotation(
            lookPosition - cameraPosition, Vector3.up);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = cameraFovs[slot];
        camera.nearClipPlane = 0.04f;
        camera.farClipPlane = 100f;
        camera.depth = 0f;
        slotCameras[slot] = camera;
    }

    GameObject CreateCube(string objectName, Vector3 position, Vector3 scale,
        Quaternion rotation, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(stageRoot, false);
        cube.transform.SetPositionAndRotation(position, rotation);
        cube.transform.localScale = scale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
        return cube;
    }

    void CreatePointLight(string lightName, Vector3 position, Color color,
        float intensity, float range)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(stageRoot, false);
        lightObject.transform.position = position;

        Light pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = color;
        pointLight.intensity = intensity;
        pointLight.range = range;
        pointLight.shadows = LightShadows.None;
    }

    Material LoadMaterial(string resourcePath, Color fallbackColor)
    {
        Material material = Resources.Load<Material>(resourcePath);
        if (material != null) return material;

        Shader shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material fallback = new Material(shader);
        fallback.name = resourcePath + "_Fallback";
        fallback.color = fallbackColor;
        return fallback;
    }
}
