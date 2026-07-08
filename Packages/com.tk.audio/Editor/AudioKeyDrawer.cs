using UnityEditor;
using UnityEngine;

namespace TK.Audio.Editor
{
    [CustomPropertyDrawer(typeof(AudioKeyAttribute))]
    public sealed class AudioKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            AudioKeyDropdown.Draw(position, property, label, AudioCatalogKeyIndex.ScanEntryKeys());
        }
    }

    [CustomPropertyDrawer(typeof(AudioPlaylistKeyAttribute))]
    public sealed class AudioPlaylistKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            AudioKeyDropdown.Draw(position, property, label, AudioCatalogKeyIndex.ScanPlaylistKeys());
        }
    }

    /// <summary>Shared dropdown: shows the collected keys plus, when needed, the current value
    /// tagged "(missing)" so a key that no catalog defines is visible and never silently cleared.</summary>
    internal static class AudioKeyDropdown
    {
        public static void Draw(Rect position, SerializedProperty property, GUIContent label, string[] keys)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                // Misused on a non-string field — degrade to the normal field rather than hide it.
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var current = property.stringValue;
            var options = BuildOptions(keys, current, out var selectedIndex);

            EditorGUI.BeginProperty(position, label, property);
            var newIndex = EditorGUI.Popup(position, label, selectedIndex, options);
            if (newIndex != selectedIndex)
            {
                // Index 0 is the "(none)" entry → empty string.
                property.stringValue = newIndex == 0 ? string.Empty : StripMissingTag(options[newIndex]);
            }

            EditorGUI.EndProperty();
        }

        private static GUIContent[] BuildOptions(string[] keys, string current, out int selectedIndex)
        {
            var list = new System.Collections.Generic.List<GUIContent> { new("(none)") };
            selectedIndex = 0;

            foreach (var key in keys)
            {
                if (key == current) selectedIndex = list.Count;
                list.Add(new GUIContent(key));
            }

            // A non-empty value that no catalog defines: surface it instead of hiding it.
            if (!string.IsNullOrEmpty(current) && selectedIndex == 0)
            {
                selectedIndex = list.Count;
                list.Add(new GUIContent($"{current}  (missing)"));
            }

            return list.ToArray();
        }

        private static string StripMissingTag(GUIContent option)
        {
            const string suffix = "  (missing)";
            var text = option.text;
            return text.EndsWith(suffix) ? text.Substring(0, text.Length - suffix.Length) : text;
        }
    }
}
