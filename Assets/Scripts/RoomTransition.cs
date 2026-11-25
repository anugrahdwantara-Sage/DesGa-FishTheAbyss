using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RoomTransition : MonoBehaviour
{
    static CharMovement lastTransitionedPlayer;
    static float transitionLockUntil;

    [SerializeField] Transform destination;
    [SerializeField] GameObject[] enableOnEnter;
    [SerializeField] GameObject[] disableOnEnter;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CharMovement movement = other.GetComponentInParent<CharMovement>();
        if (movement == null)
            return;

        if (lastTransitionedPlayer == movement && Time.time < transitionLockUntil)
            return;

        if (destination == null)
        {
            Debug.LogError("RoomTransition needs a destination Transform.", this);
            return;
        }

        lastTransitionedPlayer = movement;
        transitionLockUntil = Time.time + 0.25f;

        if (enableOnEnter != null)
        {
            foreach (GameObject room in enableOnEnter)
            {
                if (room != null)
                    room.SetActive(true);
            }
        }

        movement.transform.position = destination.position;
        movement.ResetAfterRoomTransition();

        if (disableOnEnter != null)
        {
            foreach (GameObject room in disableOnEnter)
            {
                if (room != null && !movement.transform.IsChildOf(room.transform))
                    room.SetActive(false);
            }
        }
    }
}
