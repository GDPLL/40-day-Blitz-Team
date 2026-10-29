using System.Collections;
using UnityEngine;

// 开始场景中的联网玩家只作为展示，不参与关卡行为。
public sealed class StartPlayerDisplay : MonoBehaviour
{
    [SerializeField] string idleStateName = "站立";

    void OnEnable()
    {
        StartCoroutine(ApplyDisplayState());
    }

    IEnumerator ApplyDisplayState()
    {
        // 等一帧，让场景中的 Start_Main 和 Animator 完成初始化。
        yield return null;

        if (FindObjectOfType<Start_Main>() == null)
            yield break;

        // 开始界面使用场景灯光，关闭玩家预制体自带的点光源，避免异常影子。
        foreach (Light playerLight in GetComponentsInChildren<Light>(true))
            playerLight.enabled = false;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("StartPlayerDisplay|未找到玩家动画控制器");
            yield break;
        }

        animator.SetBool("walk", false);
        animator.SetBool("raiseGun", false);
        animator.SetBool("jump", false);
        animator.SetBool("IsGround", true);
        animator.SetBool("IsWeightLess", false);
        animator.SetBool("IsDeath", false);
        animator.SetBool("IsRoll", false);
        animator.SetBool("IsReload", false);
        animator.SetBool("Squat", false);
        animator.SetBool("IsSlide", false);
        animator.SetBool("PeekLeft", false);
        animator.SetBool("PeekRight", false);
        animator.SetBool("IsRollHalf", false);
        animator.SetFloat("VerticalSpeed", 0f);
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 1f);
        animator.SetFloat("Speed", 0f);
        animator.SetFloat("CrouchActionSpeed", 0f);

        animator.Play(idleStateName, 0, 0f);
        animator.Update(0f);
        animator.speed = 0f;
    }
}
