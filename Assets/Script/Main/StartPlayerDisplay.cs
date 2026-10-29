using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

// 开始场景中的联网玩家只作为展示，不参与关卡行为。
public sealed class StartPlayerDisplay : NetworkBehaviour
{
    [SerializeField] string idleStateName = "站立";
    [SerializeField] RuntimeAnimatorController lobbyController;
    readonly NetworkVariable<int> displaySlot = new NetworkVariable<int>();
    readonly NetworkVariable<ulong> targetClientId =
        new NetworkVariable<ulong>(ulong.MaxValue);

    // 主机在生成前写入席位与目标客户端
    public void ConfigureLobbyDisplay(int slot, ulong clientId)
    {
        displaySlot.Value = slot;
        targetClientId.Value = clientId;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (Start_Main.Instance == null) return;   //关卡内不执行展示
        StartCoroutine(ApplyDisplayState(displaySlot.Value));
    }

    IEnumerator ApplyDisplayState(int slot)
    {
        // 等待网络变量和场景动画组件完成初始化
        yield return null;

        if (Start_Main.Instance == null)
            yield break;

        // 开始界面使用场景灯光，关闭玩家预制体自带的点光源，避免异常影子。
        foreach (Light playerLight in GetComponentsInChildren<Light>(true))
            playerLight.enabled = false;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.isKinematic = true;
        }

        // 展示场景禁止网络位置回写覆盖脚底对齐
        foreach (NetworkTransform networkTransform
            in GetComponentsInChildren<NetworkTransform>(true))
            networkTransform.enabled = false;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("StartPlayerDisplay|未找到玩家动画控制器");
            yield break;
        }

        // 开始场景使用包含完整持枪动作的控制器
        if (lobbyController != null
            && animator.runtimeAnimatorController != lobbyController)
            animator.runtimeAnimatorController = lobbyController;
        animator.Rebind();
        animator.enabled = true;

        // 不同席位使用不同姿态
        bool crouched = slot == 3;
        bool raisedGun = slot >= 2;
        string stateName = slot switch
        {
            0 => "站立",
            1 => "持枪",
            2 => "举枪",
            3 => "举枪",
            _ => idleStateName
        };

        animator.SetBool("walk", false);
        animator.SetBool("raiseGun", raisedGun);
        animator.SetBool("jump", false);
        animator.SetBool("IsGround", true);
        animator.SetBool("IsWeightLess", false);
        animator.SetBool("IsDeath", false);
        animator.SetBool("IsRoll", false);
        animator.SetBool("IsReload", false);
        animator.SetBool("Squat", crouched);
        animator.SetBool("IsSlide", false);
        animator.SetBool("PeekLeft", false);
        animator.SetBool("PeekRight", false);
        animator.SetBool("IsRollHalf", false);
        animator.SetFloat("VerticalSpeed", 0f);
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 1f);
        animator.SetFloat("Speed", 0f);
        animator.SetFloat("CrouchActionSpeed", 0f);

        if (raisedGun)
        {
            // 蹲伏或站立底层叠加持枪瞄准
            animator.Play(crouched ? "蹲下-常态" : "站立", 0, 1f);
            animator.SetLayerWeight(1, 1f);
            animator.Play("举枪", 1, 1f);
            animator.Update(0.1f);
        }
        else
        {
            animator.SetLayerWeight(1, 0f);
            animator.Play(stateName, 0, 0.25f);
        }
        animator.Update(0f);
        Physics.SyncTransforms();
        AlignFeetToStage(slot);
        if (raisedGun) AlignGunHorizontal(slot);
        animator.speed = 0f;

        // 只让目标玩家的本机切换到自己的视角
        if (NetworkManager.Singleton != null
            && NetworkManager.Singleton.LocalClientId == targetClientId.Value)
            Start_Main.Instance.ShowViewForSlot(slot);

        Debug.Log($"StartPlayerDisplay|展示完成|玩家{slot + 1}|状态{stateName}");
    }

    // 按脚掌骨骼贴到箱顶或地面
    void AlignFeetToStage(int slot)
    {
        float lowestFootY = float.PositiveInfinity;
        Transform[] bones = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            bool isFootBone = boneName.Contains("mixamorig:LeftFoot")
                || boneName.Contains("mixamorig:RightFoot")
                || boneName.Contains("mixamorig:LeftToeBase")
                || boneName.Contains("mixamorig:RightToeBase")
                || boneName.Contains("mixamorig:LeftToe_End")
                || boneName.Contains("mixamorig:RightToe_End");

            if (isFootBone)
                lowestFootY = Mathf.Min(lowestFootY, bones[i].position.y);
        }

        if (float.IsPositiveInfinity(lowestFootY))
        {
            Debug.LogError("StartPlayerDisplay|AlignFeetToStage|未找到脚掌骨骼");
            return;
        }

        float surfaceY = Start_Main.Instance.GetViewSurfaceHeight(slot);
        float lift = surfaceY - 0.04f - lowestFootY;
        if (Mathf.Abs(lift) > 0.005f)
            transform.position += Vector3.up * lift;

        Debug.Log($"StartPlayerDisplay|AlignFeetToStage|玩家{slot + 1}|抬升{lift:0.000}");
    }

    // 举枪展示时锁定水平枪线
    void AlignGunHorizontal(int slot)
    {
        Transform gunRoot = FindChildByName(transform, "枪械1");
        if (gunRoot == null)
        {
            Debug.LogError("StartPlayerDisplay|AlignGunHorizontal|枪械根节点缺失");
            return;
        }

        Vector3 aimDirection = Start_Main.Instance.GetViewAimDirection(slot);
        gunRoot.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    Transform FindChildByName(Transform root, string namePart)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name.Contains(namePart)) return children[i];
        }
        return null;
    }
}
