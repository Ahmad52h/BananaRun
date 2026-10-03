using UnityEngine;
using MaekoStudio.Flock;

/// <summary>Moves the canopy flock ahead of the runner; river flock keeps its own patrol.</summary>
public class CanopyFlockFollow : MonoBehaviour
{
    [SerializeField] private RunnerMotor player;
    private void LateUpdate()
    {
        if (player != null) transform.position = new Vector3(0, 19f, player.transform.position.z + 38f);
    }
}
