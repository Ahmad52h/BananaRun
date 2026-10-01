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
    [Header("Slide")]
    [SerializeField, Min(0.1f)] private float slideDuration = 0.75f;
    [SerializeField, Min(0.5f)] private float slideHeight = 0.8f;

    private CharacterController controller;
    private int currentLane;
    private float centerX;
    private float verticalVelocity;
    private float slowdownStarted;
    private float slowdownDuration;
    private float slowdownMultiplier = 1f;
    private PowerUpManager powerUps;
    private float standingHeight;
    private Vector3 standingCenter;
    private float slideEndsAt;
    private bool sliding;

    public float LaneDistance => laneDistance;
    public float ForwardSpeed => forwardSpeed;
    public float BaseGravity => gravity;
    public bool IsSliding => sliding;
    public float EffectiveGravity => gravity * (powerUps != null && powerUps.IsActive(PowerUpType.Glider) &&
        controller != null && !controller.isGrounded ? 0.35f : 1f);
    public float CurrentSpeed => forwardSpeed * (slowdownDuration > 0f
        ? Mathf.Lerp(slowdownMultiplier, 1f, Mathf.Clamp01((Time.time - slowdownStarted) / slowdownDuration))
        : 1f);

    public void ApplySlowdown(float multiplier, float duration)
    {
        slowdownMultiplier = Mathf.Clamp01(multiplier);
        slowdownDuration = Mathf.Max(0f, duration);
        slowdownStarted = Time.time;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        powerUps = GetComponent<PowerUpManager>();
        standingHeight = controller.height;
        standingCenter = controller.center;

        // The starting position defines the center lane.
        centerX = transform.position.x;
        currentLane = 0;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        if (sliding && Time.time >= slideEndsAt) EndSlide();

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += EffectiveGravity * deltaTime;

        float targetX = centerX + currentLane * laneDistance;

        float nextX = Mathf.MoveTowards(
            transform.position.x,
            targetX,
            laneChangeSpeed * deltaTime
        );

        Vector3 movement = new Vector3(
            nextX - transform.position.x,
            verticalVelocity * deltaTime,
            CurrentSpeed * deltaTime
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
        if (sliding || !controller.isGrounded)
            return;

        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );
    }

    public bool Slide()
    {
        if (!isActiveAndEnabled || sliding || !controller.enabled || !controller.isGrounded)
            return false;
        sliding = true;
        slideEndsAt = Time.time + slideDuration;
        float height = Mathf.Max(controller.radius * 2f, Mathf.Min(slideHeight, standingHeight));
        float bottom = standingCenter.y - standingHeight * 0.5f;
        controller.height = height;
        controller.center = new Vector3(standingCenter.x, bottom + height * 0.5f, standingCenter.z);
        return true;
    }

    private void EndSlide()
    {
        sliding = false;
        controller.height = standingHeight;
        controller.center = standingCenter;
    }

    private void OnDisable()
    {
        if (sliding && controller != null) EndSlide();
    }

    public void ResetVerticalVelocity() => verticalVelocity = -2f;

    private void OnValidate()
    {
        gravity = Mathf.Min(gravity, -0.1f);
        slideHeight = Mathf.Min(slideHeight, GetComponent<CharacterController>()?.height ?? 2f);
    }
}
