using UnityEngine;

/// Camera View: follows and renders only. Player_Control supplies look input and mode commands.
public sealed class Player_camera : MonoBehaviour
{
    public Transform Camera_Object;
    public Transform Target_Object;
    public Transform Player;
    public float radius;
    public Vector3 startOffset;
    public Vector3 shoulderStartOffset;
    public float shoulderRadius;
    public float shakeDuration = .1f;
    public float shakeMagnitude = .05f;
    public float modeSwitchSmoothTime = .15f;

    private float yaw, pitch;
    private bool shoulderAim;
    private Vector3 currentLocalOffset, localVelocity, shakeOffset;
    private float shakeEndTime;

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        currentLocalOffset = startOffset + Vector3.back * radius;
    }

    public void SetFollowTarget(Transform target) => Player = target;
    public void SetShoulderAim(bool value) => shoulderAim = value;
    public void Rotate(Vector2 delta)
    {
        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, -90f, 90f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
    public void Shake()
    {
        shakeOffset = Random.insideUnitSphere * shakeMagnitude;
        shakeEndTime = Time.time + shakeDuration;
    }

    private void LateUpdate()
    {
        if (Player == null || Camera_Object == null || Target_Object == null) return;
        Vector3 desiredLocalOffset = shoulderAim
            ? shoulderStartOffset + Vector3.back * shoulderRadius
            : startOffset + Vector3.back * radius;
        currentLocalOffset = Vector3.SmoothDamp(currentLocalOffset, desiredLocalOffset, ref localVelocity, modeSwitchSmoothTime);
        if (Time.time >= shakeEndTime) shakeOffset = Vector3.zero;
        Target_Object.position = Player.position;
        Camera_Object.position = Target_Object.position + transform.rotation * currentLocalOffset + shakeOffset;
    }
}
