using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class SceneNavMeshConfigurator
{
    private const string DemoCityScenePath = "Assets/_External/PolygonApocalypse/Scenes/Demo_City_Universal_RenderPipeline.unity";
    private const string NavMeshSurfaceName = "Zombie_NavMeshSurface";
    private const string NavMeshDataFolder = "Assets/_Project/Art/Terrain/Generated/NavMesh";

    private static readonly string[] NavMeshObjectRoots =
    {
        "Demo",
        "Ground",
        "Buildings",
        "Terrain",
        "Trees",
        "Vehicles",
        "Vehicles_Wrecked",
        "Props",
        "Weapons",
        "DeadBodies",
        "Plane"
    };

    [MenuItem("Tools/Zombie Rogue/Configure Scene NavMesh Objects And Bake")]
    public static void ConfigureActiveSceneAndBake()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("Open a saved scene before configuring NavMesh objects.");
            return;
        }

        ConfigureSceneAndBake(scene.path);
    }

    public static void ConfigureDemoCityAndBake()
    {
        ConfigureSceneAndBake(DemoCityScenePath);
    }

    private static void ConfigureSceneAndBake(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
        {
            Debug.LogError($"Scene not found: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        int walkableArea = GetAreaIndex("Walkable", 0);
        NavMeshSetupStats stats = new();

        ConfigureRootSet(NavMeshObjectRoots, walkableArea, ref stats);

        NavMeshData bakedData = BuildNavMeshData(BuildMarkups(NavMeshObjectRoots, walkableArea));
        if (bakedData == null)
        {
            Debug.LogError($"NavMesh build returned no data for scene: {scenePath}");
            return;
        }

        string navMeshAssetPath = SaveNavMeshData(scenePath, bakedData);
        AttachNavMeshDataSource(navMeshAssetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            $"Configured NavMesh objects and baked {navMeshAssetPath}. " +
            $"Scene objects: {stats.sceneObjects}, prefab objects: {stats.prefabObjects}, missing roots: {stats.missingRoots}.");
    }

    private static void ConfigureRootSet(string[] rootNames, int navMeshArea, ref NavMeshSetupStats stats)
    {
        foreach (string rootName in rootNames)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                stats.missingRoots++;
                Debug.LogWarning($"NavMesh root not found in scene: {rootName}");
                continue;
            }

            ConfigureHierarchy(root.transform, navMeshArea, ref stats);
        }
    }

    private static void ConfigureHierarchy(Transform root, int navMeshArea, ref NavMeshSetupStats stats)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform transform in transforms)
        {
            GameObject sceneObject = transform.gameObject;

            if (transform != root && !ShouldConfigureObject(sceneObject))
                continue;

            ConfigureGameObjectForNavMesh(sceneObject, navMeshArea);
            stats.sceneObjects++;

            GameObject prefabObject = PrefabUtility.GetCorrespondingObjectFromSource(sceneObject);
            if (prefabObject == null || PrefabUtility.IsPartOfImmutablePrefab(prefabObject))
                continue;

            ConfigureGameObjectForNavMesh(prefabObject, navMeshArea);
            EditorUtility.SetDirty(prefabObject);
            stats.prefabObjects++;
        }
    }

    private static bool ShouldConfigureObject(GameObject gameObject)
    {
        return gameObject.GetComponent<Renderer>() != null
            || gameObject.GetComponent<Collider>() != null
            || gameObject.GetComponent<Terrain>() != null;
    }

    private static void ConfigureGameObjectForNavMesh(GameObject gameObject, int navMeshArea)
    {
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
        flags |= StaticEditorFlags.NavigationStatic;
        GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
        GameObjectUtility.SetNavMeshArea(gameObject, navMeshArea);
        EditorUtility.SetDirty(gameObject);
    }

    private static List<NavMeshBuildMarkup> BuildMarkups(string[] rootNames, int area)
    {
        List<NavMeshBuildMarkup> markups = new();
        AddRootMarkups(markups, rootNames, area);
        return markups;
    }

    private static void AddRootMarkups(List<NavMeshBuildMarkup> markups, string[] rootNames, int area)
    {
        foreach (string rootName in rootNames)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null)
                continue;

            markups.Add(new NavMeshBuildMarkup
            {
                root = root.transform,
                overrideArea = true,
                area = area
            });
        }
    }

    private static NavMeshData BuildNavMeshData(List<NavMeshBuildMarkup> markups)
    {
        List<NavMeshBuildSource> sources = new();

        NavMeshBuilder.CollectSources(
            null,
            ~0,
            NavMeshCollectGeometry.RenderMeshes,
            0,
            markups,
            sources);

        sources.RemoveAll(IsDynamicActorSource);

        if (sources.Count == 0)
            return null;

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        Bounds bounds = CalculateBounds(sources, Vector3.zero, 64f);
        return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
    }

    private static bool IsDynamicActorSource(NavMeshBuildSource source)
    {
        if (source.component is not Component component)
            return false;

        Transform sourceTransform = component.transform;
        return sourceTransform.GetComponentInParent<NavMeshAgent>() != null
            || sourceTransform.GetComponentInParent<NavMeshObstacle>() != null
            || sourceTransform.GetComponentInParent<CharacterController>() != null
            || sourceTransform.GetComponentInParent<Rigidbody>() != null;
    }

    private static Bounds CalculateBounds(List<NavMeshBuildSource> sources, Vector3 fallbackCenter, float fallbackRadius)
    {
        Bounds bounds = new(fallbackCenter, Vector3.one * Mathf.Max(1f, fallbackRadius));
        bool hasBounds = false;

        foreach (NavMeshBuildSource source in sources)
        {
            if (!TryGetSourceBounds(source, out Bounds sourceBounds))
                continue;

            if (!hasBounds)
            {
                bounds = sourceBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(sourceBounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(fallbackCenter, Vector3.one * Mathf.Max(64f, fallbackRadius));

        bounds.Expand(2f);
        return bounds;
    }

    private static bool TryGetSourceBounds(NavMeshBuildSource source, out Bounds bounds)
    {
        if (source.component is Collider collider)
        {
            bounds = collider.bounds;
            return true;
        }

        if (source.component is Terrain terrain && terrain.terrainData != null)
        {
            bounds = terrain.terrainData.bounds;
            bounds.center += terrain.transform.position;
            return true;
        }

        if (source.component is Renderer renderer)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private static string SaveNavMeshData(string scenePath, NavMeshData bakedData)
    {
        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets/_Project/Art/Terrain/Generated", "NavMesh");

        string navMeshAssetPath = $"{NavMeshDataFolder}/{Path.GetFileNameWithoutExtension(scenePath)}_ZombieNavMesh.asset";
        NavMeshData oldData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshAssetPath);

        if (oldData != null)
            AssetDatabase.DeleteAsset(navMeshAssetPath);

        AssetDatabase.CreateAsset(bakedData, navMeshAssetPath);
        AssetDatabase.SaveAssets();
        return navMeshAssetPath;
    }

    private static void AttachNavMeshDataSource(string navMeshAssetPath)
    {
        GameObject navMeshObject = GameObject.Find(NavMeshSurfaceName);
        if (navMeshObject == null)
            navMeshObject = new GameObject(NavMeshSurfaceName);

        ZombieNavMeshDataSource navMeshDataSource = navMeshObject.GetComponent<ZombieNavMeshDataSource>();
        if (navMeshDataSource == null)
            navMeshDataSource = navMeshObject.AddComponent<ZombieNavMeshDataSource>();

        navMeshDataSource.NavMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshAssetPath);
        EditorUtility.SetDirty(navMeshDataSource);
    }

    private static int GetAreaIndex(string areaName, int fallback)
    {
        int area = NavMesh.GetAreaFromName(areaName);
        return area >= 0 ? area : fallback;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private struct NavMeshSetupStats
    {
        public int sceneObjects;
        public int prefabObjects;
        public int missingRoots;
    }
}
