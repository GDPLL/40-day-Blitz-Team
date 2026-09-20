using System;

/// Pure weapon rules: ammo and fire cooldown only.
[Serializable]
public sealed class WeaponModel
{
    public int Ammo { get; private set; }
    public int Capacity { get; private set; }
    public float NextFireTime { get; private set; }
    public void Initialize(int ammo, int capacity) { Capacity = Math.Max(1, capacity); Ammo = Math.Max(0, Math.Min(ammo, Capacity)); }
    public bool TryFire(float now, float fireRate)
    {
        if (Ammo <= 0 || now < NextFireTime) return false;
        Ammo--; NextFireTime = now + 1f / Math.Max(.01f, fireRate); return true;
    }
    public void Reload() => Ammo = Capacity;
}
