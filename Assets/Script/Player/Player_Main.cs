using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Player_Main : MonoBehaviour
{
    public  Camera ocamera;
    public  Player_camera oCamera;
    public  RectTransform oUI_RectTransform;

    

    public static Camera camera;
    public static Player_camera player_Camera;
    public static RectTransform UI_RectTransform;


    private void Start()
    {
        camera = ocamera;
        player_Camera = oCamera;
        UI_RectTransform =oUI_RectTransform;
    }

}
