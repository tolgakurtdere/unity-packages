using System;
using System.Collections.Generic;
using TK.Core.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TK.Audio
{
    /// <summary>
    /// Pooled one-shot SFX: variations (random clip pick), per-shot pitch variance, and a
    /// per-key retrigger throttle (same-frame spam guard). Disabled/muted channel spawns
    /// nothing; in-flight shots are volume-swept by ApplyVolumes so a PushMute silences them
    /// immediately. Shots are timeScale-independent (pause-menu clicks work) — only the
    /// return-to-pool timer runs on unscaled time.
    /// </summary>
    internal sealed class SfxPlayer
    {
        private readonly struct ActiveShot
        {
            public ActiveShot(AudioSource source, float baseScale)
            {
                Source = source;
                BaseScale = baseScale;
            }

            public AudioSource Source { get; }
            public float BaseScale { get; }
        }

        private readonly AudioService _owner;
        private readonly ObjectPool<AudioSource> _pool;
        private readonly Dictionary<string, float> _lastPlayedAt = new();
        private readonly List<ActiveShot> _active = new();

        public SfxPlayer(AudioService owner, ObjectPool<AudioSource> pool)
        {
            _owner = owner;
            _pool = pool;
        }

        public void Play(AudioCatalog.Entry entry, float extraScale)
        {
            if (!entry.HasDirectClips)
            {
                Debug.LogError(entry.addressableClip != null && entry.addressableClip.RuntimeKeyIsValid()
                    ? $"[AudioService] Sfx entry '{entry.key}' only has an addressable clip — addressable SFX is not supported in this version."
                    : $"[AudioService] Sfx entry '{entry.key}' has no clips.");
                return;
            }

            // Retrigger throttle: a key replayed inside its window is silently dropped.
            if (!string.IsNullOrEmpty(entry.key) && entry.minRetriggerInterval > 0f)
            {
                if (_lastPlayedAt.TryGetValue(entry.key, out var last)
                    && Time.unscaledTime - last < entry.minRetriggerInterval)
                {
                    return;
                }

                _lastPlayedAt[entry.key] = Time.unscaledTime;
            }

            var clip = PickClip(entry.clips);
            var pitch = 1f + Random.Range(-entry.pitchVariance, entry.pitchVariance);
            PlayResolved(clip, entry.volumeScale * extraScale, pitch);
        }

        public void PlayDirect(AudioClip clip, float volumeScale, float pitch)
        {
            PlayResolved(clip, volumeScale, pitch);
        }

        public void ApplyVolumes()
        {
            var effective = _owner.EffectiveSfxVolume;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var shot = _active[i];
                if (!shot.Source)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                shot.Source.volume = shot.BaseScale * effective;
            }
        }

        private void PlayResolved(AudioClip clip, float baseScale, float pitch)
        {
            if (!clip) return;
            if (_owner.EffectiveSfxVolume <= 0f) return; // don't spawn silent shots

            var source = _pool.Get();
            source.clip = clip;
            source.pitch = Mathf.Max(0.01f, pitch);
            source.loop = false;
            source.volume = baseScale * _owner.EffectiveSfxVolume;
            source.Play();

            _active.Add(new ActiveShot(source, baseScale));
            ReturnAfterAsync(source, clip.length / source.pitch);
        }

        private static AudioClip PickClip(IReadOnlyList<AudioClip> clips)
        {
            var start = Random.Range(0, clips.Count);
            for (var i = 0; i < clips.Count; i++)
            {
                var candidate = clips[(start + i) % clips.Count];
                if (candidate) return candidate;
            }

            return null;
        }

        private async void ReturnAfterAsync(AudioSource source, float seconds)
        {
            try
            {
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    await Awaitable.NextFrameAsync();
                    if (_owner.IsDisposed || !source) return; // host teardown destroys the sources

                    elapsed += Time.unscaledDeltaTime;
                }

                for (var i = _active.Count - 1; i >= 0; i--)
                {
                    if (_active[i].Source == source) _active.RemoveAt(i);
                }

                source.Stop();
                source.clip = null;
                _pool.Return(source);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
