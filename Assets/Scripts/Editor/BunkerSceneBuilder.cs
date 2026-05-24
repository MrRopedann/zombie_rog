using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BunkerSceneBuilder
{
    private const string ScenePath = "Assets/_Scenes/Bunker.unity";
    private const string GeneratedRootName = "Generated_Bunker_Shelter";
    private const string AutoBuildRequestPath = "Library/CodexBuildBunker.request";
    private const string AutoBuildResultPath = "Library/CodexBuildBunker.result";
    private const float Tile = 4f;

    private const string PlayerPrefabPath = "Assets/Resources/Prefabs/Character/Player.prefab";
    private const string FloorPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Floor_Stone_x2_01.prefab";
    private const string ConcreteFloorPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Concrete_Floor_01.prefab";
    private const string WallPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Wall_Concrete_x2_01.prefab";
    private const string DoorWallPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Wall_Concrete_Door_01.prefab";
    private const string CeilingPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Ceiling_Concrete_01.prefab";
    private const string VentCeilingPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Ceiling_Vent_01.prefab";
    private const string PillarPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_PillarBlock_01.prefab";
    private const string StairsPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Stairs_Double_01.prefab";
    private const string HatchPrefabPath = "Assets/PolygonApocalypse/Prefabs/Buildings/SM_Bld_Bunker_Hatch_Round_01.prefab";
    private const string EntrancePrefabPath = "Assets/PolygonApocalypse/Prefabs/Environment/SM_Env_Bunker_Entrance_01.prefab";
    private const string LightPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Bunker_Light_01.prefab";
    private const string FluorescentLightPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Light_Flurecent_01.prefab";
    private const string WallPipePrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Bunker_WallPipe_01.prefab";
    private const string WallWiresPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Bunker_WallWires_01.prefab";
    private const string RoofWirePrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Bunker_RoofWire_01.prefab";
    private const string CratePrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Crate_03.prefab";
    private const string LargeCratePrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Crate_Large_01.prefab";
    private const string AmmoBoxPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Ammo_Box_01.prefab";
    private const string WorkShelfPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_WorkShelf_01.prefab";
    private const string ShelfPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Shelf_01_Combined.prefab";
    private const string TablePrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Table_02.prefab";
    private const string GeneratorPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Generator_01.prefab";
    private const string MedicalShelfPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Medical_Shelf_01.prefab";
    private const string MedicalBoxPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_MedicalBox_01.prefab";
    private const string PowerBoxPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_PowerBoxes_01.prefab";
    private const string BarrelWaterPrefabPath = "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Barrel_Water_01.prefab";

    [MenuItem("Tools/Zombie Rogue/Scenes/Build Bunker Shelter")]
    public static void BuildAndSaveBunkerScene()
    {
        Scene scene = OpenOrCreateBunkerScene();
        BuildScene(scene);
        EnsureBunkerInBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Bunker scene was generated from PolygonApocalypse prefabs.");
    }

    [MenuItem("Tools/Zombie Rogue/Scenes/Queue Bunker Shelter Build")]
    public static void QueueBuildOnNextEditorLoad()
    {
        Directory.CreateDirectory("Library");
        File.WriteAllText(AutoBuildRequestPath, DateTime.Now.ToString("O"));
        Debug.Log("Bunker scene build was queued. Reopen or focus Unity Editor to execute it.");
    }

    [InitializeOnLoadMethod]
    private static void BuildFromRequestOnLoad()
    {
        if (!File.Exists(AutoBuildRequestPath))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoBuildRequestPath))
                return;

            try
            {
                File.Delete(AutoBuildRequestPath);
                BuildAndSaveBunkerScene();
                File.WriteAllText(AutoBuildResultPath, $"OK {DateTime.Now:O}");
            }
            catch (Exception exception)
            {
                File.WriteAllText(AutoBuildResultPath, exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    private static Scene OpenOrCreateBunkerScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/_Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        return scene;
    }

    private static void BuildScene(Scene scene)
    {
        ClearGeneratedRoot();

        GameObject root = new GameObject(GeneratedRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        Transform architecture = CreateChild(root.transform, "Architecture");
        Transform gameplay = CreateChild(root.transform, "Gameplay");
        Transform props = CreateChild(root.transform, "Props");
        Transform storage = CreateChild(props, "Storage");
        Transform crafting = CreateChild(props, "Crafting");
        Transform medical = CreateChild(props, "Medical");
        Transform coop = CreateChild(props, "Coop_Waiting");

        ConfigureLighting();
        BuildRooms(architecture);
        BuildEntrance(architecture, props);
        BuildGameplayMarkers(gameplay);
        BuildStorageArea(storage);
        BuildCraftingArea(crafting);
        BuildMedicalArea(medical);
        BuildCoopWaitingArea(coop);
        GameObject player = BuildPlayer(scene, gameplay);
        if (player != null)
            RemoveNonPlayerMainCameras(player.transform);
        else
            ConfigureCamera();
    }

    private static void ClearGeneratedRoot()
    {
        GameObject oldRoot = GameObject.Find(GeneratedRootName);
        if (oldRoot != null)
            UnityEngine.Object.DestroyImmediate(oldRoot);
    }

    private static void BuildRooms(Transform parent)
    {
        for (int x = -4; x <= 4; x++)
        {
            for (int z = -3; z <= 3; z++)
            {
                string path = Mathf.Abs(x) <= 1 && Mathf.Abs(z) <= 1 ? ConcreteFloorPrefabPath : FloorPrefabPath;
                PlacePrefab(path, parent, $"Floor_{x}_{z}", new Vector3(x * Tile, 0f, z * Tile), Vector3.zero);
            }
        }

        for (int x = -4; x <= 4; x++)
        {
            bool isDoor = x == 0;
            PlacePrefab(isDoor ? DoorWallPrefabPath : WallPrefabPath, parent, $"Wall_North_{x}", new Vector3(x * Tile, 0f, 3.5f * Tile), new Vector3(0f, 180f, 0f));
            PlacePrefab(WallPrefabPath, parent, $"Wall_South_{x}", new Vector3(x * Tile, 0f, -3.5f * Tile), Vector3.zero);
        }

        for (int z = -3; z <= 3; z++)
        {
            PlacePrefab(WallPrefabPath, parent, $"Wall_West_{z}", new Vector3(-4.5f * Tile, 0f, z * Tile), new Vector3(0f, 90f, 0f));
            PlacePrefab(WallPrefabPath, parent, $"Wall_East_{z}", new Vector3(4.5f * Tile, 0f, z * Tile), new Vector3(0f, -90f, 0f));
        }

        for (int x = -4; x <= 4; x++)
        {
            for (int z = -3; z <= 3; z++)
            {
                string path = (x + z) % 5 == 0 ? VentCeilingPrefabPath : CeilingPrefabPath;
                PlacePrefab(path, parent, $"Ceiling_{x}_{z}", new Vector3(x * Tile, 3.2f, z * Tile), Vector3.zero);
            }
        }

        Vector3[] pillarPositions =
        {
            new(-14f, 0f, -10f),
            new(14f, 0f, -10f),
            new(-14f, 0f, 10f),
            new(14f, 0f, 10f),
            new(-6f, 0f, 0f),
            new(6f, 0f, 0f)
        };

        for (int i = 0; i < pillarPositions.Length; i++)
            PlacePrefab(PillarPrefabPath, parent, $"Pillar_{i + 1:00}", pillarPositions[i], Vector3.zero);

        CreateCollider(parent, "Bunker_WalkableFloorCollider", new Vector3(0f, -0.12f, 0f), new Vector3(38f, 0.24f, 30f));
        CreateCollider(parent, "Bunker_NorthWallCollider", new Vector3(0f, 1.5f, 15.2f), new Vector3(38f, 3f, 0.6f));
        CreateCollider(parent, "Bunker_SouthWallCollider", new Vector3(0f, 1.5f, -15.2f), new Vector3(38f, 3f, 0.6f));
        CreateCollider(parent, "Bunker_WestWallCollider", new Vector3(-18.2f, 1.5f, 0f), new Vector3(0.6f, 3f, 30f));
        CreateCollider(parent, "Bunker_EastWallCollider", new Vector3(18.2f, 1.5f, 0f), new Vector3(0.6f, 3f, 30f));

        AddLightRig(parent);
    }

    private static void BuildEntrance(Transform architecture, Transform props)
    {
        PlacePrefab(EntrancePrefabPath, architecture, "Exterior_BunkerEntrance", new Vector3(0f, 0f, 20f), new Vector3(0f, 180f, 0f));
        PlacePrefab(StairsPrefabPath, architecture, "Entrance_Stairs", new Vector3(0f, 0f, 14f), new Vector3(0f, 180f, 0f));
        PlacePrefab(HatchPrefabPath, architecture, "Entrance_RoundHatch", new Vector3(0f, 2.7f, 12.7f), new Vector3(0f, 180f, 0f));

        PlacePrefab(WallPipePrefabPath, props, "North_WallPipe", new Vector3(-8f, 1.7f, 14.55f), new Vector3(0f, 180f, 0f));
        PlacePrefab(WallWiresPrefabPath, props, "South_WallWires", new Vector3(8f, 1.5f, -14.55f), Vector3.zero);
        PlacePrefab(RoofWirePrefabPath, props, "Roof_Wires_A", new Vector3(-7f, 3.25f, 1f), Vector3.zero);
        PlacePrefab(RoofWirePrefabPath, props, "Roof_Wires_B", new Vector3(9f, 3.25f, -5f), new Vector3(0f, 90f, 0f));
    }

    private static void BuildGameplayMarkers(Transform parent)
    {
        Vector3[] spawnPositions =
        {
            new(0f, 0.15f, -6f),
            new(2.2f, 0.15f, -6f),
            new(-2.2f, 0.15f, -6f),
            new(4.4f, 0.15f, -6f),
            new(-4.4f, 0.15f, -6f),
            new(0f, 0.15f, -8.2f),
            new(2.2f, 0.15f, -8.2f),
            new(-2.2f, 0.15f, -8.2f)
        };

        for (int i = 0; i < spawnPositions.Length; i++)
            CreateMarker(parent, $"Bunker_PlayerSpawn_{i + 1:00}", spawnPositions[i], new Vector3(0f, 0f, 0f));

        CreateMarker(parent, "Bunker_RaidSelectionTerminal_Point", new Vector3(0f, 0.15f, 8f), new Vector3(0f, 180f, 0f));
        CreateMarker(parent, "Bunker_CraftingArea_Point", new Vector3(11f, 0.15f, -2f), new Vector3(0f, -90f, 0f));
        CreateMarker(parent, "Bunker_StorageArea_Point", new Vector3(-11f, 0.15f, -2f), new Vector3(0f, 90f, 0f));
    }

    private static void BuildStorageArea(Transform parent)
    {
        GameObject chestA = PlacePrefab(LargeCratePrefabPath, parent, "Storage_Chest_01", new Vector3(-12.5f, 0f, -5f), new Vector3(0f, 35f, 0f));
        ConfigureStorageContainer(chestA, "Хранилище 01", "bunker_storage_01", Rarity.Common);

        GameObject chestB = PlacePrefab(CratePrefabPath, parent, "Storage_Chest_02", new Vector3(-15f, 0f, -1f), new Vector3(0f, 90f, 0f));
        ConfigureStorageContainer(chestB, "Хранилище 02", "bunker_storage_02", Rarity.Uncommon);

        GameObject chestC = PlacePrefab(AmmoBoxPrefabPath, parent, "Ammo_Locker", new Vector3(-11f, 0f, 3.2f), new Vector3(0f, -35f, 0f));
        ConfigureStorageContainer(chestC, "Оружейный ящик", "bunker_ammo_locker", Rarity.Rare);

        PlacePrefab(ShelfPrefabPath, parent, "Storage_Shelf_A", new Vector3(-16f, 0f, 6f), new Vector3(0f, 90f, 0f));
        PlacePrefab(ShelfPrefabPath, parent, "Storage_Shelf_B", new Vector3(-16f, 0f, 9.5f), new Vector3(0f, 90f, 0f));
    }

    private static void BuildCraftingArea(Transform parent)
    {
        PlacePrefab(WorkShelfPrefabPath, parent, "Crafting_Workbench", new Vector3(12f, 0f, -4.5f), new Vector3(0f, -90f, 0f));
        PlacePrefab(TablePrefabPath, parent, "Repair_Table", new Vector3(11f, 0f, 1.3f), new Vector3(0f, 25f, 0f));
        PlacePrefab(GeneratorPrefabPath, parent, "Bunker_Generator", new Vector3(15.5f, 0f, 6.5f), new Vector3(0f, -90f, 0f));
        PlacePrefab(PowerBoxPrefabPath, parent, "RaidSelection_Terminal", new Vector3(0f, 0f, 10.8f), new Vector3(0f, 180f, 0f));
    }

    private static void BuildMedicalArea(Transform parent)
    {
        PlacePrefab(MedicalShelfPrefabPath, parent, "Medical_Shelf", new Vector3(8.5f, 0f, 10f), new Vector3(0f, 180f, 0f));
        PlacePrefab(MedicalBoxPrefabPath, parent, "Medical_Box", new Vector3(5.5f, 0f, 11f), new Vector3(0f, -20f, 0f));
        PlacePrefab(BarrelWaterPrefabPath, parent, "Water_Barrel", new Vector3(13.5f, 0f, 10.5f), new Vector3(0f, 40f, 0f));
    }

    private static void BuildCoopWaitingArea(Transform parent)
    {
        PlacePrefab(TablePrefabPath, parent, "Waiting_Table", new Vector3(0f, 0f, -10.5f), Vector3.zero);
        PlacePrefab(CratePrefabPath, parent, "Waiting_Crate_A", new Vector3(5.5f, 0f, -11.5f), new Vector3(0f, 25f, 0f));
        PlacePrefab(CratePrefabPath, parent, "Waiting_Crate_B", new Vector3(-5.5f, 0f, -11.5f), new Vector3(0f, -25f, 0f));
    }

    private static GameObject BuildPlayer(Scene scene, Transform parent)
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogWarning($"Player prefab was not found at {PlayerPrefabPath}. Bunker spawn markers were still created.");
            return null;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = "Player";
        player.transform.SetParent(parent, false);
        player.transform.localPosition = new Vector3(0f, 0.15f, -6f);
        player.transform.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(player);
        return player;
    }

    private static void RemoveNonPlayerMainCameras(Transform playerRoot)
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.transform.IsChildOf(playerRoot))
                continue;

            bool looksLikeMainCamera = camera.CompareTag("MainCamera") ||
                camera.name.Equals("Main Camera", StringComparison.OrdinalIgnoreCase) ||
                camera.name.Equals("MainCamera", StringComparison.OrdinalIgnoreCase);

            if (looksLikeMainCamera)
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    private static void AddLightRig(Transform parent)
    {
        Vector3[] lightPositions =
        {
            new(-10f, 3f, -8f),
            new(0f, 3f, -8f),
            new(10f, 3f, -8f),
            new(-10f, 3f, 3f),
            new(0f, 3f, 3f),
            new(10f, 3f, 3f),
            new(0f, 3f, 11f)
        };

        for (int i = 0; i < lightPositions.Length; i++)
        {
            Vector3 position = lightPositions[i];
            PlacePrefab(i % 2 == 0 ? LightPrefabPath : FluorescentLightPrefabPath, parent, $"Bunker_LightMesh_{i + 1:00}", position, Vector3.zero);
            GameObject lightObject = new GameObject($"Bunker_PointLight_{i + 1:00}", typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position + Vector3.down * 0.2f;
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 11f;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.91f, 0.72f);
        }
    }

    private static void ConfigureLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.05f, 0.055f, 0.06f);
        RenderSettings.fogDensity = 0.006f;

        Light directional = UnityEngine.Object.FindObjectOfType<Light>();
        if (directional == null)
        {
            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            directional = lightObject.GetComponent<Light>();
        }

        directional.name = "Directional Light";
        directional.type = LightType.Directional;
        directional.intensity = 0.35f;
        directional.color = new Color(0.9f, 0.86f, 0.74f);
        directional.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
        }

        camera.name = "Main Camera";
        camera.transform.position = new Vector3(0f, 8f, -22f);
        camera.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 150f;
    }

    private static GameObject PlacePrefab(string path, Transform parent, string name, Vector3 localPosition, Vector3 localEuler, Vector3? localScale = null)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject instance;

        if (prefab != null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Debug.LogWarning($"Missing prefab: {path}. A cube placeholder was created for {name}.");
        }

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        instance.transform.localScale = localScale ?? Vector3.one;
        MarkStatic(instance);
        EditorUtility.SetDirty(instance);
        return instance;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static GameObject CreateMarker(Transform parent, string name, Vector3 localPosition, Vector3 localEuler)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.Euler(localEuler);
        return marker;
    }

    private static void CreateCollider(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject colliderObject = new GameObject(name, typeof(BoxCollider));
        colliderObject.transform.SetParent(parent, false);
        colliderObject.transform.localPosition = center;
        BoxCollider collider = colliderObject.GetComponent<BoxCollider>();
        collider.size = size;
        MarkStatic(colliderObject);
    }

    private static void ConfigureStorageContainer(GameObject target, string displayName, string networkId, Rarity rarity)
    {
        if (target == null)
            return;

        LootContainer container = target.GetComponent<LootContainer>();
        if (container == null)
            container = target.AddComponent<LootContainer>();

        SerializedObject serialized = new SerializedObject(container);
        serialized.FindProperty("containerName").stringValue = displayName;
        serialized.FindProperty("networkId").stringValue = networkId;
        serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
        serialized.FindProperty("maxSlots").intValue = 32;
        serialized.FindProperty("interactionRange").floatValue = 3f;
        serialized.FindProperty("firstSearchDelay").floatValue = 0f;
        serialized.FindProperty("ownerType").enumValueIndex = (int)LootContainerOwnerType.World;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (target.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = target.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.6f, 1.2f, 1.6f);
        }

        if (target.GetComponent<SaveableObject>() == null)
            target.AddComponent<SaveableObject>();

        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(container);
    }

    private static void MarkStatic(GameObject target)
    {
        if (target == null)
            return;

        StaticEditorFlags flags =
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.NavigationStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic;

        GameObjectUtility.SetStaticEditorFlags(target, flags);
    }

    private static void EnsureBunkerInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (string.Equals(scenes[i].path, ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
