using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DynamicRunnerHazard : MonoBehaviour
{
    public enum Motion { Bird, Warthog, Boulder }
    [SerializeField] private Motion motion;
    [SerializeField] private Transform visual;
    [SerializeField] private Transform[] legs;
    [SerializeField, Min(0f)] private float speed = 2f;
    [SerializeField, Min(1f)] private float activationDistance = 26f;
    [SerializeField, Min(0f)] private float travelLimit = 7f;
    [SerializeField, Min(0.1f)] private float rollingRadius = 0.65f;
    [SerializeField] private AudioSource rumble;
    private Rigidbody body;
    private RunnerMotor player;
    private Vector3 origin, visualOrigin;
    private Quaternion visualRotation;
    private float travelled, age;
    public Motion Kind => motion;
    public float DistanceTravelled => travelled;
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        player = FindFirstObjectByType<RunnerMotor>();
        if (visual != null) { visualOrigin = visual.localPosition; visualRotation = visual.localRotation; }
    }
    private void OnEnable()
    {
        origin = transform.position;
        travelled = age = 0;
        if (visual != null) { visual.localPosition = visualOrigin; visual.localRotation = visualRotation; }
    }
    private void Start()
    {
        if (motion != Motion.Bird) return;
        var animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.HasState(0, Animator.StringToHash("Maeko_Glide")))
            animator.CrossFadeInFixedTime("Maeko_Glide", 0.15f);
    }
    private void FixedUpdate()
    {
        if (player == null) return;
        float distance = origin.z - player.transform.position.z;
        if (distance > activationDistance || distance < -12f || !player.enabled)
        {
            if (rumble != null) rumble.Stop();
            return;
        }
        age += Time.fixedDeltaTime;
        float step = Mathf.Min(speed * Time.fixedDeltaTime, travelLimit - travelled);
        travelled += step;
        body.MovePosition(origin + Vector3.back * travelled);
        if (rumble != null)
        {
            if (step > 0 && !rumble.isPlaying) rumble.Play();
            if (step <= 0) rumble.Stop();
        }
        if (visual == null) return;
        if (motion == Motion.Boulder)
            visual.Rotate(Vector3.left, step / rollingRadius * Mathf.Rad2Deg, Space.World);
        else if (motion == Motion.Bird)
        {
            visual.localPosition = visualOrigin + Vector3.up * (Mathf.Sin(age * 2.8f) * 0.1f);
            visual.localRotation = visualRotation * Quaternion.Euler(Mathf.Cos(age * 2.8f) * 5f, 0, Mathf.Sin(age * 1.6f) * 7f);
        }
        else
        {
            visual.localPosition = visualOrigin + Vector3.up * (Mathf.Abs(Mathf.Sin(age * 9f)) * 0.065f);
            visual.localRotation = visualRotation * Quaternion.Euler(Mathf.Sin(age * 9f) * 2f, 0, 0);
            if (legs != null) for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(Mathf.Sin(age * 9f + i % 2 * Mathf.PI) * 23f, 0, 0);
        }
    }
    private void OnDisable() { if (rumble != null) rumble.Stop(); }
}
