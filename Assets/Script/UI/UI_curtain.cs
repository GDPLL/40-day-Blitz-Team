using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI幕布
public class UI_curtain : MonoBehaviour
{
    public GameObject UI_gameObjcet;
    public TextMeshProUGUI text;    //幕布文本
    public Image bgImage;           //幕布底板

    // 初始化，隐藏幕布
    public void UI_curtain_Init()
    {
        if (text == null)
        {
            Debug.LogError("UI_curtain|UI_curtain_Init|文本为空");
            return;
        }
        if (bgImage == null)
        {
            Debug.LogError("UI_curtain|UI_curtain_Init|底板为空");
            return;
        }

        UI_gameObjcet.SetActive(false);
    }

    // 显示文本，showBG 为底板开关
    public void UI_curtain_Show(string content, bool showBG)
    {
        if (text == null) return;

        if (bgImage != null) bgImage.enabled = showBG;    //花屏期间只留文字
        text.text = content;
        UI_gameObjcet.SetActive(true);
    }

    // 隐藏
    public void UI_curtain_Hide()
    {
        UI_gameObjcet.SetActive(false);
    }
}
