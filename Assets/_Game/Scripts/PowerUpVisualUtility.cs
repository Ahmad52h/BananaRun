using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small runtime helpers shared by pickup and ability presentation.</summary>
public static class PowerUpVisualUtility
{
    public static Color ColorFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Bulldozer: return new Color(1f, 0.72f, 0.12f);
            case PowerUpType.Glider: return new Color(0.35f, 0.95f, 0.45f);
            case PowerUpType.Magnet: return new Color(0.72f, 0.38f, 1f);
            case PowerUpType.Shield: return new Color(0.72f, 0.82f, 0.76f);
            case PowerUpType.Thunder: return new Color(1f, 0.86f, 0.18f);
            default: return Color.white;
        }
    }

    /// <summary>
    /// Creates a transparent unlit material. URP/Unlit is preferred; the
    /// sprite shader is a safe fallback in stripped builds.
    /// </summary>
    public static Material CreateGlowMaterial(Color color, bool additive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material;
        if (shader != null)
        {
            material = new Material(shader) { name = "Runtime Power-Up Glow" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            material = new Material(shader) { name = "Runtime Power-Up Glow" };
            material.color = color;
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        return material;
    }

    /// <summary>
    /// Creates a transparent, lightly reflective shell for the Shield. URP/Lit
    /// keeps the bubble neutral and readable under the jungle lighting, unlike
    /// an unlit colour overlay. Falls back to the standard glow material.
    /// </summary>
    public static Material CreateSoftShellMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return CreateGlowMaterial(color);

        var material = new Material(shader) { name = "Runtime Shield Shell" };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.82f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    /// <summary>Builds a flat annulus on the XZ plane for a pickup's ground glow.</summary>
    public static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments = 48)
    {
        segments = Mathf.Max(8, segments);
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x * innerRadius, 0f, z * innerRadius);
            vertices[i * 2 + 1] = new Vector3(x * outerRadius, 0f, z * outerRadius);

            int next = (i + 1) % segments;
            int triangle = i * 6;
            triangles[triangle] = i * 2;
            triangles[triangle + 1] = next * 2;
            triangles[triangle + 2] = i * 2 + 1;
            triangles[triangle + 3] = i * 2 + 1;
            triangles[triangle + 4] = next * 2;
            triangles[triangle + 5] = next * 2 + 1;
        }

        var mesh = new Mesh { name = "Pickup Aura Ring" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
