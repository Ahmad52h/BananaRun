using System.Collections;
using UnityEngine;

public class LaneChangeVisual : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform characterVisual;

    [Header("Lane Change Feel")]
    [SerializeField] private float leanAngle = 15f;
    [SerializeField] private float turnAngle = 10f;
    [SerializeField] private float animationTime = 0.18f;

    private Quaternion startRotation;
    private Coroutine currentAnimation;

    private void Awake()
    {
        if (characterVisual == null)
            characterVisual = transform;

        startRotation = characterVisual.localRotation;
    }

    public void PlayLaneChange(int direction)
    {
        // direction:
        // -1 = Left
        //  1 = Right

        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(
            LaneAnimation(Mathf.Clamp(direction, -1, 1))
        );
    }

    private IEnumerator LaneAnimation(int direction)
    {
        Quaternion leanRotation =
            startRotation *
            Quaternion.Euler(
                0f,
                direction * turnAngle,
                -direction * leanAngle
            );

        float halfTime = animationTime * 0.5f;

        // Lean into the lane
        float t = 0f;

        while (t < halfTime)
        {
            t += Time.deltaTime;

            float normalized = Mathf.Clamp01(t / halfTime);
            normalized = Smooth(normalized);

            characterVisual.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    leanRotation,
                    normalized
                );

            yield return null;
        }

        // Return upright
        t = 0f;

        while (t < halfTime)
        {
            t += Time.deltaTime;

            float normalized = Mathf.Clamp01(t / halfTime);
            normalized = Smooth(normalized);

            characterVisual.localRotation =
                Quaternion.Slerp(
                    leanRotation,
                    startRotation,
                    normalized
                );

            yield return null;
        }

        characterVisual.localRotation = startRotation;
        currentAnimation = null;
    }

    private float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}