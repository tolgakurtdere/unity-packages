using System;
using TK.Core.Save;
using TK.Core.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TK.Audio
{
    /// <summary>
    /// Audio engine: two channels (Music/Sfx) with optional save-backed settings, a ref-counted
    /// temporary mute on top of them (the ads seam), pooled one-shot SFX, and a crossfading
    /// music player with playlists. Construct once at your composition root, register it in your
    /// service container, and optionally expose it statically via <see cref="Audio.Bind"/>.
    /// Owns a hidden host GameObject with its AudioSources; call <see cref="Dispose"/> on teardown.
    /// </summary>
    public sealed class AudioService : IDisposable
    {
        private const string SaveKey = "tk_audio_settings";

        [Serializable]
        internal sealed class AudioSettingsData
        {
            public bool Music = true;
            public bool Sfx = true;
            public float MusicVolume = 1f;
            public float SfxVolume = 1f;
        }

        private readonly AudioCatalog _catalog;
        private readonly ISaveSystem _saveSystem;
        private readonly AudioSettingsData _data;
        private readonly RefCountLock _muteSuppressions = new();
        private readonly MusicPlayer _music;
        private readonly SfxPlayer _sfx;

        private GameObject _host;
        private bool _disposed;
        private bool _warnedNoCatalog;

        public AudioService(AudioCatalog catalog = null, ISaveSystem saveSystem = null, int sfxPoolSize = 8)
        {
            _catalog = catalog;
            _saveSystem = saveSystem;
            _data = saveSystem != null ? saveSystem.Load(SaveKey, new AudioSettingsData()) : new AudioSettingsData();

            _host = new GameObject("[TK.Audio]");
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(_host);

            var musicA = CreateSource("Music A");
            var musicB = CreateSource("Music B");
            var sfxTemplate = CreateSource("Sfx Template");
            sfxTemplate.gameObject.SetActive(false);

            var pool = new ObjectPool<AudioSource>(sfxTemplate, _host.transform, Mathf.Max(1, sfxPoolSize));
            _music = new MusicPlayer(this, musicA, musicB);
            _sfx = new SfxPlayer(this, pool);
        }

        // ---------- Settings (durable; the package only writes them through these setters) ----------

        public bool MusicEnabled
        {
            get => _data.Music;
            set => SetSetting(ref _data.Music, value);
        }

        public bool SfxEnabled
        {
            get => _data.Sfx;
            set => SetSetting(ref _data.Sfx, value);
        }

        public float MusicVolume
        {
            get => _data.MusicVolume;
            set => SetSetting(ref _data.MusicVolume, Mathf.Clamp01(value));
        }

        public float SfxVolume
        {
            get => _data.SfxVolume;
            set => SetSetting(ref _data.SfxVolume, Mathf.Clamp01(value));
        }

        /// <summary>Seconds used to crossfade between music tracks (and to fade out on stop). 0 = hard cut.</summary>
        public float MusicCrossfadeSeconds { get; set; } = 0.5f;

        // ---------- Temporary mute (never touches the settings above) ----------

        /// <summary>True while any temporary suppression is held (the user settings are unaffected).</summary>
        public bool IsMuted => _muteSuppressions.IsLocked;

        /// <summary>
        /// Temporarily silences BOTH channels without touching the durable settings. Ref-counted —
        /// pair every push with one <see cref="PopMute"/>. Wire ads with one line of glue:
        /// <c>options.AudioMuteSetter = m => { if (m) audio.PushMute(); else audio.PopMute(); };</c>
        /// </summary>
        public void PushMute()
        {
            _muteSuppressions.Lock();
            ApplyVolumes();
        }

        /// <summary>Releases one suppression acquired by <see cref="PushMute"/>. Throws when unbalanced.</summary>
        public void PopMute()
        {
            _muteSuppressions.Unlock();
            ApplyVolumes();
        }

        /// <summary>Music gain after settings and mute: <c>MusicEnabled &amp;&amp; !IsMuted ? MusicVolume : 0</c>.</summary>
        public float EffectiveMusicVolume => _data.Music && !IsMuted ? _data.MusicVolume : 0f;

        /// <summary>Sfx gain after settings and mute: <c>SfxEnabled &amp;&amp; !IsMuted ? SfxVolume : 0</c>.</summary>
        public float EffectiveSfxVolume => _data.Sfx && !IsMuted ? _data.SfxVolume : 0f;

        // ---------- Music ----------

        /// <summary>Entry key of the current music track; null for direct clips or when nothing plays.</summary>
        public string ActiveMusicKey => _music.ActiveKey;

        /// <summary>Key of the running playlist; null in single-track mode or when nothing plays.</summary>
        public string ActivePlaylistKey => _music.ActivePlaylistKey;

        /// <summary>Plays a catalog music entry (crossfading from the current track). Idempotent while the same key is active.</summary>
        public void PlayMusic(string key, bool loop = true)
        {
            if (_disposed || !TryResolveEntry(key, out var entry)) return;
            if (entry.Channel != AudioChannel.Music)
                Debug.LogWarning($"[AudioService] Entry '{key}' is not a Music entry — playing it as music anyway.");

            _music.PlayEntry(entry, loop);
        }

        /// <summary>Plays a clip directly as music (no catalog needed). Idempotent while the same clip is active.</summary>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (_disposed || !clip) return;
            _music.PlayClip(clip, loop);
        }

        /// <summary>Starts a catalog playlist (shuffled once per start when configured). Idempotent while the same playlist runs.</summary>
        public void PlayPlaylist(string key)
        {
            if (_disposed || _catalog == null)
            {
                WarnNoCatalog(key);
                return;
            }

            if (!_catalog.TryGetPlaylist(key, out var playlist))
            {
                Debug.LogError($"[AudioService] Unknown playlist key '{key}'.");
                return;
            }

            _music.PlayPlaylist(key, playlist);
        }

        /// <summary>Fades the current music out and clears the active track/playlist.</summary>
        public void StopMusic()
        {
            if (_disposed) return;
            _music.Stop();
        }

        // ---------- Sfx ----------

        /// <summary>Plays a catalog SFX entry through the pool (variations, pitch variance, retrigger throttle, voice cap).</summary>
        public void PlaySfx(string key, float volumeScale = 1f)
        {
            PlaySfx(key, volumeScale, 0f);
        }

        /// <summary>Plays a catalog SFX entry after an unscaled delay (the throttle is evaluated now, not after the delay).</summary>
        public void PlaySfx(string key, float volumeScale, float delaySeconds)
        {
            if (_disposed || !TryResolveEntry(key, out var entry)) return;
            if (entry.Channel != AudioChannel.Sfx)
                Debug.LogWarning($"[AudioService] Entry '{key}' is not an Sfx entry — playing it as a one-shot anyway.");

            _sfx.Play(entry, volumeScale, delaySeconds);
        }

        /// <summary>Plays a clip directly (no catalog tuning, no retrigger throttle, no voice cap — documented).</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (_disposed || !clip) return;
            _sfx.PlayDirect(clip, volumeScale, pitch);
        }

        /// <summary>Stops every active one-shot (and looping SFX) started from this key.</summary>
        public void StopSfx(string key)
        {
            if (_disposed) return;
            _sfx.StopByKey(key);
        }

        /// <summary>Stops all active SFX (one-shots and loops); does not touch music.</summary>
        public void StopAllSfx()
        {
            if (_disposed) return;
            _sfx.StopAll();
        }

        // ---------- Lifecycle / internals ----------

        /// <summary>The hidden host all AudioSources live under (advanced/debugging use).</summary>
        public Transform Host => _host ? _host.transform : null;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _music.DisposeNow();

            if (_host)
            {
                if (Application.isPlaying) Object.Destroy(_host);
                else Object.DestroyImmediate(_host);
            }

            _host = null;
        }

        internal bool IsDisposed => _disposed;
        internal bool HostAlive => _host;
        internal AudioCatalog Catalog => _catalog;

        internal void ApplyVolumes()
        {
            _music.ApplyVolumes();
            _sfx.ApplyVolumes();
        }

        internal bool TryResolveEntry(string key, out AudioCatalog.Entry entry)
        {
            entry = null;
            if (_catalog == null)
            {
                WarnNoCatalog(key);
                return false;
            }

            if (!_catalog.TryGetEntry(key, out entry))
            {
                Debug.LogError($"[AudioService] Unknown audio key '{key}'.");
                return false;
            }

            return true;
        }

        private void WarnNoCatalog(string key)
        {
            if (_disposed || _warnedNoCatalog) return;
            _warnedNoCatalog = true;
            Debug.LogError($"[AudioService] No AudioCatalog was provided — string-key call '{key}' ignored (use the direct-clip overloads, or construct the service with a catalog).");
        }

        private void SetSetting<T>(ref T field, T value) where T : IEquatable<T>
        {
            if (field.Equals(value)) return;

            field = value;
            _saveSystem?.Save(SaveKey, _data);
            ApplyVolumes();
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_host.transform, worldPositionStays: false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
