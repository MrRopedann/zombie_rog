using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CharacterAnimatorConfigurator
{
    private const string ControllerPath = "Assets/_Project/Art/Animations/Character/CharacterAnimatorController.controller";
    private const string DeathStateName = "Death";
    private const string DamageStateName = "CombatDamage01";
    private const string DamageLayerName = "Aiming";
    private const string DamageClipPath = "Assets/_Project/Art/Animations/Character/HumanM@CombatDamage01.fbx";
    private const string DamageClipName = "CombatDamage01";
    private const string ReloadStateName = "reload";
    private const string ReloadClipPath = "Assets/_Project/Art/Animations/Character/Weapon/Reload.fbx";
    private const string ReloadClipName = "reload";
    private const string DeathParameter = "isDead";
    private const string DamageParameter = "isDamage";
    private const string ReloadParameter = "Reload";
    private const string ReloadSpeedParameter = "ReloadSpeed";
    private const string AssaultAimParameter = "AssasultAim";

    [MenuItem("Tools/Zombie Rogue/Configure Character Animator")]
    public static void Configure()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            Debug.LogWarning($"Character animator controller not found: {ControllerPath}");
            return;
        }

        EnsureBoolParameter(controller, DeathParameter);
        EnsureBoolParameter(controller, DamageParameter);
        EnsureTriggerParameter(controller, ReloadParameter);
        EnsureFloatParameter(controller, ReloadSpeedParameter, 0.85f);

        AnimatorStateMachine baseStateMachine = controller.layers[0].stateMachine;
        AnimatorState deathState = FindState(baseStateMachine, DeathStateName);
        AnimatorState baseDamageState = FindState(baseStateMachine, DamageStateName);
        Motion damageMotion = baseDamageState != null ? baseDamageState.motion : LoadDamageMotion();

        if (deathState != null)
        {
            ConfigureStateClipImport(deathState, loop: false, lockRootMotion: false);
            ClearStateTransitions(deathState);
            EnsureAnyBoolTransition(baseStateMachine, deathState, DeathParameter, 0.1f);
        }
        else
        {
            Debug.LogWarning($"Character animator state not found: {DeathStateName}");
        }

        if (baseDamageState == null && damageMotion != null)
        {
            baseDamageState = baseStateMachine.AddState(DamageStateName, new Vector3(240f, 180f, 0f));
        }

        if (baseDamageState != null)
        {
            baseDamageState.motion = damageMotion;
            baseDamageState.writeDefaultValues = true;
            ConfigureStateClipImport(baseDamageState, loop: false, lockRootMotion: true);
            ClearStateTransitions(baseDamageState);
            EnsureAnyBoolTransition(baseStateMachine, baseDamageState, DamageParameter, 0.04f);
            EnsureExitTransition(baseDamageState, baseStateMachine.defaultState, 0.9f, 0.08f);
        }
        else
        {
            Debug.LogWarning($"Character animator damage clip not found: {DamageClipPath}");
        }

        RemoveDamageStateFromWeaponLayer(controller);
        ConfigureReloadLayer(controller);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Character animator configured.");
    }

    private static void EnsureBoolParameter(AnimatorController controller, string parameterName)
    {
        EnsureParameter(controller, parameterName, AnimatorControllerParameterType.Bool, 0f);
    }

    private static void EnsureTriggerParameter(AnimatorController controller, string parameterName)
    {
        EnsureParameter(controller, parameterName, AnimatorControllerParameterType.Trigger, 0f);
    }

    private static void EnsureFloatParameter(AnimatorController controller, string parameterName, float defaultValue)
    {
        EnsureParameter(controller, parameterName, AnimatorControllerParameterType.Float, defaultValue);
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType,
        float defaultFloat)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name != parameterName)
                continue;

            if (parameter.type == parameterType)
            {
                if (parameterType == AnimatorControllerParameterType.Float)
                    parameter.defaultFloat = defaultFloat;

                return;
            }

            controller.RemoveParameter(parameter);
            break;
        }

        AnimatorControllerParameter newParameter = new()
        {
            name = parameterName,
            type = parameterType,
            defaultFloat = defaultFloat
        };

        controller.AddParameter(newParameter);
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state != null
                && string.Equals(childState.state.name, stateName, System.StringComparison.OrdinalIgnoreCase))
            {
                return childState.state;
            }
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            AnimatorState state = FindState(childStateMachine.stateMachine, stateName);
            if (state != null)
                return state;
        }

        return null;
    }

    private static void RemoveDamageStateFromWeaponLayer(AnimatorController controller)
    {
        int layerIndex = FindLayerIndex(controller, DamageLayerName);
        if (layerIndex < 0)
        {
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[layerIndex].stateMachine;
        AnimatorState damageState = FindState(stateMachine, DamageStateName);

        if (damageState == null)
        {
            return;
        }

        RemoveAnyTransitionsTo(stateMachine, damageState);
        RemoveStateTransitionsTo(stateMachine, damageState);
        ClearStateTransitions(damageState);
        stateMachine.RemoveState(damageState);
    }

    private static void ConfigureReloadLayer(AnimatorController controller)
    {
        Motion reloadMotion = LoadReloadMotion();

        if (reloadMotion == null)
        {
            Debug.LogWarning($"Reload animation clip not found: {ReloadClipPath}");
            return;
        }

        int layerIndex = FindLayerIndex(controller, DamageLayerName);

        if (layerIndex < 0)
        {
            Debug.LogWarning($"Reload animation layer not found: {DamageLayerName}");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[layerIndex].stateMachine;
        AnimatorState reloadState = FindState(stateMachine, ReloadStateName);

        if (reloadState == null)
        {
            reloadState = stateMachine.AddState(ReloadStateName, new Vector3(300f, 310f, 0f));
        }

        reloadState.name = ReloadStateName;
        reloadState.motion = reloadMotion;
        reloadState.writeDefaultValues = true;
        reloadState.speed = 1f;
        reloadState.speedParameterActive = true;
        reloadState.speedParameter = ReloadSpeedParameter;

        ConfigureStateClipImport(reloadState, loop: false, lockRootMotion: true);
        ClearStateTransitions(reloadState);
        RemoveAnyTransitionsTo(stateMachine, reloadState);

        AnimatorState assaultAimState = FindState(stateMachine, "assault aim");
        AnimatorState defaultState = stateMachine.defaultState;

        if (assaultAimState != null)
        {
            AnimatorStateTransition transition = reloadState.AddTransition(assaultAimState);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, AssaultAimParameter);
        }

        if (defaultState != null)
        {
            AnimatorStateTransition transition = reloadState.AddTransition(defaultState);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, AssaultAimParameter);
        }
    }

    private static Motion LoadReloadMotion()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ReloadClipPath);
        AnimationClip fallbackClip = null;

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is not AnimationClip clip)
                continue;

            fallbackClip ??= clip;

            if (clip.name == ReloadClipName)
                return clip;
        }

        return fallbackClip;
    }

    private static Motion LoadDamageMotion()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(DamageClipPath);
        AnimationClip fallbackClip = null;

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is not AnimationClip clip)
                continue;

            fallbackClip ??= clip;

            if (clip.name == DamageClipName)
                return clip;
        }

        return fallbackClip;
    }

    private static int FindLayerIndex(AnimatorController controller, string layerName)
    {
        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == layerName)
                return i;
        }

        return -1;
    }

    private static void ConfigureStateClipImport(AnimatorState state, bool loop, bool lockRootMotion)
    {
        if (state.motion is not AnimationClip clip)
            return;

        string clipPath = AssetDatabase.GetAssetPath(clip);
        ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;

        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation importerClip = clips[i];
            WrapMode wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

            if (importerClip.loopTime != loop)
            {
                importerClip.loopTime = loop;
                changed = true;
            }

            if (importerClip.loopPose != loop)
            {
                importerClip.loopPose = loop;
                changed = true;
            }

            if (importerClip.wrapMode != wrapMode)
            {
                importerClip.wrapMode = wrapMode;
                changed = true;
            }

            if (lockRootMotion)
            {
                if (!importerClip.lockRootRotation)
                {
                    importerClip.lockRootRotation = true;
                    changed = true;
                }

                if (!importerClip.lockRootHeightY)
                {
                    importerClip.lockRootHeightY = true;
                    changed = true;
                }

                if (!importerClip.lockRootPositionXZ)
                {
                    importerClip.lockRootPositionXZ = true;
                    changed = true;
                }

                if (!importerClip.keepOriginalOrientation)
                {
                    importerClip.keepOriginalOrientation = true;
                    changed = true;
                }

                if (!importerClip.keepOriginalPositionY)
                {
                    importerClip.keepOriginalPositionY = true;
                    changed = true;
                }

                if (!importerClip.keepOriginalPositionXZ)
                {
                    importerClip.keepOriginalPositionXZ = true;
                    changed = true;
                }
            }

            clips[i] = importerClip;
        }

        if (!changed)
            return;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static void EnsureAnyBoolTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState destinationState,
        string parameterName,
        float duration)
    {
        RemoveAnyTransitionsTo(stateMachine, destinationState, parameterName);

        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destinationState);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameterName);
    }

    private static void RemoveAnyTransitionsTo(
        AnimatorStateMachine stateMachine,
        AnimatorState destinationState,
        string parameterName)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (transition.destinationState == destinationState && HasCondition(transition, parameterName))
                stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void RemoveAnyTransitionsTo(
        AnimatorStateMachine stateMachine,
        AnimatorState destinationState)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (transition.destinationState == destinationState)
                stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void RemoveStateTransitionsTo(
        AnimatorStateMachine stateMachine,
        AnimatorState destinationState)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            if (state == null)
                continue;

            foreach (AnimatorStateTransition transition in state.transitions)
            {
                if (transition.destinationState == destinationState)
                    state.RemoveTransition(transition);
            }
        }
    }

    private static bool HasCondition(AnimatorStateTransition transition, string parameterName)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameterName)
                return true;
        }

        return false;
    }

    private static void EnsureExitTransition(
        AnimatorState from,
        AnimatorState to,
        float exitTime,
        float duration)
    {
        if (to == null)
            return;

        ClearStateTransitions(from);

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
    }

    private static void ClearStateTransitions(AnimatorState state)
    {
        foreach (AnimatorStateTransition transition in state.transitions)
        {
            state.RemoveTransition(transition);
        }
    }
}
