using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Application entry point living in the splash scene (Build Index 0).
    /// Loads the configured main scene additively via SceneLoader (which also unloads the splash
    /// scene). The composition root in the main scene (e.g. your AppFlowBase subclass) takes over
    /// once the scene is ready.
    /// </summary>
    public class AppBootstrapper : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float splashDelaySeconds = 2f;

        [Tooltip("Scene loaded additively as the persistent main scene.")]
        [SerializeField] private string mainSceneName = SceneLoader.MAIN_SCENE;

        [Tooltip("The splash scene to unload once the main scene is up.")]
        [SerializeField] private string splashSceneName = SceneLoader.SPLASH_SCENE;

#pragma warning disable 0414 // each is read on only one side of the UNITY_EDITOR split below
        [Tooltip("DEPRECATED — superseded by the TKStartupSettings asset's LogPolicy, which is applied " +
                 "before the splash screen (this runs in Start(), so anything logged earlier escapes it) " +
                 "and which leaves development builds alone. Kept as a fallback: it still applies when no " +
                 "TKStartupSettings asset exists, so upgrading without creating one changes nothing.")]
        [SerializeField] private bool disableLogsInReleaseBuilds = true;

        [Tooltip("Skip the splash delay in the Editor for faster iteration.")]
        [SerializeField] private bool skipSplashDelayInEditor;
#pragma warning restore 0414

        private async void Start()
        {
#if !UNITY_EDITOR
            // Fallback only. A TKStartupSettings asset owns log policy and already applied it before the
            // splash screen; this field is left acting solely for projects that have not created one, so
            // upgrading core without adding the asset does not silently switch release logging back on.
            if (StartupSettings.Active == null && disableLogsInReleaseBuilds) Debug.unityLogger.logEnabled = false;
#endif

            var delaySeconds = splashDelaySeconds;
#if UNITY_EDITOR
            if (skipSplashDelayInEditor) delaySeconds = 0f;
#endif

            // Fake Loading / Splash Delay
            var startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < delaySeconds)
            {
                await Awaitable.NextFrameAsync();
            }

            await SceneLoader.LoadMainSceneAsync(mainSceneName, splashSceneName);
        }
    }
}
