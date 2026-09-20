using UnityEngine;

/// Health Controller: changes HealthModel and tells Unity when its view object should be removed.
public sealed class Object_System : MonoBehaviour, Idamage
{
    public int HP = 100;
    public int MaxHp = 100;
    public HealthModel Model { get; } = new HealthModel();

    private void Awake()
    {
        Model.Initialize(MaxHp, HP);
        HP = Model.Current;
    }

    public void Takedamage(int hit)
    {
        Model.TakeDamage(hit);
        HP = Model.Current; // retains current prefab/UI compatibility
        if (Model.IsDead) Destroy(gameObject);
    }
}
