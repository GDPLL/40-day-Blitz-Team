using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 将开始场景展示区写入场景并生成机位预览
public static class StartViewStageSceneBuilder
{
    const string StartScenePath = "Assets/Scenes/Start.unity";
    const string PlayerPrefabPath = "Assets/prefab/Player.prefab";

    [MenuItem("Tools/开始场景/重建四号展示区")]
    public static void BuildAndSave()
    {
        Scene scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        Start_Main startMain = Object.FindObjectOfType<Start_Main>();
        if (startMain == null) throw new System.Exception("Start_Main 缺失");

        StartViewStage stage = startMain.GetComponent<StartViewStage>();
        if (stage == null) stage = startMain.gameObject.AddComponent<StartViewStage>();
        stage.RebuildLayout(startMain.StartVector3);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("StartViewStageSceneBuilder|BuildAndSave|展示区已保存");
    }

    [MenuItem("Tools/开始场景/生成3号机位预览")]
    public static void RenderPlayer3Preview()
    {
        RenderPlayerPreview(2, "player3_preview.png");
    }

    [MenuItem("Tools/开始场景/生成4号机位预览")]
    public static void RenderPlayer4Preview()
    {
        RenderPlayerPreview(3, "player4_preview.png");
    }

    static void RenderPlayerPreview(int slot, string fileName)
    {
        Scene scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        Start_Main startMain = Object.FindObjectOfType<Start_Main>();
        StartViewStage stage = startMain != null
            ? startMain.GetComponent<StartViewStage>() : null;
        if (startMain == null || stage == null)
            throw new System.Exception("展示区组件缺失");

        Transform root = startMain.transform.Find("开始场景_四号展示区");
        Transform anchor = root != null ? root.Find($"玩家{slot + 1}出生点") : null;
        Transform cameraTransform = root != null ? root.Find($"玩家{slot + 1}视角") : null;
        Camera camera = cameraTransform != null
            ? cameraTransform.GetComponent<Camera>() : null;
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (anchor == null || camera == null || playerPrefab == null)
            throw new System.Exception("展示对象或玩家预制体缺失");

        GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
        player.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null) body.isKinematic = true;

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            string[] names = new string[animator.runtimeAnimatorController.animationClips.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = animator.runtimeAnimatorController.animationClips[i].name;
            Debug.Log("StartViewStageSceneBuilder|Clips|" + string.Join(",", names));

            RuntimeAnimatorController displayController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/others/Player.controller");
            if (displayController != null)
                animator.runtimeAnimatorController = displayController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetBool("IsGround", true);
            animator.SetBool("walk", false);
            animator.SetBool("jump", false);
            animator.SetBool("Squat", slot == 0 || slot == 3);
            animator.SetBool("raiseGun", true);
            animator.SetLayerWeight(1, 1f);
            animator.Play(slot == 0 || slot == 3 ? "蹲下-常态" : "站立", 0, 1f);
            animator.Play("举枪", 1, 1f);
            animator.Update(0.1f);
            animator.speed = 0f;
            Transform gunRoot = FindChildByName(player.transform, "枪械1");
            if (gunRoot != null)
                gunRoot.rotation = Quaternion.LookRotation(stage.GetAimDirection(slot), Vector3.up);
            AnimatorClipInfo[] baseClips = animator.GetCurrentAnimatorClipInfo(0);
            AnimatorClipInfo[] handClips = animator.GetCurrentAnimatorClipInfo(1);
            string baseName = baseClips.Length > 0 ? baseClips[0].clip.name : "none";
            string handName = handClips.Length > 0 ? handClips[0].clip.name : "none";
            Debug.Log($"StartViewStageSceneBuilder|State|base={baseName}|hand={handName}|weight={animator.GetLayerWeight(1)}");
        }

        RenderCamera(camera, fileName);
        Object.DestroyImmediate(player);
        EditorSceneManager.SaveScene(scene);
    }

    static void RenderCamera(Camera camera, string fileName)
    {
        const int width = 1280;
        const int height = 720;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);

        camera.enabled = true;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();

        string output = Path.GetFullPath(
            "C:/Users/Tosaka/.codex/visualizations/2026/10/29/01a1889a-1a85-7950-9b4b-9c0ed6698284/"
            + fileName);
        File.WriteAllBytes(output, image.EncodeToPNG());

        RenderTexture.active = null;
        camera.targetTexture = null;
        camera.enabled = false;
        RenderTexture.ReleaseTemporary(target);
        Object.DestroyImmediate(image);
        Debug.Log($"StartViewStageSceneBuilder|RenderCamera|已输出{output}");
    }

    static Transform FindChildByName(Transform root, string namePart)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name.Contains(namePart)) return children[i];
        }
        return null;
    }
}
