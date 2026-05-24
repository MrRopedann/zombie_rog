using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class ZombieSystemConfigurator
{
    private const string ControllerPath = "Assets/_Project/Art/Animations/Zombie/Zombie_Animations/AnimationControllers/ZombieBase.controller";
    private const string AnimationPath = "Assets/_Project/Art/Animations/Zombie/Zombie_Animations/Animations/";
    private const string BloodEffectPath = "Assets/_External/PolygonApocalypse/Prefabs/FX/Prefabbed/FX_BloodSplat_01.prefab";
    private const string NavMeshSurfaceName = "Zombie_NavMeshSurface";
    private const string NavMeshDataFolder = "Assets/_Project/Art/Terrain/Generated/NavMesh";

    private static readonly string[] ZombiePrefabPaths =
    {
        "Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Female_01.prefab",
        "Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Female_02.prefab",
        "Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Male_01.prefab",
        "Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Male_02.prefab"
    };

    [MenuItem("Tools/Zombie Rogue/Configure Zombie System")]
    public static void Configure()
    {
        AnimatorController controller = CreateAnimatorController();

        foreach (string prefabPath in ZombiePrefabPaths)
        {
            ConfigureZombiePrefab(prefabPath, controller);
        }

        BakeZombieNavMeshes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Zombie system configured.");
    }

    private static AnimatorController CreateAnimatorController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("isDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isAttacking", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        AnimatorState idle = AddState(stateMachine, "Idle", "Zombie_Idle_01.FBX", 260, 80, loop: true);
        AnimatorState walk = AddState(stateMachine, "Walk", "Zombie_Walk_01_Forward_InPlace.fbx", 520, 80, loop: true);
        AnimatorState run = AddState(stateMachine, "Run", "Zombie_Run_01_Forward_InPlace.fbx", 780, 80, loop: true);
        AnimatorState attack = AddState(stateMachine, "Attack", "Zombie_Attack01.FBX", 520, 260, loop: false);
        AnimatorState hit = AddState(stateMachine, "Hit", "Zombie_HitReact_Head.fbx", 260, 260, loop: false);
        AnimatorState death = AddState(stateMachine, "Death", "Zombie_Idle_Death.fbx", 780, 260, loop: false);

        stateMachine.defaultState = idle;

        AddFloatTransition(idle, walk, "Speed", AnimatorConditionMode.Greater, 0.05f, 0.15f);
        AddFloatTransition(walk, idle, "Speed", AnimatorConditionMode.Less, 0.05f, 0.15f);
        AddFloatTransition(walk, run, "Speed", AnimatorConditionMode.Greater, 1.6f, 0.15f);
        AddFloatTransition(run, walk, "Speed", AnimatorConditionMode.Less, 1.4f, 0.15f);

        AddAnyTriggerTransition(stateMachine, attack, "Attack", 0.05f);
        AddExitTransition(attack, idle, 0.85f, 0.08f);

        AddAnyTriggerTransition(stateMachine, hit, "Hit", 0.05f);
        AddExitTransition(hit, idle, 0.75f, 0.08f);

        AnimatorStateTransition deathTransition = stateMachine.AddAnyStateTransition(death);
        deathTransition.hasExitTime = false;
        deathTransition.duration = 0.1f;
        deathTransition.canTransitionToSelf = false;
        deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "isDead");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState AddState(
        AnimatorStateMachine stateMachine,
        string stateName,
        string clipFileName,
        float x,
        float y,
        bool loop)
    {
        ConfigureClipImport(clipFileName, loop);

        AnimatorState state = stateMachine.AddState(stateName, new Vector3(x, y, 0f));
        AnimationClip clip = LoadClip(clipFileName);
        state.motion = clip;
        state.writeDefaultValues = true;

        if (clip != null)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        return state;
    }

    private static void ConfigureClipImport(string fileName, bool loop)
    {
        string clipPath = AnimationPath + fileName;
        ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;

        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            WrapMode wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

            if (clip.loopTime == loop && clip.wrapMode == wrapMode)
                continue;

            clip.loopTime = loop;
            clip.loopPose = loop;
            clip.wrapMode = wrapMode;
            clips[i] = clip;
            changed = true;
        }

        if (!changed)
            return;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static AnimationClip LoadClip(string fileName)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationPath + fileName);

        if (clip == null)
            Debug.LogWarning($"Zombie animation clip not found: {fileName}");

        return clip;
    }

    private static void AddFloatTransition(
        AnimatorState from,
        AnimatorState to,
        string parameter,
        AnimatorConditionMode mode,
        float threshold,
        float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void AddAnyTriggerTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState to,
        string parameter,
        float duration)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
    }

    private static void ConfigureZombiePrefab(string prefabPath, RuntimeAnimatorController controller)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Animator animator = prefabRoot.GetComponent<Animator>() ?? prefabRoot.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogWarning($"Zombie prefab has no Animator: {prefabPath}");
                return;
            }

            GameObject zombieRoot = animator.gameObject;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            CapsuleCollider collider = EnsureComponent<CapsuleCollider>(zombieRoot);
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.35f;
            collider.direction = 1;
            collider.isTrigger = false;

            Rigidbody rigidbody = EnsureComponent<Rigidbody>(zombieRoot);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            NavMeshAgent agent = EnsureComponent<NavMeshAgent>(zombieRoot);
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = 2.6f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.35f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            ZombieHealth health = EnsureComponent<ZombieHealth>(zombieRoot);
            SetSerializedFloat(health, "maxHealth", 100f);
            ConfigureZombieHitEffect(health);

            ZombieAI ai = EnsureComponent<ZombieAI>(zombieRoot);
            ai.animator = animator;
            ai.health = health;
            ai.eyes = FindChildByName(zombieRoot.transform, "Head", "Eyes", "Neck");
            ai.viewDistance = 18f;
            ai.viewAngle = 90f;
            ai.peripheralAngle = 140f;
            ai.peripheralDistance = 10f;
            ai.closeSightDistance = 2.2f;
            ai.walkSpeed = 1.2f;
            ai.runSpeed = 2.6f;
            ai.attackRange = 1.8f;
            ai.attackCooldown = 1.15f;
            ai.attackDamage = 15f;
            ai.attackHitDelay = 0.35f;
            ai.attackHitRadius = 0.55f;
            ai.disableAgentOnDeath = true;
            ai.disableMainColliderOnDeath = false;
            ai.destroyAfterDeathDelay = 0f;
            ai.deathFreezeNormalizedTime = 0.98f;

            EditorUtility.SetDirty(zombieRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component == null)
            component = gameObject.AddComponent<T>();

        return component;
    }

    private static void SetSerializedFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureZombieHitEffect(ZombieHealth health)
    {
        GameObject bloodEffect = AssetDatabase.LoadAssetAtPath<GameObject>(BloodEffectPath);

        if (bloodEffect == null)
        {
            Debug.LogWarning($"Zombie blood effect not found: {BloodEffectPath}");
            return;
        }

        SerializedObject serializedObject = new SerializedObject(health);
        SerializedProperty hitEffect = serializedObject.FindProperty("hitEffect");
        SerializedProperty suppressImpact = serializedObject.FindProperty("suppressProjectileImpactEffect");
        SerializedProperty hitEffectLifetime = serializedObject.FindProperty("hitEffectLifetime");

        if (hitEffect != null)
            hitEffect.objectReferenceValue = bloodEffect;

        if (suppressImpact != null)
            suppressImpact.boolValue = true;

        if (hitEffectLifetime != null)
            hitEffectLifetime.floatValue = 3f;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BakeZombieNavMeshes()
    {
        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets/_Project/Art/Terrain/Generated", "NavMesh");

        foreach (string scenePath in FindZombieScenePaths())
        {
            BakeNavMeshForScene(scenePath);
        }
    }

    private static IEnumerable<string> FindZombieScenePaths()
    {
        HashSet<string> scenePaths = new HashSet<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                scenePaths.Add(scene.path);
        }

        string[] zombieGuids = new string[ZombiePrefabPaths.Length];
        for (int i = 0; i < ZombiePrefabPaths.Length; i++)
            zombieGuids[i] = AssetDatabase.AssetPathToGUID(ZombiePrefabPaths[i]);

        foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);

            if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
                continue;

            string sceneText = File.ReadAllText(scenePath);

            foreach (string zombieGuid in zombieGuids)
            {
                if (!string.IsNullOrWhiteSpace(zombieGuid) && sceneText.Contains(zombieGuid))
                {
                    scenePaths.Add(scenePath);
                    break;
                }
            }
        }

        return scenePaths;
    }

    private static void BakeNavMeshForScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        NavMeshData bakedData = BuildNavMeshDataForActiveScene();
        if (bakedData == null)
        {
            Debug.LogWarning($"NavMesh build returned no data for scene: {scenePath}");
            return;
        }

        string navMeshAssetPath = $"{NavMeshDataFolder}/{Path.GetFileNameWithoutExtension(scenePath)}_ZombieNavMesh.asset";

        NavMeshData oldData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshAssetPath);
        if (oldData != null)
            AssetDatabase.DeleteAsset(navMeshAssetPath);

        AssetDatabase.CreateAsset(bakedData, navMeshAssetPath);
        AssetDatabase.SaveAssets();

        GameObject navMeshObject = GameObject.Find(NavMeshSurfaceName);
        if (navMeshObject == null)
            navMeshObject = new GameObject(NavMeshSurfaceName);

        ZombieNavMeshDataSource navMeshDataSource = EnsureComponent<ZombieNavMeshDataSource>(navMeshObject);
        navMeshDataSource.NavMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshAssetPath);

        EditorUtility.SetDirty(navMeshDataSource);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"Baked zombie NavMesh for {scenePath}: {navMeshAssetPath}");
    }

    private static NavMeshData BuildNavMeshDataForActiveScene()
    {
        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
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
        Bounds bounds = new Bounds(fallbackCenter, Vector3.one * Mathf.Max(1f, fallbackRadius));
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

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Transform FindChildByName(Transform root, params string[] names)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in names)
            {
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;
            }
        }

        return root;
    }
}
