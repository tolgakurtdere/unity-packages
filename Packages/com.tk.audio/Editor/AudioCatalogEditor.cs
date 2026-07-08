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
        // Alpha-tinted so both editor themes read well: a blue accent at rest, a green
        // "will accept" highlight while AudioClips are dragged over the area.
        private static readonly Color IdleFill = new(0.26f, 0.47f, 0.63f, 0.22f);
        private static readonly Color IdleBorder = new(0.40f, 0.60f, 0.75f, 0.70f);
        private static readonly Color HotFill = new(0.30f, 0.62f, 0.38f, 0.45f);
        private static readonly Color HotBorder = new(0.44f, 0.82f, 0.52f, 0.95f);

        private GUIStyle _labelStyle;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            var dropRect = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            var hot = IsAcceptableDragOver(dropRect);
            DrawDropArea(dropRect, hot);
            HandleDrop(dropRect);
        }

        private static bool IsAcceptableDragOver(Rect rect)
        {
            // A drag is in progress (objectReferences populated) and the cursor is over the area
            // carrying at least one AudioClip.
            return DragAndDrop.objectReferences.Length > 0
                   && rect.Contains(Event.current.mousePosition)
                   && ContainsAudioClip(DragAndDrop.objectReferences);
        }

        private void DrawDropArea(Rect rect, bool hot)
        {
            EditorGUI.DrawRect(rect, hot ? HotFill : IdleFill);
            DrawBorder(rect, hot ? HotBorder : IdleBorder);

            _labelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _labelStyle.normal.textColor = hot ? new Color(0.85f, 1f, 0.88f) : new Color(0.78f, 0.85f, 0.92f);

            GUI.Label(rect, hot ? "Release to add Sfx entries" : "Drop AudioClips here to add Sfx entries", _labelStyle);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            const float t = 1f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), color);                     // top
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);              // bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), color);                    // left
            EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);             // right
        }

        private static bool ContainsAudioClip(Object[] references)
        {
            foreach (var reference in references)
            {
                if (reference is AudioClip) return true;
            }

            return false;
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
