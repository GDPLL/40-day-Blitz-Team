using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 本机弹药与血量显示
public class UI_Player : MonoBehaviour
{
    public UI_Magazine magazineUI;   // 弹夹UI

    [Header("被锁定提示")]
    public Image lockImage1;         // 被锁定闪烁
    public Image lockImage2;         // 锁定完成闪烁
    public TextMeshProUGUI lockText1;   // 被锁定文本
    public TextMeshProUGUI lockText2;   // 锁定完成文本
    public float blinkRate = 8f;     // 每秒闪烁次数

    int lockStage;                   // 锁定阶段
    float blinkTimer;                // 闪烁计时

    // 初始化，检查状态图与弹夹UI是否已绑定
    public void UI_Player_Init()
    {
        // 场景合并后可能丢失弹夹组件引用，这里按需补回，避免再次覆盖场景改动
        if (magazineUI == null) magazineUI = GetComponent<UI_Magazine>();
        if (magazineUI == null) magazineUI = gameObject.AddComponent<UI_Magazine>();

        magazineUI.UI_Magazine_Init();

        UI_Player_Show_Lock(0);
        Debug.Log("UI_Player|UI_Player_Init|完成初始化");
    }

    // 按弹药、血量、弹夹数与换弹状态刷新
    public void UI_Player_Show(int ammo, int maxAmmo, int hp, int maxHp,
        int magazineCount, bool isReload)
    {
        if (magazineUI != null)
            magazineUI.UI_Magazine_Show(ammo, maxAmmo, magazineCount, isReload);
    }

    // 锁定阶段 0解除 1被锁 2完成
    public void UI_Player_Show_Lock(int stage)
    {
        lockStage = stage;
        blinkTimer = 0f;

        if (lockImage1 != null) lockImage1.enabled = false;
        if (lockImage2 != null) lockImage2.enabled = false;
        if (lockText1 != null) lockText1.enabled = false;
        if (lockText2 != null) lockText2.enabled = false;
    }

    // 闪烁，完成时两个同时闪
    void Update()
    {
        if (lockStage <= 0) return;

        blinkTimer += Time.deltaTime;
        bool on = Mathf.Repeat(blinkTimer * blinkRate, 1f) < 0.5f;

        if (lockImage1 != null) lockImage1.enabled = on;
        if (lockText1 != null) lockText1.enabled = on;

        bool done = lockStage > 1 && on;    // 完成阶段两个一起闪
        if (lockImage2 != null) lockImage2.enabled = done;
        if (lockText2 != null) lockText2.enabled = done;
    }
}
