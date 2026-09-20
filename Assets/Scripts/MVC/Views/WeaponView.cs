using UnityEngine;

/// <summary>Weapon View: particles, sound, tracer and recoil recovery only.</summary>
public sealed class WeaponView : MonoBehaviour
{
    private Transform muzzle;
    private LineRenderer tracer;
    private AudioSource shotAudio;
    private GameObject hitEffect;
    private GameObject muzzleEffect;
    private float tracerEndTime;
    private float lastShotTime;

    public void Configure(Transform shootPoint, LineRenderer line, AudioSource audio, GameObject hitPrefab, GameObject muzzlePrefab)
    {
        muzzle = shootPoint;
        tracer = line != null ? line : GetComponent<LineRenderer>();
        shotAudio = audio;
        hitEffect = hitPrefab;
        muzzleEffect = muzzlePrefab;
        if (tracer != null) tracer.enabled = false;
    }

    public void RenderShot(Vector3 origin, Vector3 direction, Vector3 end, RaycastHit? hit)
    {
        lastShotTime = Time.time;
        if (shotAudio != null && !shotAudio.isPlaying) shotAudio.Play();
        if (muzzleEffect != null) Instantiate(muzzleEffect, origin, Quaternion.LookRotation(direction));
        if (hit.HasValue && hitEffect != null)
        {
            RaycastHit value = hit.Value;
            Vector3 bounce = Vector3.Reflect(direction, value.normal);
            Instantiate(hitEffect, value.point, Quaternion.LookRotation(bounce.sqrMagnitude < .001f ? -value.normal : bounce));
        }
        if (tracer == null) return;
        tracer.positionCount = 2;
        tracer.SetPosition(0, origin);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        tracerEndTime = Time.time + .05f;
    }

    private void Update()
    {
        if (tracer != null && Time.time >= tracerEndTime) tracer.enabled = false;
        if (shotAudio != null && shotAudio.isPlaying && Time.time - lastShotTime > .2f) shotAudio.Stop();
    }
}
