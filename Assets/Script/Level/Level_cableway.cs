using System.Collections.Generic;
using UnityEngine;

// 缆车交互，托起玩家
public class Level_cableway : MonoBehaviour, IScene_Interaction
{
    public Transform target;        //目标点
    public float upForce = 1000f;   //上升力
    public float throwSpeed = 6f;   //抛出速度

    public AudioSource audioSource;     //一次性音源
    public AudioSource riseSource;      //运行音源，循环
    public AudioClip startClip;         //开始声
    public AudioClip riseClip;          //运行声
    public AudioClip leaveClip;         //离开声

    bool rising;    //托起中
    readonly Dictionary<Player_Control, float> releasedAt = new Dictionary<Player_Control, float>();   //玩家抛出时间
    const float launchCooldown = 0.5f;    //抛出重触发间隔

    // 持续施力，control为玩家
    public void Scene_Interaction(Player_Control control)
    {
        if (control == null)
        {
            Debug.LogError("Level_cableway|Scene_Interaction|Player_Control 为空");
            return;
        }
        if (target == null || control.rigidbody == null)
        {
            Debug.LogError("Level_cableway|Scene_Interaction|引用为空");
            return;
        }
        if (releasedAt.TryGetValue(control, out float time))
        {
            if (Time.fixedTime - time < launchCooldown)
            {
                Throw_Date(control);
                return;
            }

            releasedAt.Remove(control);
        }

        // 未到目标高度，持续上升并锁住水平位置
        if (control.transform.position.y < target.position.y)
        {
            if (control.Con_body != null) control.Con_body.Body_Scene_Lift_Date();

            // 刚上缆车，播开始声并起运行声
            if (!rising)
            {
                rising = true;
                Play_Once(startClip);

                if (riseSource != null && riseClip != null)
                {
                    riseSource.clip = riseClip;
                    riseSource.loop = true;
                    riseSource.Play();
                }
            }

            control.rigidbody.AddForce(Vector3.up * upForce, ForceMode.Force);

            Vector3 vector3 = new Vector3(this.transform.position.x, control.rigidbody.position.y, this.transform.position.z);
            control.rigidbody.velocity = new Vector3(0f, control.rigidbody.velocity.y, 0f);
            control.rigidbody.position = vector3;
            return;
        }

        // 到目标点，收声并播离开
        if (rising)
        {
            rising = false;
            if (riseSource != null && riseSource.isPlaying) riseSource.Stop();
            Play_Once(leaveClip);
        }

        releasedAt[control] = Time.fixedTime;
        Throw_Date(control);
    }

    // 水平抛出，control为玩家
    void Throw_Date(Player_Control control)
    {
        Vector3 dir = target.position - control.rigidbody.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f)
        {
            Debug.LogError("Level_cableway|Throw_Date|方向为空");
            return;
        }

        control.rigidbody.velocity = dir.normalized * throwSpeed;
    }

    // 播一次音效
    void Play_Once(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;

        audioSource.PlayOneShot(clip);
    }
}
