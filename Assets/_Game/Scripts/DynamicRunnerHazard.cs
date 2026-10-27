using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DynamicRunnerHazard : MonoBehaviour
{
    public enum Motion
    {
        Bird,
        Warthog,
        Boulder,
        RollingLog,
        Barrier
    }

    [SerializeField] private Motion motion;
    [SerializeField] private Transform visual;
    [SerializeField] private Transform[] legs;

    [SerializeField, Min(0f)]
    private float speed = 2f;

    [SerializeField, Min(1f)]
    private float activationDistance = 26f;

    [SerializeField, Min(0f)]
    private float travelLimit = 7f;

    [SerializeField, Min(0.1f)]
    private float rollingRadius = 0.65f;

    [SerializeField]
    private AudioSource rumble;

    private Rigidbody body;
    private RunnerMotor player;
    private RivalRunner rival;
    private PlayerHealth playerHealth;

    private Vector3 origin;
    private Vector3 visualOrigin;
    private Quaternion visualRotation;

    private float travelled;
    private float age;

    private RunnerCourseGenerator difficultyOwner;

    public Motion Kind => motion;
    public float DistanceTravelled => travelled;
    public float TravelLimit => travelLimit;
    public RunnerMotor TargetPlayer => player;
    public RivalRunner TargetRival => rival;

    public float EffectiveSpeed =>
        speed *
        (difficultyOwner != null
            ? difficultyOwner.CurrentHazardSpeedMultiplier
            : 1f);

    public void Configure(
        RunnerMotor runner,
        RunnerCourseGenerator course)
    {
        player = runner;
        BindCompetitors();

        difficultyOwner =
            course != null &&
            course.Player == runner
                ? course
                : null;
    }

    private void BindCompetitors()
    {
        playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        rival = null;
        foreach (var competitor in FindObjectsByType<RivalRunner>(FindObjectsSortMode.None))
            if (competitor.Player == player) { rival = competitor; break; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        // Standalone hazards automatically bind only
        // when exactly one RunnerMotor exists.
        RunnerMotor[] runners =
            FindObjectsByType<RunnerMotor>(
                FindObjectsSortMode.None
            );

        if (runners.Length == 1)
        {
            player = runners[0];
            BindCompetitors();
        }

        if (visual != null)
        {
            visualOrigin = visual.localPosition;
            visualRotation = visual.localRotation;
        }
    }

    private void OnEnable()
    {
        origin = transform.position;

        travelled = 0f;
        age = 0f;

        // Only Rolling Boulder should be silent.
        if (motion == Motion.Boulder && rumble != null)
        {
            rumble.Stop();
            rumble.playOnAwake = false;
            rumble.mute = true;
        }

        if (visual != null)
        {
            visual.localPosition = visualOrigin;
            visual.localRotation = visualRotation;
        }
    }

    private void Start()
    {
        if (motion != Motion.Bird)
            return;

        Animator animator =
            GetComponentInChildren<Animator>();

        if (animator != null &&
            animator.HasState(
                0,
                Animator.StringToHash("Maeko_Glide")
            ))
        {
            animator.CrossFadeInFixedTime(
                "Maeko_Glide",
                0.15f
            );
        }
    }

    private void FixedUpdate()
    {
        if (player == null)
            return;

        float playerDistance = origin.z - player.transform.position.z;
        float rivalDistance = rival != null ? origin.z - rival.transform.position.z : float.PositiveInfinity;
        bool playerApproaching = player.enabled && playerDistance <= activationDistance && playerDistance >= -12f;
        bool rivalApproaching = rival != null && rival.isActiveAndEnabled && !rival.HasFinished &&
            rivalDistance <= activationDistance && rivalDistance >= -12f;
        if ((playerHealth != null && playerHealth.IsRunOver) || (!playerApproaching && !rivalApproaching))
        {
            if (rumble != null)
                rumble.Stop();

            return;
        }

        age += Time.fixedDeltaTime;

        float remainingDistance =
            Mathf.Max(0f, travelLimit - travelled);

        float step =
            Mathf.Min(
                EffectiveSpeed * Time.fixedDeltaTime,
                remainingDistance
            );

        travelled += step;

        body.MovePosition(
            origin + Vector3.back * travelled
        );

        // Other moving hazards keep their sounds.
        // Boulder stays silent.
        if (rumble != null &&
            motion != Motion.Boulder)
        {
            if (step > 0f &&
                !rumble.isPlaying)
            {
                rumble.Play();
            }

            if (step <= 0f &&
                rumble.isPlaying)
            {
                rumble.Stop();
            }
        }

        if (visual == null)
            return;

        if (motion == Motion.Boulder || motion == Motion.RollingLog)
        {
            visual.Rotate(
                Vector3.left,
                step / rollingRadius *
                Mathf.Rad2Deg,
                Space.World
            );
        }
        else if (motion == Motion.Bird)
        {
            visual.localPosition =
                visualOrigin +
                Vector3.up *
                (
                    Mathf.Sin(age * 2.8f) *
                    0.1f
                );

            visual.localRotation =
                visualRotation *
                Quaternion.Euler(
                    Mathf.Cos(age * 2.8f) * 5f,
                    0f,
                    Mathf.Sin(age * 1.6f) * 7f
                );
        }
        else if (motion == Motion.Warthog)
        {
            // Warthog / crocodile style movement.
            visual.localPosition =
                visualOrigin +
                Vector3.up *
                (
                    Mathf.Abs(
                        Mathf.Sin(age * 9f)
                    ) *
                    0.065f
                );

            visual.localRotation =
                visualRotation *
                Quaternion.Euler(
                    Mathf.Sin(age * 9f) * 2f,
                    0f,
                    0f
                );

            if (legs != null)
            {
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null)
                        continue;

                    legs[i].localRotation =
                        Quaternion.Euler(
                            Mathf.Sin(
                                age * 9f +
                                (i % 2) *
                                Mathf.PI
                            ) * 23f,
                            0f,
                            0f
                        );
                }
            }
        }
    }

    private void OnDisable()
    {
        if (rumble != null)
        {
            rumble.Stop();
        }
    }
}
