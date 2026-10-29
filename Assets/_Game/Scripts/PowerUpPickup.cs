using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class PowerUpPickup : MonoBehaviour
{
    [SerializeField] private PowerUpType type;
    [SerializeField, Min(0f)] private float spinSpeed = 120f;
    [SerializeField] private ParticleSystem collectionEffect;
    [Header("Hover presentation")]
    [SerializeField, Min(0f)] private float hoverHeight = 1.45f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.15f;
    [SerializeField, Min(0f)] private float bobSpeed = 1.8f;

    private const string BtmResourceFolder = "PowerUps/";
    private bool collected;
    private Transform visualPivot;
    private float bobPhase;

    public PowerUpType Type => type;
    public ParticleSystem CollectionEffect => collectionEffect;
    public float HoverHeight => hoverHeight;

    /// <summary>
    /// Builds an inactive pickup template for runtime-only abilities whose
    /// prefab is not serialized in the scene. The scene's existing pickup
    /// array remains untouched and receives these templates at runtime.
    /// </summary>
    public static PowerUpPickup CreateTemplate(PowerUpType type, ParticleSystem effect = null)
    {
        var root = new GameObject(type + " Pickup Template");
        root.SetActive(false);
        root.transform.position = new Vector3(0f, -1000f, 0f);

        var sphere = root.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 1f;
        sphere.center = new Vector3(0f, 1.45f, 0f);

        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var pickup = root.AddComponent<PowerUpPickup>();
        pickup.type = type;
        pickup.collectionEffect = effect;
        pickup.spinSpeed = 120f;
        return pickup;
    }

    private void Awake()
    {
        var sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        bobPhase = Random.value * Mathf.PI * 2f;
        AlignToGround();
        sphere.center = new Vector3(0f, hoverHeight, 0f);
        sphere.radius = 1f;

        var originalChildren = new List<Transform>();
        for (int i = transform.childCount - 1; i >= 0; i--)
            originalChildren.Add(transform.GetChild(i));

        visualPivot = new GameObject("Pickup Visual Pivot").transform;
        visualPivot.SetParent(transform, false);
        visualPivot.localPosition = new Vector3(0f, hoverHeight, 0f);
        foreach (var child in originalChildren)
            child.SetParent(visualPivot, false);

        if (UsesBtmVisual(type))
            ReplaceWithBtmVisual();
        else if (visualPivot.childCount == 0)
            BuildFallbackVisual();
    }

    private void AlignToGround()
    {
        Vector3 spawn = transform.position;
        float groundY = spawn.y - 1.15f;
        if (Physics.Raycast(new Vector3(spawn.x, spawn.y + 1.5f, spawn.z), Vector3.down,
                out RaycastHit hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
        }
        transform.position = new Vector3(spawn.x, groundY, spawn.z);
    }

    private static bool UsesBtmVisual(PowerUpType type) =>
        type == PowerUpType.Magnet || type == PowerUpType.Shield || type == PowerUpType.Thunder;

    private void ReplaceWithBtmVisual()
    {
        string resourceName = BtmResourceFolder + type;
        var prefab = Resources.Load<GameObject>(resourceName);
        if (prefab == null)
        {
            Debug.LogWarning($"Could not load BTM {type} pickup visual at Resources/{resourceName}. " +
                             "Keeping the existing visual or using the generated fallback.", this);
            if (visualPivot.childCount == 0) BuildFallbackVisual();
            return;
        }

        ClearVisualChildren();
        var visual = Instantiate(prefab, visualPivot, false);
        visual.name = type + " BTM Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        StripImportedComponents(visual);
        NormalizeVisual(visual);
    }

    private void ClearVisualChildren()
    {
        for (int i = visualPivot.childCount - 1; i >= 0; i--)
        {
            var child = visualPivot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static void StripImportedComponents(GameObject visual)
    {
        // The authored pack animates and scales its models itself. This project
        // owns rotation/bobbing, so remove the conflicting behaviour and any
        // imported physics hooks.
        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            behaviour.enabled = false;
            Destroy(behaviour);
        }
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
        foreach (var audio in visual.GetComponentsInChildren<AudioSource>(true))
        {
            audio.playOnAwake = false;
            audio.enabled = false;
            Destroy(audio);
        }
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private void NormalizeVisual(GameObject visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = CalculateBounds(renderers);
        float targetHeight = type == PowerUpType.Thunder ? 1.5f : 1.35f;
        float scale = targetHeight / Mathf.Max(0.01f, bounds.size.y);
        visual.transform.localScale *= scale;

        bounds = CalculateBounds(renderers);
        Vector3 localCenter = visualPivot.InverseTransformPoint(bounds.center);
        visual.transform.localPosition -= localCenter;
    }

    private static Bounds CalculateBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void BuildFallbackVisual()
    {
        var fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fallback.name = type + " Fallback Visual";
        var collider = fallback.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
        fallback.transform.SetParent(visualPivot, false);
        fallback.transform.localPosition = Vector3.zero;
        fallback.transform.localScale = Vector3.one * 0.95f;
        var renderer = fallback.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material = PowerUpVisualUtility.CreateGlowMaterial(
                new Color(0.88f, 0.86f, 0.78f, 0.9f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void Update()
    {
        if (collected || visualPivot == null) return;

        // Keep the requested smooth 120 deg/sec display spin independent of the
        // prefab's old serialized value.
        visualPivot.Rotate(Vector3.up, Mathf.Max(120f, spinSpeed) * Time.deltaTime, Space.Self);
        float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude;
        visualPivot.localPosition = new Vector3(0f, hoverHeight + bob, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var manager = other.GetComponentInParent<PowerUpManager>();
        bool player = manager != null && manager.CompareTag("Player");
        var rival = player ? null : other.GetComponentInParent<RivalRunner>();
        if (!player && rival == null) return;

        if (player)
        {
            var health = manager.GetComponent<PlayerHealth>();
            if (!manager.isActiveAndEnabled || (health != null && health.IsRunOver)) return;
            manager.Activate(type);
            // Pickup audio belongs to the Player only; the rival collects silently.
            RunnerAudio.Instance?.PlayPowerUpCollect();
        }
        else
        {
            if (!rival.CollectPowerUp(type)) return;
        }

        collected = true;
        PlayCollectionEffect(player);
        gameObject.SetActive(false);
    }

    private void PlayCollectionEffect(bool withAudio)
    {
        if (collectionEffect == null) return;
        var effect = Instantiate(collectionEffect,
            transform.position + Vector3.up * hoverHeight, Quaternion.identity);
        // The rival may collect pickups, but pickup audio belongs to the Player.
        if (!withAudio)
        {
            foreach (var source in effect.GetComponentsInChildren<AudioSource>(true))
            {
                source.mute = true;
                source.enabled = false;
            }
        }
        var main = effect.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
        effect.Play(true);
    }

    private void OnEnable() => collected = false;
}
