using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(CharacterHealth))]
public class CharMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float jumpForce = 12.5f;
    [SerializeField] float gravityScale = 2.5f;
    [SerializeField] float fallGravityMultiplier = 1.7f;
    [SerializeField] float jumpReleaseGravityMultiplier = 2f;
    [SerializeField] float coyoteTime = 0.11f;
    [SerializeField] float jumpBufferDuration = 0.12f;
    [SerializeField] bool dashUnlocked;
    [SerializeField] float dashSpeed = 15f;
    [SerializeField] float dashDuration = 0.18f;
    [SerializeField] float dashCooldown = 0.45f;
    [SerializeField] bool doubleJumpUnlocked;
    [SerializeField] bool wallClingEnabled = true;
    [SerializeField] float wallSlideMaxFallSpeed = 2.8f;
    [SerializeField] float wallJumpHorizontalSpeed = 7.4f;
    [SerializeField] float wallJumpVerticalSpeed = 11.8f;
    [SerializeField] float swimSpeed = 4f;
    [SerializeField] float swimGravityScale = 0f;
    [SerializeField] Camera followCamera;
    [SerializeField] GameObject enemyTemplate;
    [SerializeField] float fishingDuration = 3f;
    [SerializeField] float enemyLandingOffset = 0.6f;
    [SerializeField] float enemySpawnDepth = 2f;
    [SerializeField] float enemyJumpDuration = 0.8f;
    [SerializeField] float attackDuration = 0.4f;
    [SerializeField] Vector2 fishingLineAnchorOffset = new Vector2(0.25f, 0.1f);
    [SerializeField] float fishingLineLength = 3.4f;
    [SerializeField] float fishingLineDrop = 1.4f;
    [SerializeField] float fishingCastArcHeight = 1.2f;
    [SerializeField] float fishingCastDuration = 0.45f;
    [SerializeField] float fishingRetractDuration = 0.25f;
    [SerializeField] float fishingLineWidth = 0.025f;
    [SerializeField] float fishingBobberRadius = 0.08f;

    Rigidbody2D body;
    Collider2D playerCollider;
    SpriteRenderer spriteRenderer;
    Animator animator;
    PlayerAttack playerAttack;
    CharacterHealth characterHealth;
    Vector3 cameraOffset;
    readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    bool jumpHeld;
    bool isFishing;
    bool isAttacking;
    bool isSwimming;
    readonly HashSet<Collider2D> leftFishingAreas = new HashSet<Collider2D>();
    readonly HashSet<Collider2D> rightFishingAreas = new HashSet<Collider2D>();
    readonly List<Collider2D> overlappingColliders = new List<Collider2D>();
    ContactFilter2D fishingAreaFilter;
    float coyoteTimer;
    float jumpBufferTimer;
    float dashTimer;
    float dashCooldownTimer;
    float dashDirection = 1f;
    int airJumpsRemaining;
    string currentAnimation;
    float fishingTimer;
    float attackTimer;
    LineRenderer fishingLine;
    LineRenderer fishingBobber;
    Material fishingLineMaterial;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
        characterHealth = GetComponent<CharacterHealth>();
        if (followCamera == null)
            followCamera = Camera.main;
        if (followCamera != null)
            cameraOffset = followCamera.transform.position - transform.position;

        fishingAreaFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };

        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.gravityScale = gravityScale;
    }

    void FixedUpdate()
    {
        if (IsSwimming())
        {
            body.gravityScale = swimGravityScale;
            return;
        }

        if (dashTimer > 0f)
        {
            body.gravityScale = 0f;
            return;
        }

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
        RefreshFishingAreas();
        bool swimming = IsSwimming();
        jumpHeld = keyboard != null && keyboard.spaceKey.isPressed;
        playerAttack.SetFacing(spriteRenderer != null && spriteRenderer.flipX);

        if (characterHealth.IsHurt)
        {
            currentAnimation = null;
            return;
        }

        if (keyboard != null && keyboard.fKey.wasPressedThisFrame &&
            !isFishing && !isAttacking && dashTimer <= 0f)
        {
            if (swimming)
            {
                Debug.Log("You cannot fish or attack while swimming.", this);
            }
            else if (!CanFishFacingCurrentDirection())
            {
                Debug.Log("Enter a canFishleft or canfishright trigger and face away from the pond to fish.", this);
            }
            else
            {
                isFishing = true;
                fishingTimer = fishingDuration;
                CreateFishingVisuals();
                PlayAnimation("fishing");
            }
        }

        if (isFishing)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            fishingTimer -= Time.deltaTime;
            UpdateFishingVisual();
            if (fishingTimer <= 0f)
            {
                isFishing = false;
                SetFishingVisualsVisible(false);
                PlayAnimation("idle");
                SpawnEnemy();
            }

            return;
        }

        Mouse mouse = Mouse.current;
        bool attackPressed = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
        attackPressed |= mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (attackPressed && !swimming && !isAttacking && dashTimer <= 0f)
            StartAttack();

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
                isAttacking = false;
        }

        float input = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input += 1f;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = input * (swimming ? swimSpeed : moveSpeed);

        bool jumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        if (jumpPressed)
            jumpBufferTimer = jumpBufferDuration;
        else
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);

        coyoteTimer = IsGrounded()
            ? coyoteTime
            : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);

        if (swimming && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            isSwimming = false;
            velocity.y = jumpForce;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
        else if (swimming)
        {
            float verticalInput = 0f;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.spaceKey.isPressed)
                    verticalInput += 1f;
                if (keyboard.sKey.isPressed)
                    verticalInput -= 1f;
            }

            Vector2 swimInput = new Vector2(input, verticalInput);
            if (swimInput.sqrMagnitude > 1f)
                swimInput.Normalize();

                velocity.x = swimInput.x * swimSpeed;
                velocity.y = swimInput.y * swimSpeed;
        }
        else
        {
            int wallDirection = GetWallDirection();
            bool dashPressed = keyboard != null &&
                (keyboard.leftShiftKey.wasPressedThisFrame ||
                 keyboard.rightShiftKey.wasPressedThisFrame ||
                 keyboard.lKey.wasPressedThisFrame);

            if (dashUnlocked && dashPressed && dashCooldownTimer <= 0f && !isAttacking && !isFishing)
            {
                dashDirection = input != 0f
                    ? Mathf.Sign(input)
                    : spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
                dashTimer = dashDuration;
                dashCooldownTimer = dashCooldown;
                velocity = new Vector2(dashDirection * dashSpeed, 0f);
            }
            else if (dashTimer > 0f)
            {
                dashTimer = Mathf.Max(0f, dashTimer - Time.deltaTime);
                velocity = new Vector2(dashDirection * dashSpeed, 0f);
            }
            else
            {
                if (wallClingEnabled && !IsGrounded() && wallDirection != 0 &&
                    velocity.y < -wallSlideMaxFallSpeed)
                {
                    velocity.y = -wallSlideMaxFallSpeed;
                }

                if (jumpBufferTimer > 0f)
                {
                    if (coyoteTimer > 0f)
                    {
                        velocity.y = jumpForce;
                        coyoteTimer = 0f;
                        airJumpsRemaining = doubleJumpUnlocked ? 1 : 0;
                        jumpBufferTimer = 0f;
                    }
                    else if (wallClingEnabled && wallDirection != 0)
                    {
                        velocity.x = -wallDirection * wallJumpHorizontalSpeed;
                        velocity.y = wallJumpVerticalSpeed;
                        if (spriteRenderer != null)
                            spriteRenderer.flipX = wallDirection > 0;
                        airJumpsRemaining = doubleJumpUnlocked ? 1 : 0;
                        jumpBufferTimer = 0f;
                    }
                    else if (doubleJumpUnlocked && airJumpsRemaining > 0)
                    {
                        velocity.y = jumpForce;
                        airJumpsRemaining--;
                        jumpBufferTimer = 0f;
                    }
                }
            }
        }

        body.linearVelocity = velocity;

        if (dashTimer > 0f)
            UpdateLocomotionAnimation(input);
        else if (isAttacking)
            PlayAnimation("attack");
        else if (swimming)
            PlayAnimation(input != 0f || velocity.y != 0f ? "run" : "idle");
        else
            UpdateLocomotionAnimation(input);

        if (input != 0f && spriteRenderer != null)
        {
            spriteRenderer.flipX = input < 0f;
            playerAttack.SetFacing(spriteRenderer.flipX);
        }
    }

    void OnDisable()
    {
        leftFishingAreas.Clear();
        rightFishingAreas.Clear();
        overlappingColliders.Clear();
    }

    public void SetSwimming(bool swimming)
    {
        isSwimming = swimming;
        if (swimming && !IsInsideFishingArea())
        {
            dashTimer = 0f;
            isAttacking = false;
            attackTimer = 0f;
            isFishing = false;
            fishingTimer = 0f;
            SetFishingVisualsVisible(false);
        }
    }

    bool IsSwimming()
    {
        return isSwimming && !IsInsideFishingArea();
    }

    void RefreshFishingAreas()
    {
        leftFishingAreas.Clear();
        rightFishingAreas.Clear();
        overlappingColliders.Clear();

        if (playerCollider == null)
            return;

        playerCollider.Overlap(fishingAreaFilter, overlappingColliders);
        foreach (Collider2D area in overlappingColliders)
        {
            if (area == null)
                continue;

            if (string.Equals(area.gameObject.name, "canFishleft", StringComparison.OrdinalIgnoreCase))
                leftFishingAreas.Add(area);
            else if (string.Equals(area.gameObject.name, "canfishright", StringComparison.OrdinalIgnoreCase))
                rightFishingAreas.Add(area);
        }
    }

    bool CanFishFacingCurrentDirection()
    {
        bool facingLeft = spriteRenderer != null && spriteRenderer.flipX;
        return (HasActiveFishingArea(leftFishingAreas) && !facingLeft) ||
            (HasActiveFishingArea(rightFishingAreas) && facingLeft);
    }

    bool IsInsideFishingArea()
    {
        return HasActiveFishingArea(leftFishingAreas) || HasActiveFishingArea(rightFishingAreas);
    }

    bool HasActiveFishingArea(HashSet<Collider2D> fishingAreas)
    {
        foreach (Collider2D area in fishingAreas)
        {
            if (area != null && area.enabled && area.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    public void UnlockDash()
    {
        dashUnlocked = true;
    }

    public void UnlockDoubleJump()
    {
        doubleJumpUnlocked = true;
    }

    public void ResetAfterRoomTransition()
    {
        body.linearVelocity = Vector2.zero;
        isFishing = false;
        SetFishingVisualsVisible(false);
        isAttacking = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        airJumpsRemaining = 0;
        fishingTimer = 0f;
        attackTimer = 0f;
    }

    void OnDestroy()
    {
        if (fishingLineMaterial != null)
            Destroy(fishingLineMaterial);
    }

    void CreateFishingVisuals()
    {
        if (fishingLine != null && fishingBobber != null)
        {
            SetFishingVisualsVisible(true);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("No compatible shader is available for the procedural fishing line.", this);
            return;
        }

        fishingLineMaterial = new Material(shader);
        fishingLine = CreateFishingLineRenderer("Fishing Line", fishingLineWidth, new Color(0.12f, 0.09f, 0.07f));
        fishingBobber = CreateFishingLineRenderer("Fishing Bobber", fishingBobberRadius * 0.55f, new Color(1f, 0.36f, 0.18f));
        SetFishingVisualsVisible(false);
    }

    LineRenderer CreateFishingLineRenderer(string objectName, float width, Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = objectName == "Fishing Bobber";
        line.positionCount = objectName == "Fishing Bobber" ? 16 : 20;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sharedMaterial = fishingLineMaterial;
        line.sortingOrder = 20;
        line.numCapVertices = 2;
        return line;
    }

    void UpdateFishingVisual()
    {
        if (fishingLine == null || fishingBobber == null)
            return;

        SetFishingVisualsVisible(true);

        float facing = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        Vector3 anchor = transform.position + new Vector3(
            Mathf.Abs(fishingLineAnchorOffset.x) * facing,
            fishingLineAnchorOffset.y,
            0f);
        Vector3 target = anchor + new Vector3(facing * fishingLineLength, -fishingLineDrop, 0f);
        float elapsed = Mathf.Max(0f, fishingDuration - fishingTimer);
        float castDuration = Mathf.Max(0.01f, fishingCastDuration);
        float retractDuration = Mathf.Max(0.01f, fishingRetractDuration);
        float retractStart = Mathf.Max(castDuration, fishingDuration - retractDuration);
        Vector3 bobberPosition;

        if (elapsed < castDuration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / castDuration);
            bobberPosition = Vector3.Lerp(anchor, target, progress);
            bobberPosition.y += 4f * fishingCastArcHeight * progress * (1f - progress);
        }
        else if (elapsed >= retractStart)
        {
            float progress = Mathf.Clamp01((elapsed - retractStart) / retractDuration);
            bobberPosition = Vector3.Lerp(target, anchor, Mathf.SmoothStep(0f, 1f, progress));
        }
        else
        {
            bobberPosition = target;
            bobberPosition.y += Mathf.Sin(Time.time * 2.8f) * 0.045f;
        }

        float sag = Mathf.Min(0.45f, Vector3.Distance(anchor, bobberPosition) * 0.08f);
        int linePoints = fishingLine.positionCount;
        for (int i = 0; i < linePoints; i++)
        {
            float progress = i / (float)(linePoints - 1);
            Vector3 point = Vector3.Lerp(anchor, bobberPosition, progress);
            point.y -= 4f * sag * progress * (1f - progress);
            fishingLine.SetPosition(i, point);
        }

        int bobberPoints = fishingBobber.positionCount;
        for (int i = 0; i < bobberPoints; i++)
        {
            float angle = i * Mathf.PI * 2f / bobberPoints;
            fishingBobber.SetPosition(i, bobberPosition + new Vector3(
                Mathf.Cos(angle) * fishingBobberRadius,
                Mathf.Sin(angle) * fishingBobberRadius,
                0f));
        }
    }

    void SetFishingVisualsVisible(bool visible)
    {
        if (fishingLine != null)
            fishingLine.enabled = visible;
        if (fishingBobber != null)
            fishingBobber.enabled = visible;
    }

    void SpawnEnemy()
    {
        if (enemyTemplate == null)
        {
            Debug.LogError("Assign an enemy template before fishing.", this);
            return;
        }

        Enemy1AI enemyTemplateAI = enemyTemplate.GetComponent<Enemy1AI>();
        float launchHorizontalOffset = enemyTemplateAI != null
            ? enemyTemplateAI.LaunchHorizontalOffset
            : 0f;
        float launchJumpHeight = enemyTemplateAI != null
            ? enemyTemplateAI.LaunchJumpHeight
            : 1.5f;
        float facingDirection = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        float landingOffsetTowardPlayer = enemyTemplateAI != null
            ? enemyTemplateAI.LandingOffsetTowardPlayer
            : 0f;
        Vector3 landingPosition = transform.position + new Vector3(
            -facingDirection * landingOffsetTowardPlayer,
            -enemyLandingOffset,
            0f);
        Vector3 spawnPosition = landingPosition +
            new Vector3(facingDirection * launchHorizontalOffset, -enemySpawnDepth, 0f);

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
            landingPosition,
            launchJumpHeight));
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
        Vector3 landingPosition,
        float launchJumpHeight)
    {
        float elapsed = 0f;
        while (elapsed < enemyJumpDuration)
        {
            float progress = elapsed / enemyJumpDuration;
            float horizontalProgress = 1f - (1f - progress) * (1f - progress);
            float arcHeight = 4f * launchJumpHeight * progress * (1f - progress);
            enemyTransform.position = new Vector3(
                Mathf.Lerp(startPosition.x, landingPosition.x, horizontalProgress),
                Mathf.Lerp(startPosition.y, landingPosition.y, progress) + arcHeight,
                landingPosition.z);
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
            if (contacts[i].normal.y > 0f)
                return true;
        }

        return false;
    }

    int GetWallDirection()
    {
        int contactCount = body.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            if (Mathf.Abs(contacts[i].normal.x) > 0.5f)
                return contacts[i].normal.x > 0f ? -1 : 1;
        }

        return 0;
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
