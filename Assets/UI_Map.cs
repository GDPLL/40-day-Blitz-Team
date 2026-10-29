using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 地图分层显示，编号对应分层下标
public class UI_Map : MonoBehaviour
{
    [Header("分层图，下标0-9")]
    public Image[] layers;

    [Header("每层下沿高度")]
    public float[] layerY;                            // 下标与 layers 对应

    [Header("编号")]
    public int[] respawnLayer = { 0, 2, 4, 7, 9 };    // 1F-5F 复活点所在分层
    public int[] gasLayer = { 1, 3, 6, 7 };           // 毒气1-4阶段到达的分层

    [Header("楼层文本")]
    public TextMeshProUGUI floorText;                 // 当前楼层名称
    public string[] floorName =
    {
        "1F商场装修区", "2F商场装修区", "3F工地物料区", "4F工地吊机平台",
        "5F工地高塔基座平台", "6F工地高塔建设区", "7F工地高塔建设区",
        "8F工地???征用区", "9F工地???阵地区", "10F工地???堡垒区", "11F工地???部署区"
    };

    [Header("颜色，整体偏黑调")]
    public Color hideColor = new Color(0f, 0f, 0f, 0f);         // 未点亮
    public Color yellowColor = new Color(0.62f, 0.50f, 0.09f);  // 当前楼层
    public Color greenColor = new Color(0.14f, 0.45f, 0.20f);   // 复活楼层
    public Color redColor = new Color(0.60f, 0.11f, 0.09f);     // 毒气楼层

    [Header("M键放大")]
    public GameObject showOnOpen;       // M后显示的对象
    public Transform zoomTarget;        // 放大对象，空则取自身
    public float zoomScale = 1.6f;      // 放大倍率
    public float zoomTime = 0.25f;      // 缩放平滑时长

    [Header("N键折叠")]
    public Transform foldTarget;        // 折叠对象
    public Transform moveTarget;        // 移动对象
    public Transform movePoint;         // 折叠后去的位置
    public float moveTurn = 12f;        // 旋转插值速度

    bool zoomOn;                        // 已展开
    bool foldOn;                        // 已折叠
    bool isReady;                       // 已初始化
    Vector3 zoomBase;                   // 放大前缩放
    Vector3 zoomVelocity;               // 放大平滑速度
    Vector3 foldBase;                   // 折叠前缩放
    Vector3 foldVelocity;               // 折叠平滑速度
    Vector3 moveBase;                   // 归位位置
    Vector3 moveVelocity;               // 移动平滑速度
    Quaternion moveRotBase;             // 归位旋转

    // 初始化，全部隐藏
    public void UI_Map_Init()
    {
        if (layers == null || layers.Length == 0)
        {
            Debug.LogError("UI_Map|UI_Map_Init|分层图未绑定");
            return;
        }

        for (int i = 0; i < layers.Length; i++)
            if (layers[i] != null) layers[i].color = hideColor;

        // 缩放对象重置
        if (zoomTarget == null) zoomTarget = transform;
        zoomBase = zoomTarget.localScale;
        zoomOn = false;
        zoomVelocity = Vector3.zero;

        // 折叠对象重置
        foldOn = false;
        foldVelocity = Vector3.zero;
        if (foldTarget != null) foldBase = foldTarget.localScale;

        // 移动对象重置
        moveVelocity = Vector3.zero;
        if (moveTarget != null)
        {
            moveBase = moveTarget.localPosition;
            moveRotBase = moveTarget.localRotation;
        }

        if (showOnOpen != null) showOnOpen.SetActive(false);
        if (floorText != null)
        {
            floorText.text = string.Empty;
            floorText.enabled = false;
        }

        isReady = true;     // 基准值已就位
    }

    // 刷新颜色，优先级红>绿>黄
    public void UI_Map_Show(float playerY, int respawnFloor, int gasLevel)
    {
        if (layers == null || layers.Length == 0) return;

        int playerIndex = Layer_Index(playerY);                    // 当前高度分层
        int respawnIndex = Layer_Of(respawnLayer, respawnFloor);   // 复活楼层分层
        int gasIndex = gasLevel > 0 ? Layer_Of(gasLayer, gasLevel - 1) : -1;   // 毒气到达分层

        // 楼层名称跟随当前高度
        if (floorText != null && floorName != null && floorName.Length > 0)
            floorText.text = floorName[Mathf.Clamp(playerIndex, 0, floorName.Length - 1)];

        for (int i = 0; i < layers.Length; i++)
        {
            Image img = layers[i];
            if (img == null) continue;

            if (i <= gasIndex) img.color = redColor;                // 毒气0-编号全红
            else if (i == respawnIndex) img.color = greenColor;
            else if (i == playerIndex) img.color = yellowColor;
            else img.color = hideColor;
        }
    }

    // M键展开切换，N键折叠
    void Update()
    {
        if (!isReady) return;       // 未初始化不写缩放

        if (Input_Manage.Instance != null)
        {
            if (Input_Manage.Instance.MapDown)
            {
                zoomOn = !zoomOn;
                if (showOnOpen != null) showOnOpen.SetActive(zoomOn);
                if (floorText != null) floorText.enabled = zoomOn;    // 仅展开时显示
            }

            if (Input_Manage.Instance.FoldDown) foldOn = !foldOn;
        }

        // M键放大
        if (zoomTarget != null)
        {
            Vector3 want = zoomBase * (zoomOn ? zoomScale : 1f);
            zoomTarget.localScale = Vector3.SmoothDamp(zoomTarget.localScale, want, ref zoomVelocity, zoomTime);
        }

        // N键折叠，缩到 0 隐藏不可见
        if (foldTarget != null)
        {
            Vector3 foldWant = foldOn ? Vector3.zero : foldBase;
            foldTarget.localScale = Vector3.SmoothDamp(foldTarget.localScale, foldWant, ref foldVelocity, zoomTime);
        }

        // 折叠时移到目标点，再按归位
        if (moveTarget != null && movePoint != null)
        {
            Vector3 moveWant = foldOn ? movePoint.localPosition : moveBase;
            moveTarget.localPosition = Vector3.SmoothDamp(moveTarget.localPosition, moveWant, ref moveVelocity, zoomTime);

            Quaternion rotWant = foldOn ? movePoint.localRotation : moveRotBase;
            moveTarget.localRotation = Quaternion.Slerp(moveTarget.localRotation, rotWant, moveTurn * Time.deltaTime);
        }
    }

    // 编号转分层下标，取首尾
    int Layer_Of(int[] arr, int index)
    {
        if (arr == null || arr.Length == 0) return -1;
        int num = arr[Mathf.Clamp(index, 0, arr.Length - 1)];
        return Mathf.Clamp(num, 0, layers.Length - 1);
    }

    // 高度所在分层，-1为不在
    int Layer_Index(float y)
    {
        if (layerY == null) return -1;

        int index = -1;
        for (int i = 0; i < layerY.Length && i < layers.Length; i++)
            if (y >= layerY[i]) index = i;     // 高于就切换

        return index;
    }
}
