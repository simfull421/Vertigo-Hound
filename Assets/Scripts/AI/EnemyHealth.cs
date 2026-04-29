using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 50f;
    public float currentHealth;

    public bool IsDead => _isDead;

    public event System.Action<EnemyHealth> OnDeath;

    private bool _isDead;

    void Awake()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        _isDead = false;
        currentHealth = maxHealth;
    }

    /// <summary>
    /// 데미지 적용. 생존 여부를 반환합니다.
    /// </summary>
    public bool TakeHit(float damage, Vector3 hitPoint, Vector3 hitDir, Rigidbody hitRb)
    {
        _ = hitPoint;
        _ = hitDir;
        _ = hitRb;

        if (_isDead) return false;

        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            _isDead = true;
            OnDeath?.Invoke(this);
            return false;
        }

        return true;
    }
}
