using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerAttack))]
public class Enemy1AI : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float detectionRange = 8f;
    [SerializeField] float attackRange = 1.4f;
    [SerializeField] float attackCooldown = 1.5f;
    [SerializeField] float attackDuration = 0.75f;

    Rigidbody2D body;
    Animator animator;
    SpriteRenderer spriteRenderer;
    PlayerAttack playerAttack;
    string currentAnimation;
    float attackTimer;
    float attackCooldownTimer;
    bool isBreaching;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerAttack = GetComponent<PlayerAttack>();
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (target == null)
            Debug.LogWarning("Enemy1AI has no target. Assign the player Transform or use the Player tag.", this);
        else
            playerAttack.RegisterTarget(target.gameObject);

        if (!isBreaching)
            PlayAnimation("idle");
    }

    void Update()
    {
        if (isBreaching)
            return;

        attackCooldownTimer -= Time.deltaTime;

        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            if (attackTimer <= 0f)
                PlayAnimation("idle");
            return;
        }

        if (target == null)
        {
            StopMoving();
            PlayAnimation("idle");
            return;
        }

        float differenceX = target.position.x - transform.position.x;
        float distanceX = Mathf.Abs(differenceX);
        if (distanceX > detectionRange)
        {
            StopMoving();
            PlayAnimation("idle");
            return;
        }

        if (spriteRenderer != null && distanceX > 0.01f)
            spriteRenderer.flipX = differenceX < 0f;

        if (distanceX <= attackRange)
        {
            StopMoving();
            if (attackCooldownTimer <= 0f)
            {
                attackTimer = attackDuration;
                attackCooldownTimer = attackCooldown;
                PlayAnimation("attack");
            }
            else
            {
                PlayAnimation("idle");
            }

            return;
        }

        body.linearVelocity = new Vector2(Mathf.Sign(differenceX) * moveSpeed, body.linearVelocity.y);
        PlayAnimation("walk");
    }

    public void BeginBreach()
    {
        isBreaching = true;
        attackTimer = 0f;
        StopMoving();
        PlayAnimation("breach");
    }

    public void FinishBreach()
    {
        isBreaching = false;
        PlayAnimation("idle");
    }

    public void AttackHitFrame()
    {
        playerAttack.PerformAttack(spriteRenderer != null && spriteRenderer.flipX);
    }

    void StopMoving()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    void PlayAnimation(string animationName)
    {
        if (currentAnimation == animationName)
            return;

        animator.Play(animationName, 0, 0f);
        currentAnimation = animationName;
    }
}
