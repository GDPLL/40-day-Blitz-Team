using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 复活点刷新器
public class Level_ProgressCollider : MonoBehaviour, IScene_Interaction
{
    public int Level_Num;
    public void Scene_Interaction(Player_Control player_Control)
    {
        if (player_Control.respawnIndex < Level_Num) player_Control.respawnIndex = Level_Num;

    }
}
