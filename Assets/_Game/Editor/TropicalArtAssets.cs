using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class TropicalArtSetup
{
    const string Art = "Assets/_Game/Art/Tropical/";
    const string Prefabs = "Assets/_Game/Prefabs/Tropical/";
    const string Materials = "Assets/_Game/Materials/Tropical/";
    static Material bark, cutWood, leaves, leafLight, white, gold, stone, rope;

    static Material Mat(string name, string hex)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        ColorUtility.TryParseHtmlString(hex, out var color);
        material.color = color;
        material.SetFloat("_Smoothness", 0.12f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = rotation;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> triangles)
    {
        string path = Art + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static void MeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static GameObject SavePrefab(GameObject root, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildPalm()
    {
        var root = new GameObject("Tropical Palm");
        var v = new List<Vector3>(); var t = new List<int>();
        const int rings = 9, sides = 8;
        for (int r = 0; r < rings; r++)
        {
            float h = r / (float)(rings - 1);
            var center = new Vector3(h * h * 0.95f, h * 6.8f, 0);
            float radius = Mathf.Lerp(0.37f, 0.16f, h);
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2 / sides;
                v.Add(center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                if (r == 0) continue;
                int p = r * sides + j, n = r * sides + (j + 1) % sides;
                t.AddRange(new[] { p - sides, p, n, p - sides, n, n - sides });
            }
        }
        MeshObject("Curved Palm Trunk", root.transform, SaveMesh("PalmTrunk", v, t), bark);
        v.Clear(); t.Clear();
        for (int leaf = 0; leaf < 9; leaf++)
        {
            float a = leaf * Mathf.PI * 2 / 9;
            Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            int first = v.Count;
            for (int step = 0; step <= 6; step++)
            {
                float u = step / 6f;
                Vector3 center = new Vector3(0.95f, 6.8f, 0) + direction * (u * 3.8f);
                center.y += Mathf.Sin(u * Mathf.PI) * 0.8f - u * u * 1.5f;
                float width = Mathf.Sin(u * Mathf.PI) * 0.68f;
                v.Add(center - side * width);
                v.Add(center + Vector3.up * width * 0.25f);
                v.Add(center + side * width);
                if (step == 0) continue;
                int p = first + step * 3;
                t.AddRange(new[] { p - 3, p, p + 1, p - 3, p + 1, p - 2, p - 2, p + 1, p + 2, p - 2, p + 2, p - 1 });
            }
        }
        leaves.SetFloat("_Cull", 0);
        MeshObject("Broad Palm Fronds", root.transform, SaveMesh("PalmFronds", v, t), leaves);
        return SavePrefab(root, "TropicalPalm");
    }

    static GameObject BuildLog()
    {
        var root = new GameObject("Fallen Log Visual");
        Primitive("Bark", root.transform, PrimitiveType.Cylinder, new Vector3(0, 0.4f, 0), new Vector3(0.8f, 1.075f, 0.8f), bark, new Vector3(0, 0, 90));
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Cut End", root.transform, PrimitiveType.Cylinder, new Vector3(side * 1.08f, 0.4f, 0), new Vector3(0.68f, 0.013f, 0.68f), cutWood, new Vector3(0, 0, 90));
            Primitive("Growth Ring", root.transform, PrimitiveType.Cylinder, new Vector3(side * 1.096f, 0.4f, 0), new Vector3(0.44f, 0.009f, 0.44f), bark, new Vector3(0, 0, 90));
            Primitive("Heartwood", root.transform, PrimitiveType.Cylinder, new Vector3(side * 1.108f, 0.4f, 0), new Vector3(0.35f, 0.009f, 0.35f), cutWood, new Vector3(0, 0, 90));
        }
        Primitive("Broken Branch", root.transform, PrimitiveType.Cylinder, new Vector3(0.45f, 0.72f, 0.08f), new Vector3(0.18f, 0.17f, 0.18f), bark, new Vector3(15, 0, -25));
        return SavePrefab(root, "FallenLogVisual");
    }

    static GameObject BuildFlowers()
    {
        var root = new GameObject("White Daisy Patch");
        for (int flower = 0; flower < 3; flower++)
        {
            var p = new Vector3((flower - 1) * 0.28f, 0.25f + flower * 0.055f, (flower % 2) * 0.22f);
            Primitive("Stem", root.transform, PrimitiveType.Cylinder, new Vector3(p.x, p.y / 2, p.z), new Vector3(0.025f, p.y / 2, 0.025f), leaves);
            for (int petal = 0; petal < 6; petal++)
            {
                float angle = petal * Mathf.PI / 3;
                Primitive("White Petal", root.transform, PrimitiveType.Sphere, p + new Vector3(Mathf.Cos(angle) * 0.1f, 0, Mathf.Sin(angle) * 0.1f), new Vector3(0.17f, 0.045f, 0.1f), white, new Vector3(0, -petal * 60, 0));
            }
            Primitive("Golden Center", root.transform, PrimitiveType.Sphere, p + Vector3.up * 0.025f, new Vector3(0.11f, 0.06f, 0.11f), gold);
        }
        CombineByMaterial(root);
        return SavePrefab(root, "DaisyPatch");
    }

    static void CombineByMaterial(GameObject root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>();
        foreach (var group in filters.GroupBy(f => f.GetComponent<MeshRenderer>().sharedMaterial))
        {
            var mesh = new Mesh { name = (root.name + group.Key.name).Replace(" ", "") };
            mesh.CombineMeshes(group.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray());
            string path = Art + mesh.name.Replace(" ", "") + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            MeshObject(group.Key.name, root.transform, mesh, group.Key);
        }
        foreach (var filter in filters) Object.DestroyImmediate(filter.gameObject);
    }

    static GameObject BuildBorder(int variant, GameObject palm, GameObject flowers)
    {
        var root = new GameObject("Tropical Border " + variant);
        var random = new System.Random(810 + variant);
        for (int side = -1; side <= 1; side += 2)
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(palm, root.transform);
            tree.transform.localPosition = new Vector3(side * (9.2f + variant), 0, 5 + (side + 1) * 5);
            tree.transform.localEulerAngles = new Vector3(0, variant * 72 + side * 35, 0);
            tree.transform.localScale = Vector3.one * (0.9f + variant * 0.1f);
            for (int i = 0; i < 4; i++)
            {
                float z = i * 6 + (float)random.NextDouble() * 2;
                var flower = (GameObject)PrefabUtility.InstantiatePrefab(flowers, root.transform);
                flower.transform.localPosition = new Vector3(side * (5.65f + (float)random.NextDouble() * 0.6f), 0, z);
                var grass = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Plants/PT_Grass_02.prefab"), root.transform);
                grass.transform.localPosition = new Vector3(side * 6.2f, 0, z + 2);
                grass.transform.localScale *= 0.7f;
                Primitive("River Pebble", root.transform, PrimitiveType.Sphere, new Vector3(side * (5.2f + (float)random.NextDouble() * 0.4f), 0.08f, z + 3), new Vector3(0.42f, 0.25f, 0.58f), stone);
            }
            if ((variant + side + 1) % 3 != 1)
            {
                var fence = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/Fence/PT_Modular_Fence_Wood_01.prefab"), root.transform);
                fence.transform.localEulerAngles = new Vector3(0, 90, 0);
                Fit(fence, new Vector3(0.25f, 0.85f, 5), new Vector3(side * 6.5f, 0, 14));
            }
        }
        foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        return SavePrefab(root, "Border" + variant);
    }

    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static void Fit(GameObject go, Vector3 size, Vector3 bottom)
    {
        var bounds = BoundsOf(go);
        // World-axis fitting for the cardinal rotations used by border fences and props.
        var localSize = go.transform.InverseTransformVector(size);
        var localBounds = go.transform.InverseTransformVector(bounds.size);
        go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(Mathf.Abs(localSize.x / localBounds.x), Mathf.Abs(localSize.y / localBounds.y), Mathf.Abs(localSize.z / localBounds.z)));
        bounds = BoundsOf(go);
        go.transform.position += new Vector3(bottom.x - bounds.center.x, bottom.y - bounds.min.y, bottom.z - bounds.center.z);
    }
}
