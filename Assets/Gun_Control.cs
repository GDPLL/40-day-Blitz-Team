using UnityEngine;

/// Weapon Controller: translates fire commands into WeaponModel changes, hit detection and view commands.
public sealed class Gun_Control : MonoBehaviour
{
    [Header("Legacy prefab bindings - transferred to WeaponView in Awake")]
    public Transform shootPoint;
    public LineRenderer line;
    public AudioSource audioSource;
    public GameObject hitEffect;
    public GameObject fireEffect;

    [Header("Weapon rules")]
    public int ammo = 30;
    public int maxAmmo = 30;
    public float fireRate = 10f;
    public float range = 100f;
    public int damage = 34;
    public float recoilX = .03f;
    public float recoilY = .06f;
    public float maxRecoil = .5f;
    public float recoilRecoverFactor = 2f;

    public WeaponModel Model { get; } = new WeaponModel();
    private WeaponView view;
    private Vector2 recoil;
    private float recoilRecoverySpeed;
    private float recoilMultiplier = 1f;

    private void Awake()
    {
        Model.Initialize(ammo, maxAmmo);
        view = GetComponent<WeaponView>();
        if (view == null) view = gameObject.AddComponent<WeaponView>();
        view.Configure(shootPoint, line, audioSource, hitEffect, fireEffect);
    }

    private void Update()
    {
        recoil = Vector2.MoveTowards(recoil, Vector2.zero, recoilRecoverySpeed * Time.deltaTime);
    }

    public bool TryFire(Vector3 aimPoint)
    {
        if (!Model.TryFire(Time.time, fireRate)) return false;
        ammo = Model.Ammo; // compatibility with any existing HUD bindings

        Vector3 origin = shootPoint == null ? transform.position + Vector3.up : shootPoint.position;
        recoil += new Vector2(Random.Range(-recoilX, recoilX), Random.Range(0f, recoilY)) * recoilMultiplier;
        recoil = Vector2.ClampMagnitude(recoil, maxRecoil);
        recoilRecoverySpeed = recoil.magnitude / Mathf.Max(.01f, recoilRecoverFactor / fireRate);

        Vector3 direction = ((aimPoint - origin).normalized + transform.right * recoil.x + transform.up * recoil.y).normalized;
        Vector3 end = origin + direction * range;
        RaycastHit? result = null;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range))
        {
            end = hit.point;
            result = hit;
            hit.collider.GetComponent<Idamage>()?.Takedamage(damage);
        }
        view.RenderShot(origin, direction, end, result);
        return true;
    }

    public void Reload() { Model.Reload(); ammo = Model.Ammo; }
    public bool HasAmmo() => Model.Ammo > 0;
    public bool InRange(Transform target) => target != null && Vector3.Distance(transform.position, target.position) <= range;
    public void SetAimMode(bool aiming) => recoilMultiplier = aiming ? .3f : 1f;

    // Compatibility API for existing enemy/player callers.
    public bool Shoot() => TryFire((shootPoint == null ? transform : shootPoint).position + transform.forward * range);
    public bool Shoot(Transform target) => TryFire(target == null ? transform.position + transform.forward * range : target.position + Vector3.up);
    public void SetRecoilReduction(bool on) => SetAimMode(on);
}
