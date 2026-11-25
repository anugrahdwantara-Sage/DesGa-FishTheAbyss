using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterArea : MonoBehaviour
{
    readonly Dictionary<CharMovement, HashSet<Collider2D>> overlappingCharacters =
        new Dictionary<CharMovement, HashSet<Collider2D>>();

    void OnTriggerEnter2D(Collider2D other)
    {
        CharMovement character = other.GetComponentInParent<CharMovement>();
        if (character == null)
            return;

        if (!overlappingCharacters.TryGetValue(character, out HashSet<Collider2D> colliders))
        {
            colliders = new HashSet<Collider2D>();
            overlappingCharacters.Add(character, colliders);
        }

        colliders.Add(other);
        character.SetSwimming(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        CharMovement character = other.GetComponentInParent<CharMovement>();
        if (character == null || !overlappingCharacters.TryGetValue(character, out HashSet<Collider2D> colliders))
            return;

        colliders.Remove(other);
        if (colliders.Count == 0)
        {
            overlappingCharacters.Remove(character);
            character.SetSwimming(false);
        }
    }

    void OnDisable()
    {
        foreach (CharMovement character in overlappingCharacters.Keys)
        {
            if (character != null)
                character.SetSwimming(false);
        }

        overlappingCharacters.Clear();
    }
}
