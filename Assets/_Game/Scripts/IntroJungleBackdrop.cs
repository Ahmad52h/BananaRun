using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only dense jungle behind the starting line. The opening countdown
/// camera looks backwards across negative Z, so this fills both banks, arches
/// the road with canopy and closes the horizon with a forest wall. Every prop
/// is registered with <see cref="TrackDistanceStreamer"/>, which keeps the set
/// alive through the intro and retires it once both runners leave the clearing.
/// </summary>
[DefaultExecutionOrder(-30)]
public sealed class IntroJungleBackdrop : MonoBehaviour
{
    private const string SceneName = "Level01";
    private const string RootName = "Intro Jungle Backdrop (Runtime)";
    private const float BankStartZ = -84f;
    private const float BankEndZ = 18f;
    private const float WallStartZ = -48f;
    private const float WallEndZ = -86f;
    private const float ArchStartZ = -46f;
    private const float ArchEndZ = -84f;

    private Transform root;
    private GameObject[] treePrefabs;
    private GameObject shrubPrefab;
    private System.Random random;
    private float groundY = -0.3f;
    private int spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName) return;
        foreach (GameObject existing in scene.GetRootGameObjects())
            if (existing.GetComponent<IntroJungleBackdrop>() != null) return;
        GameObject host = new GameObject(RootName + " Host");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<IntroJungleBackdrop>();
    }

    private void Start() => Build();

    private void Build()
    {
        RunnerCourseGenerator generator = FindAnyObjectByType<RunnerCourseGenerator>();
        if (generator == null)
        {
            Debug.LogWarning("IntroJungleBackdrop needs RunnerCourseGenerator scenery prefabs; no generator found.", this);
            return;
        }

        treePrefabs = generator.SceneryPrefabs;
        shrubPrefab = generator.RoadsideShrubPrefab;
        groundY = generator.GroundHeight;
        random = new System.Random(9137);
        root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        BuildGroundShelf();
        BuildBanks();
        BuildForestWall();
        BuildCanopyArches();
        BuildBackdropCurtain();

        if (spawned == 0)
            Debug.LogWarning("IntroJungleBackdrop produced no scenery; check the course generator's scenery prefabs.", this);
        else
            Debug.Log($"IntroJungleBackdrop lined Z {BankStartZ:F0}..{BankEndZ:F0} with {spawned} runtime props and closed the clearing at Z {WallEndZ:F0}.", this);
    }

    private void BuildBanks()
    {
        if ((treePrefabs == null || treePrefabs.Length == 0) && shrubPrefab == null) return;

        // Three overlapping depth layers per bank plus a near shrub line. The
        // random jitter keeps the rows from reading as a grid from the intro camera.
        for (float z = BankStartZ; z <= BankEndZ; z += 5.5f)
        {
            int row = Mathf.RoundToInt((z - BankStartZ) / 5.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                SpawnTree(side * (6.4f + Range(0f, 2.4f)), z + Range(-1.2f, 1.2f), Range(0.95f, 1.4f));
                SpawnTree(side * (11.5f + Range(0f, 4.8f)), z + 1.9f + Range(-1.2f, 1.2f), Range(1.05f, 1.65f));
                if ((row & 1) == 0)
                    SpawnTree(side * (18f + Range(0f, 7.5f)), z + 3.9f + Range(-1.4f, 1.4f), Range(1.15f, 1.95f));
                if (shrubPrefab != null)
                    Spawn(shrubPrefab, side * (6f + Range(0f, 1.4f)), z + 1.1f + Range(-0.5f, 0.5f),
                        Range(0.8f, 1.3f));
            }
        }
    }

    private void BuildForestWall()
    {
        if (treePrefabs == null || treePrefabs.Length == 0) return;

        // Staggered tree rows close the whole clearing exit behind the gorilla.
        // The road is never revisited after launch, so the wall can cross it.
        int row = 0;
        for (float z = WallStartZ; z >= WallEndZ; z -= 4.8f, row++)
        {
            float stagger = (row & 1) == 0 ? 0f : 2.7f;
            for (float x = -36f + stagger; x <= 36f; x += 5.4f)
            {
                SpawnTree(x + Range(-1.4f, 1.4f), z + Range(-1.1f, 1.1f), Range(1.45f, 2.6f));
            }
        }

        if (shrubPrefab == null) return;
        for (float z = WallStartZ + 2f; z >= WallEndZ; z -= 7f)
        {
            float stagger = ((int)((WallStartZ - z) / 7f) & 1) == 0 ? 0f : 2f;
            for (float x = -35f + stagger; x <= 35f; x += 4.2f)
            {
                Spawn(shrubPrefab, x + Range(-1.3f, 1.3f), z + Range(-1f, 1f), Range(1.3f, 2.4f));
            }
        }
    }

    private void BuildCanopyArches()
    {
        if (treePrefabs == null || treePrefabs.Length == 0) return;

        // Oversized alternating crowns lean over the road, closing the canopy
        // between the bushes and the distant wall.
        int index = 0;
        for (float z = ArchStartZ; z >= ArchEndZ; z -= 4.2f, index++)
        {
            int side = (index & 1) == 0 ? -1 : 1;
            SpawnTree(side * Range(3.9f, 5.1f), z, Range(2.4f, 3.3f));
            SpawnTree(-side * Range(5.4f, 7.4f), z - 2.1f, Range(1.8f, 2.5f));
        }
    }

    private void BuildGroundShelf()
    {
        // The authored ground ends just before the horizon wall. A low shelf
        // keeps the distant trees from standing over empty sky.
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Intro Foliage Ground Shelf";
        shelf.transform.SetParent(root, false);
        shelf.transform.position = new Vector3(0f, groundY - 0.51f, -87.5f);
        shelf.transform.localScale = new Vector3(220f, 1f, 17f);
        DisableCollider(shelf);

        MeshRenderer renderer = shelf.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = ResolveGroundMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        TrackDistanceStreamer.Register(shelf);
    }

    private void BuildBackdropCurtain()
    {
        // A dark unlit curtain behind the last tree row guarantees no flat
        // horizon shows between trunks, even when fog density changes.
        GameObject curtain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        curtain.name = "Intro Foliage Horizon";
        curtain.transform.SetParent(root, false);
        curtain.transform.position = new Vector3(0f, groundY + 9f, -92f);
        curtain.transform.localScale = new Vector3(220f, 22f, 1.5f);
        DisableCollider(curtain);

        MeshRenderer renderer = curtain.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateMaterial("Intro Jungle Curtain (Runtime)",
                new Color(0.035f, 0.085f, 0.045f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        TrackDistanceStreamer.Register(curtain);
    }

    private void SpawnTree(float x, float z, float scale)
    {
        if (treePrefabs == null || treePrefabs.Length == 0) return;
        Spawn(treePrefabs[random.Next(treePrefabs.Length)], x, z, scale);
    }

    private void Spawn(GameObject prefab, float x, float z, float scale)
    {
        if (prefab == null) return;
        GameObject instance = Instantiate(prefab, new Vector3(x, groundY, z),
            Quaternion.Euler(0f, Range(0f, 360f), 0f), root);
        instance.name = prefab.name + " (Intro Backdrop)";
        instance.transform.localScale *= scale;

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        // If the streamer is not initialized yet its first scene scan will pick
        // this up; otherwise this registers the prop in the current window.
        TrackDistanceStreamer.Register(instance);
        spawned++;
    }

    private Material ResolveGroundMaterial()
    {
        string[] candidates = { "Ground", "Blended Dirt and Grass", "Ground Before River" };
        foreach (string candidate in candidates)
        {
            GameObject source = GameObject.Find(candidate);
            if (source == null) continue;
            MeshRenderer renderer = source.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null) return renderer.sharedMaterial;
        }
        return CreateMaterial("Intro Jungle Ground (Runtime)", new Color(0.1f, 0.16f, 0.07f));
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        // URP Lit is guaranteed to survive shader stripping because authored
        // materials already use it; Unlit is only a fallback for editor parity.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;

        var material = new Material(shader) { name = materialName, enableInstancing = true };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return RuntimeAssetOwner.Own(material, this);
    }

    private static void DisableCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider == null) return;
        collider.enabled = false;
        Destroy(collider);
    }

    private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
}
