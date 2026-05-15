using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ZombieSpawnerEditorTools
{
    private const string SpawnerName = "Zombie Spawner";
    private const string ZombiePrefabFolder = "Assets/Resources/Prefabs/Zombie";

    [MenuItem("Tools/Zombie Rogue/Create Zombie Spawner In Scene")]
    public static void CreateOrUpdateSpawnerInActiveScene()
    {
        ZombieSpawner spawner = Object.FindObjectOfType<ZombieSpawner>();
        GameObject spawnerObject;

        if (spawner == null)
        {
            spawnerObject = new GameObject(SpawnerName);
            spawner = spawnerObject.AddComponent<ZombieSpawner>();
        }
        else
        {
            spawnerObject = spawner.gameObject;
            spawnerObject.name = SpawnerName;
        }

        AssignDefaults(spawner);

        Selection.activeGameObject = spawnerObject;
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Zombie spawner is ready in the active scene.", spawner);
    }

    private static void AssignDefaults(ZombieSpawner spawner)
    {
        SerializedObject serializedSpawner = new(spawner);

        AssignPrefabArray(serializedSpawner.FindProperty("zombiePrefabs"), LoadZombiePrefabs());

        SerializedProperty playerProperty = serializedSpawner.FindProperty("player");
        if (playerProperty != null && playerProperty.objectReferenceValue == null)
            playerProperty.objectReferenceValue = FindPlayerTransform();

        SerializedProperty cameraProperty = serializedSpawner.FindProperty("spawnCamera");
        if (cameraProperty != null && cameraProperty.objectReferenceValue == null)
            cameraProperty.objectReferenceValue = Camera.main;

        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<GameObject> LoadZombiePrefabs()
    {
        List<GameObject> prefabs = new();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ZombiePrefabFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && prefab.GetComponentInChildren<ZombieAI>(true) != null)
                prefabs.Add(prefab);
        }

        return prefabs;
    }

    private static void AssignPrefabArray(SerializedProperty arrayProperty, List<GameObject> prefabs)
    {
        if (arrayProperty == null || !arrayProperty.isArray || prefabs.Count == 0)
            return;

        arrayProperty.arraySize = prefabs.Count;

        for (int i = 0; i < prefabs.Count; i++)
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
    }

    private static Transform FindPlayerTransform()
    {
        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            return playerObject != null ? playerObject.transform : null;
        }
        catch (UnityException)
        {
            return null;
        }
    }
}
