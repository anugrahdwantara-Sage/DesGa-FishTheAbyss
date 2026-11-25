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
    [SerializeField] float launchHorizontalOffset = 2.5f;
    [SerializeField] float launchJumpHeight = 1.5f;
    [SerializeField] float landingOffsetTowardPlayer = 0.6f;
    [SerializeField] float chaseJumpForce = 8f;
    [SerializeField] float chaseJumpCooldown = 0.8f;
    [SerializeField] float chaseJumpTargetHeight = 0.35f;

    public float LaunchHorizontalOffset => launchHorizontalOffset;
    public float LaunchJumpHeight => launchJumpHeight;
    public float LandingOffsetTowardPlayer => landingOffsetTowardPlayer;

    readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    Rigidbody2D body;
    Animator animator;
    SpriteRenderer spriteRenderer;
    PlayerAttack playerAttack;
    CharacterHealth characterHealth;
    string currentAnimation;
    float attackTimer;
    float attackCooldownTimer;
    float chaseJumpCooldownTimer;
    bool isBreaching;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerAttack = GetComponent<PlayerAttack>();
        characterHealth = GetComponent<CharacterHealth>();
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

        if (characterHealth.IsHurt)
        {
            currentAnimation = null;
            return;
        }

        attackCooldownTimer -= Time.deltaTime;
        chaseJumpCooldownTimer = Mathf.Max(0f, chaseJumpCooldownTimer - Time.deltaTime);

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

        float moveDirection = Mathf.Sign(differenceX);
        TryJumpTowardTarget(moveDirection);

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

        body.linearVelocity = new Vector2(moveDirection * moveSpeed, body.linearVelocity.y);
        PlayAnimation("walk");
    }

    void TryJumpTowardTarget(float moveDirection)
    {
        if (chaseJumpCooldownTimer > 0f)
            return;

        bool targetIsHigher = target.position.y - transform.position.y > chaseJumpTargetHeight;
        bool grounded = IsGrounded();
        bool canLeapFromWater = targetIsHigher && body.linearVelocity.y <= 0f;
        if (!grounded && !canLeapFromWater)
            return;

        if (!targetIsHigher && !IsBlockedInDirection(moveDirection))
            return;

        body.linearVelocity = new Vector2(body.linearVelocity.x, chaseJumpForce);
        chaseJumpCooldownTimer = chaseJumpCooldown;
    }

    bool IsGrounded()
    {
        int contactCount = body.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            if (contacts[i].normal.y > 0.5f)
                return true;
        }

        return false;
    }

    bool IsBlockedInDirection(float direction)
    {
        int contactCount = body.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            if (contacts[i].normal.x * direction < -0.5f)
                return true;
        }

        return false;
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
