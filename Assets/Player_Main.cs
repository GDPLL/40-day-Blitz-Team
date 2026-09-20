using UnityEngine;

/// Scene composition root. It groups scene-only view references; no global static state is used.
public sealed class Player_Main : MonoBehaviour
{
    public Camera ocamera;
    public Player_camera oCamera;
    public RectTransform oUI_RectTransform;

    public bool IsConfigured => ocamera != null && oCamera != null && oUI_RectTransform != null;
}
