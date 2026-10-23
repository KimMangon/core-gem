/// <summary>Server-side damage contract for gun targets, including future enemies.</summary>
public interface IDamageable
{
    void TakeDamage(int amount, ulong attackerClientId);
}
