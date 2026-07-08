using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TK.Audio.Editor
{
    /// <summary>
    /// Builds catalog entries from AudioClips. <see cref="NewKeysFor"/> is pure (clip name → key,
    /// deduped against existing + within the selection) and unit-tested; <see cref="AppendEntries"/>
    /// writes them, setting sane defaults so an auto-added entry is never the zeroed-fresh trap
    /// (volumeScale 0 = silent).
    /// </summary>
    public static class AudioCatalogPopulator
    {
        public static List<string> NewKeysFor(IEnumerable<AudioClip> clips, ICollection<string> existingKeys)
        {
            var result = new List<string>();
            var seen = existingKeys != null
                ? new HashSet<string>(existingKeys, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            if (clips == null) return result;

            foreach (var clip in clips)
            {
                if (!clip) continue;

                var key = clip.name;
                if (string.IsNullOrEmpty(key)) continue;
                if (seen.Add(key)) result.Add(key); // false = already present (existing or earlier in selection)
            }

            return result;
        }

        /// <summary>Appends one entry per not-yet-present clip name; returns the number added.</summary>
        public static int AppendEntries(AudioCatalog catalog, IEnumerable<AudioClip> clips, AudioChannel channel)
        {
            if (!catalog) return 0;

            var clipList = new List<AudioClip>();
            if (clips != null)
            {
                foreach (var clip in clips)
                {
                    if (clip) clipList.Add(clip);
                }
            }

            var existing = new List<string>(catalog.EntryKeys());
            var newKeys = NewKeysFor(clipList, existing);
            if (newKeys.Count == 0) return 0;

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            foreach (var key in newKeys)
            {
                var clip = clipList.Find(candidate => candidate.name == key);
                var index = entries.arraySize;
                entries.arraySize = index + 1;

                var entry = entries.GetArrayElementAtIndex(index);
                // Grown elements are zeroed — set the defaults the field initializers would have.
                entry.FindPropertyRelative("key").stringValue = key;
                entry.FindPropertyRelative("channel").enumValueIndex = (int)channel;
                entry.FindPropertyRelative("volumeScale").floatValue = 1f;
                entry.FindPropertyRelative("minRetriggerInterval").floatValue = 0.05f;

                var clipsProp = entry.FindPropertyRelative("clips");
                clipsProp.arraySize = 1;
                clipsProp.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            }

            so.ApplyModifiedProperties();
            return newKeys.Count;
        }
    }
}
