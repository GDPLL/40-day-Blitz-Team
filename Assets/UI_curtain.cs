using TMPro;
using UnityEngine;

// UI幕布
public class UI_curtain : MonoBehaviour
{
    public GameObject UI_gameObjcet;
    public TextMeshProUGUI text;    //幕布文本

    // 初始化，隐藏幕布
    public void UI_curtain_Init()
    {
        if (text == null)
        {
            Debug.LogError("UI_curtain|UI_curtain_Init|文本为空");
            return;
        }

        UI_gameObjcet.SetActive(false);
    }

    // 显示文本
    public void UI_curtain_Show(string content)
    {
        if (text == null) return;

        text.text = content;
        UI_gameObjcet.SetActive(true);
    }

    // 隐藏
    public void UI_curtain_Hide()
    {
        UI_gameObjcet.SetActive(false);
    }
}
