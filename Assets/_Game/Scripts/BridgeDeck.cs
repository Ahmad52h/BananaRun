using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A continuous walking surface sampled from the visible planks. Movement,
/// pickups and water hazards all use this same world-space deck profile.
/// </summary>
public sealed class BridgeDeck : MonoBehaviour
{
    private static readonly List<BridgeDeck> decks = new List<BridgeDeck>();
    private Vector3[] profile;
    private float centerX, halfWidth;
    private Mesh surfaceMesh;

    private void OnEnable() => decks.Add(this);
    private void OnDisable() => decks.Remove(this);

    public void Configure(Vector3[] points, float width)
    {
        profile = points;
        centerX = points[0].x;
        halfWidth = width * 0.5f;

        // One sloping ribbon avoids the vertical edges of overlapping boxes.
        var vertices = new Vector3[points.Length * 2];
        var triangles = new int[(points.Length - 1) * 6];
        for (int i = 0; i < points.Length; i++)
        {
            vertices[i * 2] = transform.InverseTransformPoint(points[i] + Vector3.left * halfWidth);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(points[i] + Vector3.right * halfWidth);
            if (i == points.Length - 1) continue;
            int t = i * 6, v = i * 2;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
        }
        surfaceMesh = new Mesh { name = "Continuous Bridge Deck" };
        surfaceMesh.vertices = vertices;
        surfaceMesh.triangles = triangles;
        surfaceMesh.RecalculateBounds();
        surfaceMesh.RecalculateNormals();
        gameObject.AddComponent<MeshCollider>().sharedMesh = surfaceMesh;
    }

    public static bool TryGetHeight(Vector3 position, out float height)
    {
        foreach (var deck in decks)
        {
            var points = deck.profile;
            if (points == null || Mathf.Abs(position.x - deck.centerX) > deck.halfWidth ||
                position.z < points[0].z || position.z > points[points.Length - 1].z) continue;
            for (int i = 1; i < points.Length; i++)
            {
                if (position.z > points[i].z) continue;
                float t = Mathf.InverseLerp(points[i - 1].z, points[i].z, position.z);
                height = Mathf.Lerp(points[i - 1].y, points[i].y, t);
                return true;
            }
        }
        height = 0f;
        return false;
    }

    /// <summary>Follow slopes while walking or landing; preserve airborne jump arcs.</summary>
    public static void FollowSurface(CharacterController controller, ref Vector3 movement, float verticalVelocity)
    {
        if (verticalVelocity > 0f) return;
        Vector3 position = controller.transform.position;
        if (!TryGetHeight(position + movement, out float nextHeight)) return;
        float currentHeight = TryGetHeight(position, out float height) ? height : nextHeight;
        float feet = controller.transform.TransformPoint(controller.center).y -
            controller.height * Mathf.Abs(controller.transform.lossyScale.y) * 0.5f;
        // Never pull a runner up from underneath the bridge or down from a jump.
        if (feet < currentHeight - 0.08f || feet > currentHeight + controller.skinWidth + 0.12f) return;
        // Press into the surface far enough to retain Below contact at plank
        // seams. The controller's skin keeps the capsule above the mesh.
        movement.y = nextHeight - feet - Mathf.Max(controller.skinWidth, 0.02f);
    }

    private void OnDestroy()
    {
        if (surfaceMesh != null) Destroy(surfaceMesh);
    }
}
