using System.Text;
using TMPro;
using UnityEngine;

// 积分排行榜，由 Player_Main 驱动
public class UI_Rank : MonoBehaviour
{
    public TextMeshProUGUI rankText;     // 排行榜文本

    readonly StringBuilder builder = new StringBuilder();   // 文本拼装

    // 初始化，检查文本绑定并隐藏
    public void UI_Rank_Init()
    {
        if (rankText == null)
        {
            Debug.LogError("UI_Rank|UI_Rank_Init|排行榜文本未绑定");
            return;
        }

        rankText.text = string.Empty;
        rankText.gameObject.SetActive(false);
    }

    // 隐藏排行榜
    public void UI_Rank_Hide()
    {
        if (rankText == null) return;

        if (rankText.gameObject.activeSelf) rankText.gameObject.SetActive(false);
    }

    // 显示排行榜，numbers为玩家编号，scores为对应积分，count为人数
    public void UI_Rank_Show(int[] numbers, int[] scores, int count)
    {
        if (rankText == null) return;

        if (numbers == null || scores == null)
        {
            Debug.LogError("UI_Rank|UI_Rank_Show|积分数组为空");
            return;
        }
        if (count > numbers.Length || count > scores.Length)
        {
            Debug.LogError("UI_Rank|UI_Rank_Show|人数超出数组长度");
            return;
        }

        // 一行一名，名次 玩家 积分
        builder.Clear();
        for (int i = 0; i < count; i++)
        {
            if (i > 0) builder.Append('\n');
            builder.Append(i + 1).Append(". 玩家").Append(numbers[i]).Append("  ").Append(scores[i]);
        }

        rankText.gameObject.SetActive(true);
        rankText.text = builder.ToString();
    }
}
