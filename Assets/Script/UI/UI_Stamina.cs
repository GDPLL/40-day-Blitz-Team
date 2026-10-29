using UnityEngine;
using UnityEngine.UI;

// 本机体力条显示
public class UI_Stamina : MonoBehaviour
{
    public Image staminaImage;      //体力填充图
    RectTransform staminaFillRect;  //临时体力填充
    float staminaFillWidth;         //临时体力宽度

    // 初始化临时体力条
    public void UI_Stamina_Init()
    {
        if (staminaImage == null) UI_Stamina_Build();
        UI_Stamina_Show(1f, 1f);
    }

    // 刷新体力条，stamina为当前值
    public void UI_Stamina_Show(float stamina, float maxStamina)
    {
        if (staminaImage == null) return;

        float normalized = maxStamina > 0f
            ? Mathf.Clamp01(stamina / maxStamina) : 0f;

        if (staminaFillRect != null)
        {
            Vector2 size = staminaFillRect.sizeDelta;
            size.x = staminaFillWidth * normalized;
            staminaFillRect.sizeDelta = size;
        }
        else staminaImage.fillAmount = normalized;

        staminaImage.color = normalized <= 0.3f
            ? new Color(1f, 0.38f, 0.24f, 1f)
            : new Color(0.62f, 0.9f, 1f, 1f);
    }

    // 创建临时体力条
    void UI_Stamina_Build()
    {
        RectTransform parent = transform as RectTransform;
        if (parent == null)
        {
            Debug.LogError("UI_Stamina|UI_Stamina_Build|父级不是RectTransform");
            return;
        }

        GameObject bg = new GameObject("临时体力条",
            typeof(RectTransform), typeof(Image));
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.SetParent(parent, false);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.zero;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(266f, 113f);
        bgRect.sizeDelta = new Vector2(400f, 12f);

        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0.06f, 0.08f, 0.09f, 0.82f);
        bgImage.raycastTarget = false;

        GameObject fill = new GameObject("填充",
            typeof(RectTransform), typeof(Image));
        staminaFillRect = fill.GetComponent<RectTransform>();
        staminaFillRect.SetParent(bgRect, false);
        staminaFillRect.anchorMin = new Vector2(0f, 0.5f);
        staminaFillRect.anchorMax = new Vector2(0f, 0.5f);
        staminaFillRect.pivot = new Vector2(0f, 0.5f);
        staminaFillRect.anchoredPosition = new Vector2(3f, 0f);
        staminaFillWidth = 394f;
        staminaFillRect.sizeDelta = new Vector2(staminaFillWidth, 6f);

        staminaImage = fill.GetComponent<Image>();
        staminaImage.color = new Color(0.62f, 0.9f, 1f, 1f);
        staminaImage.raycastTarget = false;
    }
}
