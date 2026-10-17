using UnityEngine;
using UnityEngine.UI;

// 本机弹药与血量显示
public class UI_Player : MonoBehaviour
{
    public Image ammoImage;      // 弹药图像
    public Image healthImage;    // 血量图像
    public UI_Magazine magazineUI;   // 弹夹UI

    // 初始化，检查状态图并建立弹夹显示
    public void UI_Player_Init()
    {
        if (ammoImage == null)
        {
            Debug.LogError("UI_Player|UI_Player_Init|弹药图像为空");
            return;
        }
        if (healthImage == null)
        {
            Debug.LogError("UI_Player|UI_Player_Init|血量图像为空");
            return;
        }

        // 场景未手动绑定时自动挂载，面板由 UI_Magazine 自建
        if (magazineUI == null) magazineUI = GetComponent<UI_Magazine>();
        if (magazineUI == null) magazineUI = gameObject.AddComponent<UI_Magazine>();
        magazineUI.UI_Magazine_Init();

        Debug.Log("UI_Player|UI_Player_Init|完成初始化");
    }

    // 按剩余弹药、血量、弹夹数与换弹状态刷新
    public void UI_Player_Show(int ammo, int maxAmmo, int hp, int maxHp,
        int magazineCount, bool isReload)
    {
        if (ammoImage == null || healthImage == null) return;

        ammoImage.fillAmount = maxAmmo > 0 ? (float)ammo / maxAmmo : 0f;
        healthImage.fillAmount = maxHp > 0 ? (float)hp / maxHp : 0f;

        if (magazineUI != null)
            magazineUI.UI_Magazine_Show(ammo, maxAmmo, magazineCount, isReload);
    }
}
