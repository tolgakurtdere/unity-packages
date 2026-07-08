using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TK.Audio.Editor
{
    /// <summary>
    /// Adds a drag-and-drop area to the AudioCatalog inspector: drop AudioClips to append Sfx
    /// entries (name → key, existing keys skipped, sane defaults set). A drop area — not a
    /// "use current selection" button — because selecting clips in the Project would deselect
    /// the catalog and hide its inspector.
    /// </summary>
    [CustomEditor(typeof(AudioCatalog))]
    public sealed class AudioCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            var dropRect = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop AudioClips here to add Sfx entries", EditorStyles.helpBox);
            HandleDrop(dropRect);
        }

        private void HandleDrop(Rect rect)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!rect.Contains(evt.mousePosition)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform)
            {
                evt.Use();
                return;
            }

            DragAndDrop.AcceptDrag();
            var clips = new List<AudioClip>();
            foreach (var reference in DragAndDrop.objectReferences)
            {
                if (reference is AudioClip clip) clips.Add(clip);
            }

            if (clips.Count > 0)
            {
                Undo.RecordObject(target, "Add Audio Entries");
                var added = AudioCatalogPopulator.AppendEntries((AudioCatalog)target, clips, AudioChannel.Sfx);
                Debug.Log($"[AudioCatalog] Added {added} new entr{(added == 1 ? "y" : "ies")} from {clips.Count} clip(s).", target);
            }

            evt.Use();
        }
    }
}
