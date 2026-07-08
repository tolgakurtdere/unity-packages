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
    /// addressable music) lives.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "TK/Audio Catalog")]
    public class AudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Key used by PlaySfx/PlayMusic (e.g. 'click', 'music_menu').")]
            public string key;

            public AudioChannel channel = AudioChannel.Sfx;

            [Tooltip("Direct clip references; one is picked at random when several are set (variations). Leave empty to use the addressable below.")]
            public AudioClip[] clips;

            [Tooltip("Addressable clip, used when no direct clips are set. Music-oriented: loaded on demand and released when no longer played. Addressable-only SFX entries are rejected in this version.")]
            public AssetReferenceT<AudioClip> addressableClip;

            [Min(0f)]
            [Tooltip("Per-entry gain, multiplied with the channel volume.")]
            public float volumeScale = 1f;

            [Range(0f, 0.5f)]
            [Tooltip("SFX pitch is sampled as 1 ± this value per shot.")]
            public float pitchVariance;

            [Min(0f)]
            [Tooltip("SFX played again within this window (unscaled seconds) are dropped — the same-frame spam guard.")]
            public float minRetriggerInterval = 0.05f;

            public bool HasDirectClips => clips != null && clips.Any(clip => clip);
        }

        [Serializable]
        public sealed class Playlist
        {
            [Tooltip("Key used by PlayPlaylist (e.g. 'menu').")]
            public string key;

            [Tooltip("Ordered entry keys of this catalog; every one must be a Music-channel entry.")]
            public string[] entryKeys;

            [Tooltip("Shuffle once each time the playlist starts.")]
            public bool shuffle;

            [Tooltip("Restart from the first track after the last one; off = stop at the end.")]
            public bool loop = true;
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField] private List<Playlist> playlists = new();

        public bool TryGetEntry(string key, out Entry entry)
        {
            if (!string.IsNullOrEmpty(key))
            {
                foreach (var candidate in entries)
                {
                    if (candidate != null && candidate.key == key)
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
                    if (candidate != null && candidate.key == key)
                    {
                        playlist = candidate;
                        return true;
                    }
                }
            }

            playlist = null;
            return false;
        }
    }
}
