// TKToolbar — Unity 6.3 LTS official MainToolbar API
// Adds Time Scale slider, Reset button, and scene-switch buttons to the main editor toolbar.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace TK.Toolbar.Editor
{
    public static class EditorToolbar
    {
        internal const string k_ElementPath = "TK Toolbar/TimeTweaker";

        // ──────────────────────────────────────────────
        // Main toolbar element entry point
        // ──────────────────────────────────────────────

        [MainToolbarElement(k_ElementPath, defaultDockPosition = MainToolbarDockPosition.Left)]
        static IEnumerable<MainToolbarElement> CreateToolbar()
        {
            var settings = LoadSettings();
            float min = settings ? settings.minTimeScale : 0f;
            float max = settings ? settings.maxTimeScale : 2f;
            float def = settings ? settings.defaultTimeScale : 1f;

            // 1) Label
            yield return new MainToolbarLabel(
                new MainToolbarContent(ToolbarText.SliderLabel, "Current Time Scale"));

            // 2) Slider
            float currentValue = settings ? settings.lastKnownTimeScale : def;
            yield return new MainToolbarSlider(
                new MainToolbarContent("Time Scale", "Drag to change Time Scale"),
                Mathf.Clamp(currentValue, min, max),
                min,
                max,
                OnSliderValueChanged);


            // 4) Reset button
            yield return new MainToolbarButton(
                new MainToolbarContent(ToolbarText.ResetButton, "Reset Time Scale to default"),
                OnResetClicked);

            // Scene buttons — one per configured scene
            var sceneAssets = settings ? settings.scenes : null;
            if (sceneAssets != null)
            {
                for (var i = 0; i < sceneAssets.Length; i++)
                {
                    var sceneAsset = sceneAssets[i];
                    if (!sceneAsset) continue;

                    var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                    yield return new MainToolbarButton(
                        new MainToolbarContent($"🎬{i + 1}", scenePath),
                        () => TryOpenScene(scenePath))
                    {
                        enabled = !EditorApplication.isPlaying
                    };
                }
            }
        }

        // ──────────────────────────────────────────────
        // Callbacks
        // ──────────────────────────────────────────────

        private static void OnSliderValueChanged(float newValue)
        {
            Time.timeScale = newValue;

            var settings = LoadSettings();
            if (settings)
            {
                settings.lastKnownTimeScale = newValue;
                EditorUtility.SetDirty(settings);
            }

            MainToolbar.Refresh(k_ElementPath);
        }

        private static void OnResetClicked()
        {
            var settings = LoadSettings();
            float def = settings ? settings.defaultTimeScale : 1f;

            Time.timeScale = def;

            if (settings)
            {
                settings.lastKnownTimeScale = def;
                EditorUtility.SetDirty(settings);
            }

            MainToolbar.Refresh(k_ElementPath);
        }

        // ──────────────────────────────────────────────
        // Scene buttons
        // ──────────────────────────────────────────────

        private static void TryOpenScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;

            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            EditorApplication.delayCall += () =>
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
            };
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        internal static ToolbarSettings LoadSettings()
        {
            return AssetDatabase.FindAssets("t:ToolbarSettings")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ToolbarSettings>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault();
        }
    }

    // ──────────────────────────────────────────────
    // Play mode listener — resets timeScale on exit
    // ──────────────────────────────────────────────

    [InitializeOnLoad]
    static class EditorToolbarPlayModeHandler
    {
        static EditorToolbarPlayModeHandler()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            var settings = EditorToolbar.LoadSettings();

            float def = settings ? settings.defaultTimeScale : 1f;
            Time.timeScale = def;

            if (settings)
            {
                settings.lastKnownTimeScale = def;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            MainToolbar.Refresh(EditorToolbar.k_ElementPath);
        }
    }
}
