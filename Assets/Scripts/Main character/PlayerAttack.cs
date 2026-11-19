using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] Collider2D attackArea;
    [SerializeField] float facingRightOffsetX = 1.3f;
    [SerializeField] float facingLeftOffsetX = -1.3f;
    [SerializeField] int damage = 1;

    readonly HashSet<GameObject> attackableTargets = new HashSet<GameObject>();
    readonly HashSet<GameObject> damagedTargets = new HashSet<GameObject>();
    readonly List<Collider2D> overlappingColliders = new List<Collider2D>();
    ContactFilter2D overlapFilter;
    Vector3 attackAreaDefaultPosition;
    bool hasFacingDirection;
    bool facingLeft;

    void Awake()
    {
        if (attackArea == null)
        {
            Transform areaTransform = transform.Find("attackarea");
            if (areaTransform != null)
                attackArea = areaTransform.GetComponent<Collider2D>();
        }

        if (attackArea == null)
        {
            Debug.LogError("Assign the attackarea child Collider2D.", this);
            return;
        }

        attackAreaDefaultPosition = attackArea.transform.localPosition;
        overlapFilter.SetLayerMask(Physics2D.AllLayers);
        overlapFilter.useTriggers = true;
    }

    public void RegisterTarget(GameObject target)
    {
        if (target != null)
            attackableTargets.Add(target);
    }

    public void SetFacing(bool isFacingLeft)
    {
        if (attackArea == null || (hasFacingDirection && facingLeft == isFacingLeft))
            return;

        facingLeft = isFacingLeft;
        hasFacingDirection = true;

        Vector3 localPosition = attackAreaDefaultPosition;
        localPosition.x = facingLeft ? facingLeftOffsetX : facingRightOffsetX;
        attackArea.transform.localPosition = localPosition;
        Physics2D.SyncTransforms();
    }

    public void PerformAttack(bool facingLeft)
    {
        if (attackArea == null)
        {
            Debug.LogError("Cannot attack because the attackarea Collider2D is not assigned.", this);
            return;
        }

        SetFacing(facingLeft);

        damagedTargets.Clear();
        overlappingColliders.Clear();
        attackArea.Overlap(overlapFilter, overlappingColliders);

        for (int i = 0; i < overlappingColliders.Count; i++)
        {
            Collider2D hit = overlappingColliders[i];
            GameObject target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (!attackableTargets.Contains(target) || !damagedTargets.Add(target))
                continue;

            CharacterHealth health = target.GetComponent<CharacterHealth>();
            if (health == null)
            {
                Debug.LogError("Attack target needs a CharacterHealth component.", target);
                continue;
            }

            health.TakeDamage(damage);
            if (health.CurrentHealth <= 0)
                attackableTargets.Remove(target);
        }
    }
}
