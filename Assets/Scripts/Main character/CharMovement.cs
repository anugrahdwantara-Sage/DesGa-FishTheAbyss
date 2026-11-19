using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerAttack))]
public class CharMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float jumpForce = 12.5f;
    [SerializeField] float gravityScale = 2.5f;
    [SerializeField] float fallGravityMultiplier = 1.7f;
    [SerializeField] float jumpReleaseGravityMultiplier = 2f;
    [SerializeField] Camera followCamera;
    [SerializeField] GameObject enemyTemplate;
    [SerializeField] float fishingDuration = 3f;
    [SerializeField] float enemyHorizontalOffset = 1.1f;
    [SerializeField] float enemyLandingOffset = 0.6f;
    [SerializeField] float enemySpawnDepth = 2f;
    [SerializeField] float enemyJumpHeight = 1.5f;
    [SerializeField] float enemyJumpDuration = 0.8f;
    [SerializeField] float attackDuration = 0.0f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Animator animator;
    PlayerAttack playerAttack;
    Vector3 cameraOffset;
    readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    bool jumpHeld;
    bool isFishing;
    bool isAttacking;
    string currentAnimation;
    float fishingTimer;
    float attackTimer;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
        if (followCamera == null)
            followCamera = Camera.main;
        if (followCamera != null)
            cameraOffset = followCamera.transform.position - transform.position;

        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.gravityScale = gravityScale;
    }

    void FixedUpdate()
    {
        float gravityMultiplier = 1f;
        if (body.linearVelocity.y < 0f)
            gravityMultiplier = fallGravityMultiplier;
        else if (body.linearVelocity.y > 0f && !jumpHeld)
            gravityMultiplier = jumpReleaseGravityMultiplier;

        body.gravityScale = gravityScale * gravityMultiplier;
    }

    void LateUpdate()
    {
        if (followCamera != null)
            followCamera.transform.position = transform.position + cameraOffset;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        jumpHeld = keyboard != null && keyboard.spaceKey.isPressed;
        playerAttack.SetFacing(spriteRenderer != null && spriteRenderer.flipX);

        if (keyboard != null && keyboard.fKey.wasPressedThisFrame && !isFishing && !isAttacking)
        {
            isFishing = true;
            fishingTimer = fishingDuration;
            PlayAnimation("fishing");
        }

        if (isFishing)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            fishingTimer -= Time.deltaTime;
            if (fishingTimer <= 0f)
            {
                isFishing = false;
                PlayAnimation("idle");
                SpawnEnemy();
            }

            return;
        }

        Mouse mouse = Mouse.current;
        bool attackPressed = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
        attackPressed |= mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (attackPressed && !isAttacking)
            StartAttack();

        if (isAttacking)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                PlayAnimation("idle");
            }

            return;
        }

        if (keyboard == null)
        {
            UpdateLocomotionAnimation(0f);
            return;
        }

        float input = 0f;
        if (keyboard.aKey.isPressed)
            input -= 1f;
        if (keyboard.dKey.isPressed)
            input += 1f;

        Vector2 velocity = body.linearVelocity;
        velocity.x = input * moveSpeed;

        if (keyboard.spaceKey.wasPressedThisFrame && IsGrounded())
            velocity.y = jumpForce;

        body.linearVelocity = velocity;

        UpdateLocomotionAnimation(input);

        if (input != 0f && spriteRenderer != null)
        {
            spriteRenderer.flipX = input < 0f;
            playerAttack.SetFacing(spriteRenderer.flipX);
        }
    }

    void SpawnEnemy()
    {
        if (enemyTemplate == null)
        {
            Debug.LogError("Assign an enemy template before fishing.", this);
            return;
        }

        float facingDirection = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        Vector3 landingPosition = transform.position + new Vector3(
            facingDirection * enemyHorizontalOffset,
            -enemyLandingOffset,
            0f);
        Vector3 spawnPosition = landingPosition + Vector3.down * enemySpawnDepth;

        GameObject enemy = Instantiate(enemyTemplate, spawnPosition, enemyTemplate.transform.rotation);
        enemy.name = enemyTemplate.name + " (Spawned)";
        enemy.SetActive(true);

        Rigidbody2D enemyBody = enemy.GetComponent<Rigidbody2D>();
        Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
        Enemy1AI enemyAI = enemy.GetComponent<Enemy1AI>();
        if (enemyBody == null || enemyCollider == null)
        {
            Debug.LogError("The enemy template needs a Rigidbody2D and a Collider2D.", enemy);
            Destroy(enemy);
            return;
        }

        enemyBody.simulated = false;
        enemyCollider.enabled = false;
        playerAttack.RegisterTarget(enemy);
        if (enemyAI != null)
            enemyAI.BeginBreach();
        else
        {
            Animator enemyAnimator = enemy.GetComponent<Animator>();
            if (enemyAnimator == null)
                Debug.LogError("The enemy template needs an Animator with a breach state.", enemy);
            else
                enemyAnimator.Play("breach", 0, 0f);
        }

        StartCoroutine(AnimateEnemySpawn(
            enemy.transform,
            enemyBody,
            enemyCollider,
            enemyAI,
            spawnPosition,
            landingPosition));
    }

    void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackDuration;
        PlayAnimation("attack");
    }

    public void AttackHitFrame()
    {
        playerAttack.PerformAttack(spriteRenderer != null && spriteRenderer.flipX);
    }

    IEnumerator AnimateEnemySpawn(
        Transform enemyTransform,
        Rigidbody2D enemyBody,
        Collider2D enemyCollider,
        Enemy1AI enemyAI,
        Vector3 startPosition,
        Vector3 landingPosition)
    {
        float elapsed = 0f;
        while (elapsed < enemyJumpDuration)
        {
            float progress = elapsed / enemyJumpDuration;
            float arcHeight = 4f * enemyJumpHeight * progress * (1f - progress);
            enemyTransform.position = Vector3.Lerp(startPosition, landingPosition, progress) + Vector3.up * arcHeight;
            elapsed += Time.deltaTime;
            yield return null;
        }

        enemyTransform.position = landingPosition;
        enemyCollider.enabled = true;
        enemyBody.simulated = true;
        enemyBody.linearVelocity = Vector2.zero;
        if (enemyAI != null)
            enemyAI.FinishBreach();
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

    void UpdateLocomotionAnimation(float input)
    {
        if (body.linearVelocity.y > 0.01f)
        {
            PlayAnimation("jump");
            return;
        }

        if (!IsGrounded())
        {
            PlayAnimation("fall");
            return;
        }

        PlayAnimation(input != 0f ? "run" : "idle");
    }

    void PlayAnimation(string animationName)
    {
        if (animator == null || currentAnimation == animationName)
            return;

        animator.Play(animationName);
        currentAnimation = animationName;
    }
}
