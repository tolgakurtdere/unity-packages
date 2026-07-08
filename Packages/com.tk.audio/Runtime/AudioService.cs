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
        private bool _musicManuallyPaused;
        private float _musicFade = 1f; // transient FadeChannelVolume multiplier (not persisted)
        private float _sfxFade = 1f;
        private int _musicFadeGen;
        private int _sfxFadeGen;

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
            var loopTemplate = CreateSource("Sfx Loop");
            loopTemplate.gameObject.SetActive(false);

            var pool = new ObjectPool<AudioSource>(sfxTemplate, _host.transform, Mathf.Max(1, sfxPoolSize));
            // Loops live outside the one-shot pool: their lifetime is unbounded (until Stop), which
            // would break the pool's auto-return contract; a dedicated pool grows on demand.
            var loopPool = new ObjectPool<AudioSource>(loopTemplate, _host.transform);
            _music = new MusicPlayer(this, musicA, musicB);
            _sfx = new SfxPlayer(this, pool, loopPool);
        }

        // ---------- Settings (durable; the package only writes them through these setters) ----------

        /// <summary>Raised after any durable setting (enabled/volume) changes — bind a settings slider to it. Not raised by <see cref="FadeChannelVolume"/> (that's transient).</summary>
        public event Action Changed;

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

        // ---------- Temporary mute + music pause (never touch the durable settings) ----------

        /// <summary>True while any ad-style suppression is held (<see cref="PushMute"/>).</summary>
        public bool IsMuted => _muteSuppressions.IsLocked;

        /// <summary>
        /// True when music playback is frozen — by an ad mute (<see cref="PushMute"/>) or an
        /// explicit <see cref="PauseMusic"/>. Music resumes exactly where it was once BOTH clear.
        /// </summary>
        public bool IsMusicPaused => IsMuted || _musicManuallyPaused;

        /// <summary>
        /// Temporarily silences SFX and PAUSES music (position kept) without touching the durable
        /// settings. Ref-counted — pair every push with one <see cref="PopMute"/>. Ads glue:
        /// <c>options.AudioMuteSetter = m => { if (m) audio.PushMute(); else audio.PopMute(); };</c>
        /// Music one-shots have nothing to freeze, so SFX is volume-gated + not spawned instead.
        /// </summary>
        public void PushMute()
        {
            _muteSuppressions.Lock();
            OnMuteOrPauseChanged();
        }

        /// <summary>Releases one suppression acquired by <see cref="PushMute"/>. Throws when unbalanced.</summary>
        public void PopMute()
        {
            _muteSuppressions.Unlock();
            OnMuteOrPauseChanged();
        }

        /// <summary>Freezes music (position kept) until <see cref="ResumeMusic"/> — app-pause / phone-call. Composes with ad mute.</summary>
        public void PauseMusic()
        {
            if (_musicManuallyPaused) return;
            _musicManuallyPaused = true;
            OnMuteOrPauseChanged();
        }

        /// <summary>Resumes music paused by <see cref="PauseMusic"/> (stays paused if an ad mute still holds).</summary>
        public void ResumeMusic()
        {
            if (!_musicManuallyPaused) return;
            _musicManuallyPaused = false;
            OnMuteOrPauseChanged();
        }

        /// <summary>
        /// Volume a music source plays AT when it is playing (per-slot fade/scale on top). Gating
        /// is done by pause (ad/manual) and stop (settings) — not by this value.
        /// </summary>
        internal float MusicPlayVolume => _data.Music ? _data.MusicVolume * _musicFade : 0f;

        /// <summary>Audible music level right now: 0 while paused, else <see cref="MusicPlayVolume"/>.</summary>
        public float EffectiveMusicVolume => IsMusicPaused ? 0f : MusicPlayVolume;

        /// <summary>Sfx gain after settings, mute, and any channel fade: <c>(SfxEnabled &amp;&amp; !IsMuted ? SfxVolume : 0) × fade</c>.</summary>
        public float EffectiveSfxVolume => (_data.Sfx && !IsMuted ? _data.SfxVolume : 0f) * _sfxFade;

        private void OnMuteOrPauseChanged()
        {
            _music.SetPaused(IsMusicPaused);
            _sfx.ApplyVolumes(); // SFX mute is a volume-gate; music mute is the pause above
        }

        /// <summary>
        /// Smoothly fades a channel's volume toward <paramref name="target"/> (0..1) over
        /// <paramref name="seconds"/> unscaled seconds (0 = instant) — cutscene ducking / pause.
        /// A TRANSIENT multiplier: it does not change or persist the user's <see cref="MusicVolume"/>/
        /// <see cref="SfxVolume"/> setting and does not raise <see cref="Changed"/>. Restore with a
        /// fade back to 1. The latest fade on a channel supersedes any earlier one.
        /// </summary>
        public void FadeChannelVolume(AudioChannel channel, float target, float seconds)
        {
            if (_disposed) return;

            target = Mathf.Clamp01(target);
            var generation = channel == AudioChannel.Music ? ++_musicFadeGen : ++_sfxFadeGen;

            if (seconds <= 0f)
            {
                SetChannelFade(channel, target);
                return;
            }

            FadeChannelAsync(channel, target, seconds, generation);
        }

        private async void FadeChannelAsync(AudioChannel channel, float target, float seconds, int generation)
        {
            try
            {
                var start = channel == AudioChannel.Music ? _musicFade : _sfxFade;
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    await Awaitable.NextFrameAsync();
                    if (_disposed || generation != CurrentFadeGeneration(channel)) return;

                    elapsed += Time.unscaledDeltaTime;
                    SetChannelFade(channel, Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds)));
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private int CurrentFadeGeneration(AudioChannel channel) =>
            channel == AudioChannel.Music ? _musicFadeGen : _sfxFadeGen;

        private void SetChannelFade(AudioChannel channel, float value)
        {
            if (channel == AudioChannel.Music) _musicFade = value;
            else _sfxFade = value;
            ApplyVolumes();
        }

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
            if (_disposed || !_catalog)
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

        /// <summary>
        /// Starts a looping catalog SFX (ambient/engine sounds) and returns a handle to stop or
        /// fade it. Returns a no-op handle when the Sfx channel is currently silent.
        /// </summary>
        public AudioHandle PlaySfxLoop(string key, float volumeScale = 1f)
        {
            if (_disposed || !TryResolveEntry(key, out var entry)) return default;
            if (entry.Channel != AudioChannel.Sfx)
                Debug.LogWarning($"[AudioService] Entry '{key}' is not an Sfx entry — looping it anyway.");

            return new AudioHandle(_sfx, _sfx.PlayLoop(entry, volumeScale));
        }

        /// <summary>Starts a looping clip directly (no catalog tuning). Returns a handle to stop or fade it.</summary>
        public AudioHandle PlaySfxLoop(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (_disposed || !clip) return default;
            return new AudioHandle(_sfx, _sfx.PlayLoopDirect(clip, volumeScale, pitch));
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
            if (!_catalog)
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
            Changed?.Invoke();
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
