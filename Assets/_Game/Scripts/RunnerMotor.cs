using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RunnerMotor : MonoBehaviour
{
    [Header("Running")]
    [SerializeField, Min(0f)] private float forwardSpeed = 8f;

    [Header("Lanes")]
    [SerializeField, Min(0.1f)] private float laneDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float laneChangeSpeed = 12f;

    [Header("Jump")]
    [SerializeField, Min(0.1f)] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -25f;

    private CharacterController controller;
    private int currentLane;
    private float centerX;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // The starting position defines the center lane.
        centerX = transform.position.x;
        currentLane = 0;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * deltaTime;

        float targetX = centerX + currentLane * laneDistance;

        float nextX = Mathf.MoveTowards(
            transform.position.x,
            targetX,
            laneChangeSpeed * deltaTime
        );

        Vector3 movement = new Vector3(
            nextX - transform.position.x,
            verticalVelocity * deltaTime,
            forwardSpeed * deltaTime
        );

        CollisionFlags collisions = controller.Move(movement);

        if ((collisions & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    public void ChangeLane(int direction)
    {
        currentLane = Mathf.Clamp(
            currentLane + direction,
            -1,
            1
        );
    }

    public void Jump()
    {
        if (!controller.isGrounded)
            return;

        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );
    }

    private void OnValidate()
    {
        gravity = Mathf.Min(gravity, -0.1f);
    }
}