using UnityEngine;
using UnityEngine.UI;

// 本机弹药与血量显示
public class UI_Player : MonoBehaviour
{
    public Image ammoImage;      // 弹药图像
    public Image healthImage;    // 血量图像

    // 初始化，检查两张图是否已绑定
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

        Debug.Log("UI_Player|UI_Player_Init|完成初始化");
    }

    // 按剩余弹药与血量刷新两条
    public void UI_Player_Show(int ammo, int maxAmmo, int hp, int maxHp)
    {
        if (ammoImage == null || healthImage == null) return;

        ammoImage.fillAmount = maxAmmo > 0 ? (float)ammo / maxAmmo : 0f;
        healthImage.fillAmount = maxHp > 0 ? (float)hp / maxHp : 0f;
    }
}
