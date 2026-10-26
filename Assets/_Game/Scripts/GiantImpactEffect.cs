using UnityEngine;

/// <summary>Independent lifetime so a crushed obstacle can disappear immediately.</summary>
public class GiantImpactEffect : MonoBehaviour
{
    [SerializeField] private AudioClip smash;
    private static float lastSmashAt = float.NegativeInfinity;
    private void Start()
    {
        if (smash != null && Time.realtimeSinceStartup - lastSmashAt >= 0.15f)
        {
            lastSmashAt = Time.realtimeSinceStartup;
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.4f;
            source.PlayOneShot(smash);
        }
        foreach (var particles in GetComponentsInChildren<ParticleSystem>()) particles.Play();
        Destroy(gameObject, Mathf.Max(3f, smash != null ? smash.length + 0.1f : 0f));
    }
}
