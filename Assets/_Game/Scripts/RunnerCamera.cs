using UnityEngine;

public class RunnerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;

    [SerializeField]
    private Vector3 offset = new Vector3(0f, 5f, -8f);

    private float trackCenterX;
    private float groundReferenceY;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError(
                "RunnerCamera: Assign Player to the Target field.",
                this
            );

            enabled = false;
            return;
        }

        trackCenterX = target.position.x;
        groundReferenceY = target.position.y;

        UpdatePosition();
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        transform.position = new Vector3(
            trackCenterX + offset.x,
            groundReferenceY + offset.y,
            target.position.z + offset.z
        );
    }
}