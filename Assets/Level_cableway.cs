using UnityEngine;

// 缆车交互，托起玩家
public class Level_cableway : MonoBehaviour, IScene_Interaction
{
    public Transform target;       //目标点
    public float upForce = 1000f;   //上升力
    public float pushForce = 3000f; //抛出推力

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

        // 持续上升力
        control.rigidbody.AddForce(Vector3.up * upForce, ForceMode.Force);
        
        // 未到目标高度,锁定玩家位置
        if (control.transform.position.y < target.position.y)
        {
            Vector3 vector3 = new Vector3(this.transform.position.x,control.transform.position.y,this.transform.position.z);
            control.transform.position = vector3;
            return;
        } 

        // 朝目标点水平抛出
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        control.rigidbody.AddForce(dir.normalized * pushForce, ForceMode.Impulse);
    }
}
