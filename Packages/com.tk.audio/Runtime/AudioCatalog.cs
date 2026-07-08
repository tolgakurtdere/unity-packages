using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TK.Audio
{
    /// <summary>
    /// String-keyed audio content: entries (clips + per-sound tuning) and playlists built from
    /// entry keys. Optional — the direct-clip AudioService overloads work without any catalog;
    /// the catalog is where central tuning (variations, pitch variance, retrigger throttle,
    /// addressable music) lives. Read-only at runtime: authoring happens in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "TK/Audio Catalog")]
    public class AudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Key used by PlaySfx/PlayMusic (e.g. 'click', 'music_menu').")]
            [SerializeField] private string key;

            [SerializeField] private AudioChannel channel = AudioChannel.Sfx;

            [Tooltip("Direct clip references; one is picked at random per play when several are set (variations). When ANY is set, the addressable below is ignored.")]
            [SerializeField] private AudioClip[] clips;

            [Tooltip("Addressable clip, used ONLY when no direct clips are set. Music-oriented: loaded on demand and released when no longer played. Addressable-only SFX entries are rejected in this version.")]
            [SerializeField] private AssetReferenceT<AudioClip> addressableClip;

            [Min(0f)]
            [Tooltip("Per-entry gain, multiplied with the channel volume.")]
            [SerializeField] private float volumeScale = 1f;

            [Range(0f, 0.5f)]
            [Tooltip("SFX pitch is sampled as 1 ± this value per shot.")]
            [SerializeField] private float pitchVariance;

            [Min(0f)]
            [Tooltip("SFX played again within this window (unscaled seconds) are dropped — the same-frame spam guard.")]
            [SerializeField] private float minRetriggerInterval = 0.05f;

            [Min(0)]
            [Tooltip("Max simultaneous voices of this key (0 = unlimited). When at the cap, the oldest voice is culled before the new one plays.")]
            [SerializeField] private int maxConcurrentVoices;

            public string Key => key;
            public AudioChannel Channel => channel;
            public IReadOnlyList<AudioClip> Clips => clips;

            /// <summary>
            /// Deliberately a SINGLE reference while <see cref="Clips"/> is an array: variations
            /// are a direct-clip feature (clips are resident and cheap to pick from), whereas an
            /// addressable is music-oriented — each one is a separate on-demand load with its own
            /// release bookkeeping, and music rarely wants variations.
            /// </summary>
            public AssetReferenceT<AudioClip> AddressableClip => addressableClip;

            public float VolumeScale => volumeScale;
            public float PitchVariance => pitchVariance;
            public float MinRetriggerInterval => minRetriggerInterval;
            public int MaxConcurrentVoices => maxConcurrentVoices;

            public bool HasDirectClips => clips != null && clips.Any(clip => clip);

            internal bool HasBothSources =>
                HasDirectClips && addressableClip != null && addressableClip.RuntimeKeyIsValid();
        }

        [Serializable]
        public sealed class Playlist
        {
            [Tooltip("Key used by PlayPlaylist (e.g. 'menu').")]
            [SerializeField] private string key;

            [Tooltip("Ordered entry keys of this catalog; every one must be a Music-channel entry.")]
            [SerializeField] private string[] entryKeys;

            [Tooltip("Shuffle once each time the playlist starts.")]
            [SerializeField] private bool shuffle;

            [Tooltip("Restart from the first track after the last one; off = stop at the end.")]
            [SerializeField] private bool loop = true;

            public string Key => key;
            public IReadOnlyList<string> EntryKeys => entryKeys;
            public bool Shuffle => shuffle;
            public bool Loop => loop;
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField] private List<Playlist> playlists = new();

        public bool TryGetEntry(string key, out Entry entry)
        {
            if (!string.IsNullOrEmpty(key))
            {
                foreach (var candidate in entries)
                {
                    if (candidate != null && candidate.Key == key)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public bool TryGetPlaylist(string key, out Playlist playlist)
        {
            if (!string.IsNullOrEmpty(key))
            {
                foreach (var candidate in playlists)
                {
                    if (candidate != null && candidate.Key == key)
                    {
                        playlist = candidate;
                        return true;
                    }
                }
            }

            playlist = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Authoring guard: with both sources set, direct clips silently win — surface that
            // instead of letting "why isn't my addressable playing" happen at runtime.
            foreach (var entry in entries)
            {
                if (entry != null && entry.HasBothSources)
                {
                    Debug.LogWarning($"[AudioCatalog] {name}: entry '{entry.Key}' has BOTH direct clips and an " +
                                     "addressable — direct clips win and the addressable is ignored.", this);
                }
            }
        }
#endif
    }
}
