using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Application entry point living in SplashScene (Build Index 0).
    /// Loads MainScene additively via SceneLoader (which also unloads SplashScene).
    /// The composition root in MainScene (e.g. your AppFlowBase subclass) takes over once the scene is ready.
    /// </summary>
    public class AppBootstrapper : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float splashDelaySeconds = 2f;

#pragma warning disable 0414 // read only in player builds below
        [SerializeField] private bool disableLogsInReleaseBuilds = true;
#pragma warning restore 0414

        private async void Start()
        {
#if !UNITY_EDITOR
            if (disableLogsInReleaseBuilds) Debug.unityLogger.logEnabled = false;
#endif

            // Fake Loading / Splash Delay
            var startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < splashDelaySeconds)
            {
                await Awaitable.NextFrameAsync();
            }

            await SceneLoader.LoadMainSceneAsync();
        }
    }
}
