using UnityEngine;

// 缆车交互，托起玩家
public class Level_cableway : MonoBehaviour, IScene_Interaction
{
    public Transform target;        //目标点
    public float upForce = 1000f;   //上升力
    public float pushForce = 3000f; //抛出推力

    public AudioSource audioSource;     //一次性音源
    public AudioSource riseSource;      //运行音源，循环
    public AudioClip startClip;         //开始声
    public AudioClip riseClip;          //运行声
    public AudioClip leaveClip;         //离开声

    bool rising;    //托起中

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

        // 未到目标高度，持续上升并锁住水平位置
        if (control.transform.position.y < target.position.y)
        {
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

            Vector3 vector3 = new Vector3(this.transform.position.x, control.transform.position.y, this.transform.position.z);
            control.transform.position = vector3;
            return;
        }

        // 到目标点，收声并播离开
        if (rising)
        {
            rising = false;
            if (riseSource != null && riseSource.isPlaying) riseSource.Stop();
            Play_Once(leaveClip);
        }

        // 到目标高度，清掉向上速度
        Vector3 velocity = control.rigidbody.velocity;
        velocity.y = 0f;
        control.rigidbody.velocity = velocity;

        // 朝目标点水平抛出
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        control.rigidbody.AddForce(dir.normalized * pushForce, ForceMode.Impulse);
    }

    // 播一次音效
    void Play_Once(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;

        audioSource.PlayOneShot(clip);
    }
}
