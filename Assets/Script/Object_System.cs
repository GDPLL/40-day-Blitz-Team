using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


public class Object_System : MonoBehaviour , Idamage
{

    
    public int HP = 100;
    public int MaxHp =100;

    public void Takedamage(int hit)
    {
        HP = Mathf.Clamp(HP -hit,0,MaxHp);
        Debug.Log($"收到伤害{this.gameObject.name}");

    }
    private void Update()
    {
        // 血量归零后删除对象
        if (HP <= 0)
        {
            Destroy(gameObject);
        }

    }

}

