using System;

[Serializable]
public sealed class HealthModel
{
    public int Current { get; private set; }
    public int Max { get; private set; }
    public bool IsDead => Current == 0;
    public void Initialize(int max, int current) { Max = Math.Max(1, max); Current = Math.Max(0, Math.Min(current, Max)); }
    public void TakeDamage(int amount) => Current = Math.Max(0, Current - Math.Max(0, amount));
}
