using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GameplaySceneLightingGuard
{
    private const string GameplaySceneName = "Demo_City_Universal_RenderPipeline";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToInitialScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (IsGameplayScene(activeScene))
            ApplyLighting(activeScene);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGameplayScene(scene))
            return;

        SceneManager.SetActiveScene(scene);
        ApplyLighting(scene);
        SceneLightingRunner.Run(ApplyNextFrame(scene));
    }

    private static IEnumerator ApplyNextFrame(Scene scene)
    {
        yield return null;

        if (scene.IsValid() && scene.isLoaded)
            ApplyLighting(scene);
    }

    private static bool IsGameplayScene(Scene scene)
    {
        return scene.IsValid() && scene.name == GameplaySceneName;
    }

    private static void ApplyLighting(Scene gameplayScene)
    {
        RenderSettings.fog = false;
        RenderSettings.fogColor = new Color(0.8000001f, 0.5372549f, 0.43921572f, 1f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.29f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.9191176f, 0.7096129f, 0.7833009f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
        RenderSettings.ambientIntensity = 1.12f;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.reflectionBounces = 1;

        Light gameplaySun = null;
        foreach (Light light in Object.FindObjectsOfType<Light>(true))
        {
            if (light.type != LightType.Directional)
                continue;

            bool belongsToGameplayScene = light.gameObject.scene == gameplayScene;
            light.enabled = belongsToGameplayScene;

            if (belongsToGameplayScene && gameplaySun == null)
                gameplaySun = light;
        }

        if (gameplaySun == null)
            return;

        gameplaySun.color = new Color(1f, 0.84129643f, 0.5955882f, 1f);
        gameplaySun.intensity = 1f;
        gameplaySun.shadows = LightShadows.Soft;
        gameplaySun.shadowStrength = 1f;
        gameplaySun.shadowBias = 0.045f;
        gameplaySun.shadowNormalBias = 0.2f;
        RenderSettings.sun = gameplaySun;
    }

    private sealed class SceneLightingRunner : MonoBehaviour
    {
        public static void Run(IEnumerator routine)
        {
            GameObject runnerObject = new GameObject("Gameplay Scene Lighting Guard");
            Object.DontDestroyOnLoad(runnerObject);
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            runnerObject.AddComponent<SceneLightingRunner>().StartCoroutineAndDestroy(routine);
        }

        private void StartCoroutineAndDestroy(IEnumerator routine)
        {
            StartCoroutine(RunAndDestroy(routine));
        }

        private IEnumerator RunAndDestroy(IEnumerator routine)
        {
            yield return routine;
            Destroy(gameObject);
        }
    }
}
