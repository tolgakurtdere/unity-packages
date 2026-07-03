using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK.Core.App
{
    /// <summary>
    /// Static utility for scene transitions.
    /// SplashScene → MainScene (additive, persistent) → GameScene (additive, per-level).
    /// </summary>
    public static class SceneLoader
    {
        public const string SPLASH_SCENE = "SplashScene";
        public const string MAIN_SCENE = "MainScene";
        public const string GAME_SCENE = "GameScene";

        private static string s_activeGameScene;

        /// <summary>
        /// Called from AppBootstrapper in SplashScene.
        /// Loads MainScene additively, sets it as active, then unloads SplashScene.
        /// </summary>
        public static async Awaitable LoadMainSceneAsync(string mainSceneName = MAIN_SCENE, string splashSceneName = SPLASH_SCENE)
        {
            Debug.Log($"[SceneLoader] Loading {mainSceneName} additively from SplashScene...");

            var mainScene = SceneManager.GetSceneByName(mainSceneName);
            if (!mainScene.IsValid() || !mainScene.isLoaded)
            {
                await SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Additive);
                mainScene = SceneManager.GetSceneByName(mainSceneName);
            }

            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                SceneManager.SetActiveScene(mainScene);
            }

            var splashScene = SceneManager.GetSceneByName(splashSceneName);
            if (splashScene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(splashScene);
            }
        }

        /// <summary>
        /// Loads a game scene additively. Called by AppInstaller when starting a level.
        /// Sets the game scene as active so runtime-instantiated objects belong to it.
        /// </summary>
        public static async Awaitable LoadGameAsync(string gameSceneName)
        {
            var requestedScene = SceneManager.GetSceneByName(gameSceneName);
            if (s_activeGameScene == gameSceneName && requestedScene.IsValid() && requestedScene.isLoaded)
            {
                return;
            }

            Debug.Log($"[SceneLoader] Loading game scene: {gameSceneName}");

            if (!string.IsNullOrEmpty(s_activeGameScene))
            {
                var activeGameScene = SceneManager.GetSceneByName(s_activeGameScene);
                if (activeGameScene.IsValid() && activeGameScene.isLoaded)
                {
                    await SceneManager.UnloadSceneAsync(activeGameScene);
                }
            }

            s_activeGameScene = gameSceneName;
            await SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Additive);

            requestedScene = SceneManager.GetSceneByName(gameSceneName);
            if (requestedScene.IsValid() && requestedScene.isLoaded)
            {
                SceneManager.SetActiveScene(requestedScene);
            }
        }

        /// <summary>
        /// Unloads the active game scene and returns active scene to MainScene.
        /// Called when a level ends (Win/Lose) or before loading a new level.
        /// </summary>
        public static async Awaitable UnloadGameAsync(string mainSceneName = MAIN_SCENE)
        {
            if (string.IsNullOrEmpty(s_activeGameScene)) return;

            Debug.Log($"[SceneLoader] Unloading game scene: {s_activeGameScene}");

            var activeGameScene = SceneManager.GetSceneByName(s_activeGameScene);
            if (activeGameScene.IsValid() && activeGameScene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(activeGameScene);
            }

            s_activeGameScene = null;

            var mainScene = SceneManager.GetSceneByName(mainSceneName);
            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                SceneManager.SetActiveScene(mainScene);
            }
        }
    }
}
