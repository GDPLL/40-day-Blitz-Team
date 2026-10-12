using UnityEngine;

// 锁定提示显示，只有本地玩家运行
public class Lock_On_UI : MonoBehaviour
{
    // 显示锁定提示，stage 0解除 1被锁定 2完成
    public void Lock_On_Performance_Local(int stage)
    {
        if (stage != 0 && stage != 1 && stage != 2)
        {
            Debug.LogError("Lock_On_UI|Lock_On_Performance_Local|阶段参数非法");
            return;
        }

        if (stage == 0) Debug.Log("解除锁定");
        else if (stage == 1) Debug.Log("被锁定");
        else Debug.Log("锁定完成");
    }
}
