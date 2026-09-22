using UnityEngine;

public class RunnerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;

    [SerializeField]
    private Vector3 offset = new Vector3(0f, 5f, -8f);

    [Header("Lane Follow")]
    [SerializeField, Range(0f, 1f)]
    private float horizontalFollow = 0.5f;

    [SerializeField, Min(0.01f)]
    private float horizontalSmoothTime = 0.15f;

    private float trackCenterX;
    private float groundReferenceY;
    private float horizontalVelocity;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError(
                "RunnerCamera: Assign Player to Target.",
                this
            );

            enabled = false;
            return;
        }

        trackCenterX = target.position.x;
        groundReferenceY = target.position.y;

        transform.position = new Vector3(
            trackCenterX + offset.x,
            groundReferenceY + offset.y,
            target.position.z + offset.z
        );
    }

    private void LateUpdate()
    {
        float playerSideOffset = target.position.x - trackCenterX;

        float targetX =
            trackCenterX + offset.x
            + playerSideOffset * horizontalFollow;

        float smoothX = Mathf.SmoothDamp(
            transform.position.x,
            targetX,
            ref horizontalVelocity,
            horizontalSmoothTime
        );

        transform.position = new Vector3(
            smoothX,
            groundReferenceY + offset.y,
            target.position.z + offset.z
        );
    }
}