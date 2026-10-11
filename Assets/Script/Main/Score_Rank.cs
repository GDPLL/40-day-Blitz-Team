using UnityEngine;

// 积分排行榜排序，纯逻辑
public static class Score_Rank
{
    // 按积分从高到低排序，numbers与scores成对调整，count为人数
    public static void Score_Rank_Sort_Date(int[] numbers, int[] scores, int count)
    {
        if (numbers == null || scores == null)
        {
            Debug.LogError("Score_Rank|Score_Rank_Sort_Date|积分数组为空");
            return;
        }
        if (count > numbers.Length || count > scores.Length)
        {
            Debug.LogError("Score_Rank|Score_Rank_Sort_Date|人数超出数组长度");
            return;
        }

        // 逐个插入排序，积分高在前，同分按编号
        for (int i = 1; i < count; i++)
        {
            int number = numbers[i];
            int score = scores[i];

            int j = i;
            while (j > 0 && (scores[j - 1] < score
                || (scores[j - 1] == score && numbers[j - 1] > number)))
            {
                scores[j] = scores[j - 1];
                numbers[j] = numbers[j - 1];
                j--;
            }

            scores[j] = score;
            numbers[j] = number;
        }
    }
}
