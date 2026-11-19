using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [SerializeField] int maxHealth = 5;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;

    void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            Debug.LogError("Damage must be greater than zero.", this);
            return;
        }

        if (CurrentHealth <= 0)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        if (CurrentHealth == 0)
            Destroy(gameObject);
    }
}
