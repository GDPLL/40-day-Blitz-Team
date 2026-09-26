using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Player_Main : MonoBehaviour
{
    // 场景引用
    public Camera ocamera;                  // 相机
    public Player_camera oCamera;           // 相机脚本
    public RectTransform oUI_RectTransform; // 准星UI

    // 静态引用
    public static Camera camera;                  // 相机
    public static Player_camera player_Camera;    // 相机脚本
    public static RectTransform UI_RectTransform; // 准星UI

    // 保存引用供其他脚本读取
    private void Start()
    {
        camera = ocamera;
        player_Camera = oCamera;
        UI_RectTransform = oUI_RectTransform;
    }

}
