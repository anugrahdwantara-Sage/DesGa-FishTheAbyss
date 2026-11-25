using System.Collections;
using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [SerializeField] int maxHealth = 5;
    [SerializeField] float hurtDuration = 0.583f;
    [SerializeField] float knockbackSpeed = 2.5f;
    [SerializeField] float knockbackLift = 0.25f;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsHurt { get; private set; }

    Rigidbody2D body;
    Animator animator;
    Coroutine hurtCoroutine;

    void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, Transform attacker)
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
        {
            Destroy(gameObject);
            return;
        }

        ApplyKnockback(attacker);
        PlayHurtAnimation();

        if (hurtCoroutine != null)
            StopCoroutine(hurtCoroutine);
        hurtCoroutine = StartCoroutine(RecoverFromHurt());
    }

    void ApplyKnockback(Transform attacker)
    {
        if (body == null || attacker == null)
            return;

        float differenceX = transform.position.x - attacker.position.x;
        float pushDirection;
        if (Mathf.Abs(differenceX) < 0.01f)
        {
            SpriteRenderer attackerSprite = attacker.GetComponent<SpriteRenderer>();
            pushDirection = attackerSprite != null && attackerSprite.flipX ? 1f : -1f;
        }
        else
        {
            pushDirection = Mathf.Sign(differenceX);
        }

        body.linearVelocity = new Vector2(
            pushDirection * knockbackSpeed,
            Mathf.Max(0f, body.linearVelocity.y) + knockbackLift);
    }

    void PlayHurtAnimation()
    {
        if (animator == null || !animator.HasState(0, Animator.StringToHash("Base Layer.hurt")))
        {
            Debug.LogError("Character Animator needs a 'hurt' state.", this);
            return;
        }

        animator.Play("hurt", 0, 0f);
    }

    IEnumerator RecoverFromHurt()
    {
        IsHurt = true;
        yield return new WaitForSeconds(Mathf.Max(0.01f, hurtDuration));
        IsHurt = false;
        hurtCoroutine = null;
    }
}
