#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MvpVerticalSliceSetupTool
{
    private const string RaidDataFolder = "Assets/_Project/Resources/RuntimeLoadedOnly/Data/Raid";
    private const string MissionAssetPath = RaidDataFolder + "/MVP_KillZombies.asset";
    private const string LocationAssetPath = RaidDataFolder + "/MVP_CityLocation.asset";

    [MenuItem("Zombie Rogue/MVP/Create Or Refresh Test Assets")]
    public static void CreateOrRefreshTestAssetsMenu()
    {
        TestRaidAssets assets = CreateOrRefreshTestAssets();
        Selection.activeObject = assets.location;
        Debug.Log("MVP raid test assets are ready.");
    }

    [MenuItem("Zombie Rogue/MVP/Setup Open Bunker Scene")]
    public static void SetupOpenBunkerScene()
    {
        TestRaidAssets assets = CreateOrRefreshTestAssets();

        GameObject runtimeObject = FindOrCreateSceneObject("MVP Bunker Runtime");
        BunkerManager bunkerManager = EnsureComponent<BunkerManager>(runtimeObject);
        LocationSelectionUI locationSelectionUI = EnsureComponent<LocationSelectionUI>(runtimeObject);

        GameObject storageObject = FindOrCreateSceneObject("MVP Bunker Storage");
        BunkerStorage storage = EnsureComponent<BunkerStorage>(storageObject);
        LootContainer storageContainer = EnsureComponent<LootContainer>(storageObject);

        GameObject terminalObject = FindSceneObject("MVP Bunker Terminal")
            ?? FindSceneObject("Bunker_RaidSelectionTerminal_Point")
            ?? FindOrCreateSceneObject("MVP Bunker Terminal");

        BunkerTerminal terminal = EnsureComponent<BunkerTerminal>(terminalObject);
        BoxCollider terminalTrigger = EnsureComponent<BoxCollider>(terminalObject);
        terminalTrigger.isTrigger = true;
        terminalTrigger.center = new Vector3(0f, 1f, 0f);
        terminalTrigger.size = new Vector3(2.5f, 2f, 2.5f);

        AssignObjectReference(storage, "storageContainer", storageContainer);
        AssignObjectReference(bunkerManager, "storage", storage);
        AssignLocations(bunkerManager, assets.location);
        AssignObjectReference(terminal, "bunkerManager", bunkerManager);
        AssignObjectReference(terminal, "locationSelectionUI", locationSelectionUI);

        MarkActiveSceneDirty();
        Selection.activeGameObject = terminalObject;
        Debug.Log("Open bunker scene is prepared for the MVP vertical slice.");
    }

    [MenuItem("Zombie Rogue/MVP/Setup Open Raid Scene")]
    public static void SetupOpenRaidScene()
    {
        TestRaidAssets assets = CreateOrRefreshTestAssets();

        GameObject runtimeObject = FindOrCreateSceneObject("MVP Raid Runtime");
        ObjectiveManager objectiveManager = EnsureComponent<ObjectiveManager>(runtimeObject);
        RaidStatsTracker statsTracker = EnsureComponent<RaidStatsTracker>(runtimeObject);
        RewardCalculator rewardCalculator = EnsureComponent<RewardCalculator>(runtimeObject);
        RaidManager raidManager = EnsureComponent<RaidManager>(runtimeObject);
        RaidObjectiveUI objectiveUI = EnsureComponent<RaidObjectiveUI>(runtimeObject);
        RaidResultUI resultUI = EnsureComponent<RaidResultUI>(runtimeObject);

        AssignObjectReference(raidManager, "selectedLocation", assets.location);
        AssignObjectReference(raidManager, "selectedMission", assets.mission);
        AssignObjectReference(raidManager, "objectiveManager", objectiveManager);
        AssignObjectReference(raidManager, "statsTracker", statsTracker);
        AssignObjectReference(raidManager, "rewardCalculator", rewardCalculator);
        AssignObjectReference(raidManager, "resultUI", resultUI);
        AssignBool(raidManager, "autoStartOnStart", true);

        AssignObjectReference(objectiveUI, "objectiveManager", objectiveManager);
        AssignObjectReference(objectiveUI, "raidManager", raidManager);

        GameObject extractionObject = FindOrCreateSceneObject("MVP Extraction Point");
        if (extractionObject.transform.position == Vector3.zero)
            extractionObject.transform.position = new Vector3(0f, 1f, 8f);

        BoxCollider trigger = EnsureComponent<BoxCollider>(extractionObject);
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.5f, 0f);
        trigger.size = new Vector3(4f, 3f, 4f);

        GameObject visual = FindChild(extractionObject.transform, "MVP Extraction Visual");
        if (visual == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "MVP Extraction Visual";
            Undo.RegisterCreatedObjectUndo(visual, "Create MVP extraction visual");
            visual.transform.SetParent(extractionObject.transform, false);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Object.DestroyImmediate(visualCollider);
        }

        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(4f, 0.12f, 4f);

        ExtractionPoint extractionPoint = EnsureComponent<ExtractionPoint>(extractionObject);
        AssignObjectReference(extractionPoint, "raidManager", raidManager);
        AssignObjectReference(extractionPoint, "visualRoot", visual);
        AssignObjectReference(extractionPoint, "triggerCollider", trigger);
        AssignBool(extractionPoint, "activeOnStart", false);

        MarkActiveSceneDirty();
        Selection.activeGameObject = extractionObject;
        Debug.Log("Open raid scene is prepared for the MVP vertical slice.");
    }

    private static TestRaidAssets CreateOrRefreshTestAssets()
    {
        EnsureFolder(RaidDataFolder);

        MissionDefinition mission = AssetDatabase.LoadAssetAtPath<MissionDefinition>(MissionAssetPath);
        if (mission == null)
        {
            mission = ScriptableObject.CreateInstance<MissionDefinition>();
            AssetDatabase.CreateAsset(mission, MissionAssetPath);
        }

        mission.missionId = "mvp_kill_zombies";
        mission.displayName = "Kill 5 zombies";
        mission.description = "Eliminate the first infected group and unlock extraction.";
        mission.missionType = MissionType.KillZombies;
        mission.targetCount = 5;
        mission.experienceReward = 200;
        mission.isRequired = true;
        EditorUtility.SetDirty(mission);

        LocationDefinition location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(LocationAssetPath);
        if (location == null)
        {
            location = ScriptableObject.CreateInstance<LocationDefinition>();
            AssetDatabase.CreateAsset(location, LocationAssetPath);
        }

        location.locationId = "city_mvp";
        location.displayName = "Infected City";
        location.description = "A compact test raid for the first vertical slice.";
        location.sceneName = "City";
        location.difficulty = 1;
        location.recommendedLevel = 1;
        location.baseExperienceReward = 100;
        location.isUnlockedByDefault = true;
        location.availableMissions = new List<MissionDefinition> { mission };
        EditorUtility.SetDirty(location);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new TestRaidAssets
        {
            location = location,
            mission = mission
        };
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        if (parts.Length == 0)
            return;

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static void AssignLocations(BunkerManager bunkerManager, LocationDefinition location)
    {
        SerializedObject serializedObject = new SerializedObject(bunkerManager);
        SerializedProperty locations = serializedObject.FindProperty("locations");
        if (locations != null)
        {
            locations.arraySize = 1;
            locations.GetArrayElementAtIndex(0).objectReferenceValue = location;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(bunkerManager);
    }

    private static void AssignObjectReference(Object target, string propertyName, Object value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void AssignBool(Object target, string propertyName, bool value)
    {
        if (target == null)
            return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        return Undo.AddComponent<T>(target);
    }

    private static GameObject FindOrCreateSceneObject(string name)
    {
        GameObject existing = FindSceneObject(name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create " + name);
        return created;
    }

    private static GameObject FindSceneObject(string name)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform != null && transform.gameObject.scene == activeScene && transform.name == name)
                return transform.gameObject;
        }

        return null;
    }

    private static GameObject FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name == childName)
                return child.gameObject;
        }

        return null;
    }

    private static void MarkActiveSceneDirty()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private struct TestRaidAssets
    {
        public LocationDefinition location;
        public MissionDefinition mission;
    }
}
#endif
