using UnityEngine;

public class FallingCoconutSpawner : MonoBehaviour
{
    [SerializeField] private FallingCoconutHazard hazardPrefab;
    [SerializeField] private RunnerMotor player;
    [SerializeField, Min(1f)] private float interval = 4f;
    [SerializeField, Min(1f)] private float activationRange = 30f;
    [SerializeField] private float groundHeight = -0.3f;
    [SerializeField] private int lane = 0;
    private float nextDrop;
    private float centerX;

    public void Configure(RunnerMotor runner, FallingCoconutHazard prefab, int targetLane)
    {
        player = runner;
        hazardPrefab = prefab;
        lane = Mathf.Clamp(targetLane, -1, 1);
        centerX = runner != null ? runner.transform.position.x : 0f;
    }

    private void Start() => nextDrop = Time.time + Random.Range(0.3f, interval);

    private void Update()
    {
        if (player == null || hazardPrefab == null || Time.time < nextDrop) return;
        if (Mathf.Abs(player.transform.position.z - transform.position.z) > activationRange) return;
        if (!player.isActiveAndEnabled) return;
        float x = centerX + Mathf.Clamp(lane, -1, 1) * player.LaneDistance;
        var target = new Vector3(x, groundHeight, transform.position.z + 2f);
        Instantiate(hazardPrefab, target, Quaternion.identity).Initialize(target);
        nextDrop = Time.time + interval;
    }
}
