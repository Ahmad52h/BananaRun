using UnityEngine;

/// <summary>Two outward-swinging leaves; decoration never becomes a runner obstacle.</summary>
public sealed class WoodenStartingGate : MonoBehaviour
{
    [SerializeField] private Transform leftHinge;
    [SerializeField] private Transform rightHinge;
    [SerializeField, Min(0.1f)] private float openSeconds = 0.28f;
    private float opening;
    private bool isOpen;
    public float OpenProgress => opening;

    public void ResetClosed()
    {
        isOpen = false;
        opening = 0f;
        ApplyPose();
    }

    public void Open() => isOpen = true;
    private void Update()
    {
        if (!isOpen || opening >= 1f) return;
        opening = Mathf.MoveTowards(opening, 1f, Time.deltaTime / openSeconds);
        ApplyPose();
    }

    private void ApplyPose()
    {
        float angle = 105f * (1f - Mathf.Pow(1f - opening, 3f));
        if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0f, -angle, 0f);
        if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    public void Build(Material wood, Material trim, float halfWidth)
    {
        if (leftHinge != null) return;
        for (int side = -1; side <= 1; side += 2)
        {
            Piece("Timber post", transform, new Vector3(side * halfWidth, 1.15f, 0f), new Vector3(0.28f, 2.5f, 0.3f), wood);
            Piece("Post cap", transform, new Vector3(side * halfWidth, 2.42f, 0f), new Vector3(0.4f, 0.12f, 0.4f), trim);
            var hinge = new GameObject(side < 0 ? "Left hinge" : "Right hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(side * halfWidth, 0f, 0f);
            if (side < 0) leftHinge = hinge; else rightHinge = hinge;
            for (int rail = 0; rail < 2; rail++)
                Piece("Gate rail", hinge, new Vector3(-side * halfWidth * 0.5f, 0.65f + rail * 0.5f, 0f), new Vector3(halfWidth - 0.08f, 0.25f, 0.18f), wood);
            var brace = Piece("Diagonal brace", hinge, new Vector3(-side * halfWidth * 0.5f, 0.9f, -0.12f), new Vector3(halfWidth - 0.3f, 0.12f, 0.12f), trim);
            brace.localRotation = Quaternion.Euler(0f, 0f, -side * 12f);
        }
        Piece("Starting line", transform, new Vector3(0f, 0.23f, -0.25f), new Vector3(halfWidth * 2f, 0.05f, 0.35f), trim);
    }

    private static Transform Piece(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        var collider = obj.GetComponent<Collider>();
        collider.enabled = false;
        if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        return obj.transform;
    }
}
