using UnityEngine;

/// <summary>
/// The Player view owns Unity objects only. It knows no input keys, weapon rules or game state.
/// </summary>
public sealed class PlayerView : MonoBehaviour
{
    private Rigidbody body;
    private Collider bodyCollider;
    private Transform weapon;
    private Quaternion weaponRestRotation;

    public void Configure(Rigidbody rigidbody, Transform weaponTransform)
    {
        body = rigidbody != null ? rigidbody : GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
        weapon = weaponTransform;
        if (weapon != null) weaponRestRotation = weapon.localRotation;
    }

    public void SetLocallyControlled(bool locallyControlled)
    {
        if (body == null) return;
        body.isKinematic = !locallyControlled;
        if (locallyControlled) body.freezeRotation = true;
    }

    public bool IsGrounded(float extraDistance)
    {
        return bodyCollider != null && Physics.Raycast(bodyCollider.bounds.center, Vector3.down,
            bodyCollider.bounds.extents.y + extraDistance);
    }

    public void Move(Vector3 direction, float force, float maxSpeed)
    {
        if (body == null) return;
        body.AddForce(direction * force, ForceMode.Force);
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
        if (horizontalVelocity.magnitude > maxSpeed)
            body.velocity = horizontalVelocity.normalized * maxSpeed + Vector3.up * body.velocity.y;
    }

    public void Jump(float impulse)
    {
        if (body != null) body.AddForce(Vector3.up * impulse, ForceMode.Impulse);
    }

    public void Face(Vector3 direction, float degreesPerSecond)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= .001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction), degreesPerSecond * Time.fixedDeltaTime);
    }

    public void SetWeaponAim(Vector3 point, bool aiming)
    {
        if (weapon == null) return;
        Quaternion target = weaponRestRotation;
        if (aiming)
        {
            Quaternion look = Quaternion.LookRotation(point - weapon.position, Vector3.up);
            target = weapon.parent == null ? look : Quaternion.Inverse(weapon.parent.rotation) * look;
        }
        weapon.localRotation = Quaternion.Slerp(weapon.localRotation, target, Time.fixedDeltaTime * 10f);
    }
}
