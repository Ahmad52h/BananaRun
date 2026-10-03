using UnityEngine;

/// <summary>Independent lifetime so a crushed obstacle can disappear immediately.</summary>
public class GiantImpactEffect : MonoBehaviour
{
    [SerializeField] private AudioClip smash;
    private void Start()
    {
        if (smash != null)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.PlayOneShot(smash, 1f);
        }
        foreach (var particles in GetComponentsInChildren<ParticleSystem>()) particles.Play();
        Destroy(gameObject, Mathf.Max(3f, smash != null ? smash.length + 0.1f : 0f));
    }
}
