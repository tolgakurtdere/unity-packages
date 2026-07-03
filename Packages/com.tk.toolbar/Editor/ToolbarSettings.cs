using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace TK.Toolbar.Editor
{
    /// <summary>
    /// Configuration for the TK toolbar. Create one via Assets > Create > TK > Toolbar Settings
    /// anywhere in the project; the toolbar finds it by type. Without an asset, the time scale
    /// controls use defaults and no scene buttons are shown.
    /// </summary>
    [CreateAssetMenu(fileName = "ToolbarSettings", menuName = "TK/Toolbar Settings")]
    public class ToolbarSettings : ScriptableObject
    {
        [Header("Time Scale")]
        [Min(0f)] public float minTimeScale;
        public float maxTimeScale = 2f;
        public float defaultTimeScale = 1f;

        [HideInInspector] public float lastKnownTimeScale = 1f;

        [Header("Scene Buttons")]
        [Tooltip("One toolbar button is added per scene, labeled by position.")]
        public SceneAsset[] scenes = System.Array.Empty<SceneAsset>();

        private void OnValidate()
        {
            EditorApplication.delayCall += () => MainToolbar.Refresh("TK Toolbar/TimeTweaker");
        }
    }
}
