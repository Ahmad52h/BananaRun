using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spawns lightweight, self-contained banana meshes for the victory celebration.
/// The burst owns its mesh and material, so it needs no prefab or scene edits.
/// </summary>
[DisallowMultipleComponent]
public sealed class CelebrationBananaBurst : MonoBehaviour
{
    private sealed class BananaBit
    {
        public Transform Transform;
        public Vector3 Velocity;
        public Vector3 Spin;
        public Vector3 Scale;
        public float Age;
        public float Life;
    }

    private const float Gravity = 11.5f;
    private const float MinimumScale = 0.02f;
    private const float BoundsEpsilon = 1e-5f;
    private static readonly Color BananaYellow = new Color(1f, 0.83f, 0.21f);

    private readonly List<BananaBit> bits = new List<BananaBit>();
    private Mesh bananaMesh;
    private Material bananaMaterial;

    /// <summary>Fires a fan of celebratory bananas from a world position.</summary>
    public void Emit(Vector3 origin, int count, float power = 1f, float radius = 0.35f)
    {
        if (count <= 0) return;

        // Never seed a burst with non-finite data; a single bad origin or scale
        // can poison every renderer AABB for the rest of the race.
        if (!IsFinite(origin))
            origin = IsFinite(transform.position) ? transform.position : Vector3.zero;
        if (!IsFinite(power)) power = 1f;
        if (!IsFinite(radius) || radius < 0f) radius = 0.35f;
        power = Mathf.Max(0.1f, power);

        EnsureAssets();
        if (bananaMesh == null || bananaMaterial == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-radius, radius),
                Random.Range(0f, radius * 0.7f),
                Random.Range(-radius, radius));
            if (!IsFinite(offset)) offset = Vector3.zero;

            var bitObject = new GameObject("Celebration Banana");
            bitObject.transform.SetParent(transform, true);
            bitObject.transform.position = origin + offset;
            bitObject.transform.rotation = Random.rotation;

            var filter = bitObject.AddComponent<MeshFilter>();
            filter.sharedMesh = bananaMesh;
            var renderer = bitObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = bananaMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Vector3 direction = new Vector3(
                Random.Range(-0.9f, 0.9f),
                Random.Range(0.75f, 1.5f),
                Random.Range(-0.55f, 0.75f)).normalized;
            if (!IsFinite(direction) || direction.sqrMagnitude < 0.01f)
                direction = Vector3.up;

            float scale = Random.Range(0.55f, 0.85f);
            if (!IsFinite(scale) || scale < MinimumScale) scale = 1f;

            bitObject.transform.localScale = Vector3.one * scale;
            EnsureValidScale(bitObject.transform);

            bits.Add(new BananaBit
            {
                Transform = bitObject.transform,
                Velocity = direction * Random.Range(3.4f, 6.4f) * power,
                Spin = Random.insideUnitSphere * 520f,
                Scale = Vector3.one * scale,
                Age = 0f,
                Life = Random.Range(1.25f, 1.9f)
            });
        }
    }

    private void Update()
    {
        if (bits.Count == 0) return;

        float dt = Time.deltaTime;
        if (!IsFinite(dt) || dt <= 0f) return;

        for (int i = bits.Count - 1; i >= 0; i--)
        {
            BananaBit bit = bits[i];
            if (bit.Transform == null)
            {
                bits.RemoveAt(i);
                continue;
            }

            float life = Mathf.Max(0.01f, bit.Life);
            bit.Age += dt;
            if (bit.Age >= life)
            {
                Destroy(bit.Transform.gameObject);
                bits.RemoveAt(i);
                continue;
            }

            bit.Velocity += Vector3.down * (Gravity * dt);
            if (!IsFinite(bit.Velocity))
            {
                Destroy(bit.Transform.gameObject);
                bits.RemoveAt(i);
                continue;
            }

            Vector3 nextPosition = bit.Transform.position + bit.Velocity * dt;
            if (!IsFinite(nextPosition))
            {
                Destroy(bit.Transform.gameObject);
                bits.RemoveAt(i);
                continue;
            }
            bit.Transform.position = nextPosition;

            if (IsFinite(bit.Spin))
                bit.Transform.Rotate(bit.Spin * dt, Space.Self);

            // Keep the renderer scale above zero and finite for the whole life
            // of the bit; zero/NaN scales produce degenerate renderer AABBs.
            float remaining = Mathf.Max(
                MinimumScale,
                Mathf.InverseLerp(1f, 0.7f, bit.Age / life));
            Vector3 nextScale = bit.Scale * remaining;
            nextScale = new Vector3(
                Mathf.Max(MinimumScale, nextScale.x),
                Mathf.Max(MinimumScale, nextScale.y),
                Mathf.Max(MinimumScale, nextScale.z));
            if (!IsUsableScale(nextScale))
                nextScale = Vector3.one * MinimumScale;

            bit.Transform.localScale = nextScale;
            EnsureValidScale(bit.Transform);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < bits.Count; i++)
        {
            Transform bitTransform = bits[i].Transform;
            if (bitTransform == null) continue;

            // Clear shared references before destroying the owned mesh and
            // material so no live renderer can hold a destroyed asset.
            MeshFilter filter = bitTransform.GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = null;

            MeshRenderer renderer = bitTransform.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = null;

            Destroy(bitTransform.gameObject);
        }
        bits.Clear();

        if (bananaMaterial != null)
        {
            Destroy(bananaMaterial);
            bananaMaterial = null;
        }
        if (bananaMesh != null)
        {
            Destroy(bananaMesh);
            bananaMesh = null;
        }
    }

    private void EnsureAssets()
    {
        if (bananaMesh == null) bananaMesh = BuildBananaMesh();
        if (!HasValidBounds(bananaMesh))
        {
            Debug.LogWarning(
                "CelebrationBananaBurst produced invalid mesh bounds; using a fallback mesh.",
                this);
            if (bananaMesh == null)
                bananaMesh = new Mesh { name = "Celebration Banana" };
            BuildFallbackMesh(bananaMesh);
        }
        if (bananaMaterial != null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("CelebrationBananaBurst could not find a usable shader.", this);
            return;
        }

        bananaMaterial = new Material(shader) { name = "Celebration Banana Burst" };
        if (bananaMaterial.HasProperty("_BaseColor")) bananaMaterial.SetColor("_BaseColor", BananaYellow);
        if (bananaMaterial.HasProperty("_Color")) bananaMaterial.SetColor("_Color", BananaYellow);
        if (bananaMaterial.HasProperty("_Metallic")) bananaMaterial.SetFloat("_Metallic", 0f);
        if (bananaMaterial.HasProperty("_Smoothness")) bananaMaterial.SetFloat("_Smoothness", 0.32f);
        if (bananaMaterial.HasProperty("_EmissionColor"))
        {
            bananaMaterial.EnableKeyword("_EMISSION");
            bananaMaterial.SetColor("_EmissionColor", BananaYellow * 0.22f);
        }
        // The tube is open at both tapered ends; disabling culling keeps it visible
        // regardless of the procedural winding.
        if (bananaMaterial.HasProperty("_Cull")) bananaMaterial.SetFloat("_Cull", 0f);
        bananaMaterial.enableInstancing = true;
    }

    private Mesh BuildBananaMesh()
    {
        const int rings = 10;
        var vertices = new List<Vector3>((rings + 1) * 4);
        var triangles = new List<int>(rings * 24);

        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            Vector3 center = BananaCurve(t);
            const float epsilon = 0.01f;

            Vector3 tangent = BananaCurve(Mathf.Min(1f, t + epsilon)) -
                BananaCurve(Mathf.Max(0f, t - epsilon));
            if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.up;
            else tangent.Normalize();

            Vector3 up = Vector3.Cross(tangent, Vector3.forward);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(tangent, Vector3.right);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.right;
            else up.Normalize();

            Vector3 side = Vector3.Cross(up, tangent);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.forward;
            else side.Normalize();

            // Mathf.Sin(Mathf.PI) is a tiny negative value in float math.
            // Passing that to Mathf.Pow with a fractional exponent returns NaN,
            // which previously poisoned the final ring's vertices and bounds.
            float swell = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
            float radius = 0.1f * Mathf.Pow(swell, 0.55f) + 0.012f;
            if (!IsFinite(radius) || radius < 1e-4f) radius = 0.012f;

            AddRingVertex(vertices, center + up * radius + side * radius, center);
            AddRingVertex(vertices, center + up * radius - side * radius, center);
            AddRingVertex(vertices, center - up * radius - side * radius, center);
            AddRingVertex(vertices, center - up * radius + side * radius, center);
        }

        for (int i = 0; i < rings; i++)
        {
            int near = i * 4;
            int far = (i + 1) * 4;
            for (int side = 0; side < 4; side++)
            {
                int next = (side + 1) % 4;
                triangles.Add(near + side);
                triangles.Add(far + side);
                triangles.Add(far + next);
                triangles.Add(near + side);
                triangles.Add(far + next);
                triangles.Add(near + next);
            }
        }

        var mesh = new Mesh { name = "Celebration Banana" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (!HasValidBounds(mesh))
        {
            Debug.LogWarning(
                "CelebrationBananaBurst generated invalid banana mesh bounds; using a fallback mesh.",
                this);
            BuildFallbackMesh(mesh);
        }

        return mesh;
    }

    private static void AddRingVertex(List<Vector3> vertices, Vector3 value, Vector3 fallback)
    {
        vertices.Add(IsFinite(value) ? value : fallback);
    }

    private static void BuildFallbackMesh(Mesh mesh)
    {
        const float minX = -0.42f;
        const float maxX = 0.42f;
        const float minY = -0.02f;
        const float maxY = 0.24f;
        const float minZ = -0.06f;
        const float maxZ = 0.06f;

        mesh.Clear();
        mesh.SetVertices(new List<Vector3>
        {
            new Vector3(minX, minY, minZ),
            new Vector3(maxX, minY, minZ),
            new Vector3(maxX, maxY, minZ),
            new Vector3(minX, maxY, minZ),
            new Vector3(minX, minY, maxZ),
            new Vector3(maxX, minY, maxZ),
            new Vector3(maxX, maxY, maxZ),
            new Vector3(minX, maxY, maxZ)
        });
        mesh.SetTriangles(new List<int>
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            1, 5, 6, 1, 6, 2,
            2, 6, 7, 2, 7, 3,
            3, 7, 4, 3, 4, 0
        }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private static void EnsureValidScale(Transform target)
    {
        if (target == null) return;

        Vector3 scale = target.localScale;
        if (IsUsableScale(scale)) return;

        target.localScale = Vector3.one;
    }

    private static bool IsUsableScale(Vector3 scale)
    {
        return IsFinite(scale) &&
            Mathf.Abs(scale.x) >= MinimumScale &&
            Mathf.Abs(scale.y) >= MinimumScale &&
            Mathf.Abs(scale.z) >= MinimumScale;
    }

    private static bool HasValidBounds(Mesh mesh)
    {
        if (mesh == null) return false;

        Bounds bounds = mesh.bounds;
        if (!IsFinite(bounds.center) || !IsFinite(bounds.size)) return false;

        return bounds.size.x > BoundsEpsilon &&
            bounds.size.y > BoundsEpsilon &&
            bounds.size.z > BoundsEpsilon;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static Vector3 BananaCurve(float t)
    {
        float angle = Mathf.Lerp(-1.02f, 1.02f, t);
        const float radius = 0.5f;
        // A crescent that opens upward, centred on the origin.
        return new Vector3(Mathf.Sin(angle) * radius, (Mathf.Cos(angle) - 1f) * radius + 0.23f, 0f);
    }
}
