using System;
using System.Collections.Generic;
using TK.Core.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TK.Audio
{
    /// <summary>
    /// Pooled one-shot SFX: variations (random clip pick), per-shot pitch variance, a per-key
    /// retrigger throttle (same-frame spam guard), an optional per-key concurrent-voice cap, and
    /// an optional play delay. Disabled/muted channel spawns nothing; in-flight shots are
    /// volume-swept by ApplyVolumes so a PushMute silences them immediately. Shots are
    /// timeScale-independent (pause-menu clicks work) — only the return-to-pool timer runs on
    /// unscaled time.
    /// </summary>
    internal sealed class SfxPlayer
    {
        private readonly struct ActiveShot
        {
            public ActiveShot(int id, AudioSource source, float baseScale, string key)
            {
                Id = id;
                Source = source;
                BaseScale = baseScale;
                Key = key;
            }

            // A unique per-spawn id: the return timer captures it, so a source that was recycled
            // early (cull / StopByKey) and handed out again for a NEW shot is not mistaken for the
            // old one by the stale timer (the pool reuses source objects — an ABA hazard).
            public int Id { get; }
            public AudioSource Source { get; }
            public float BaseScale { get; }
            public string Key { get; }
        }

        private readonly AudioService _owner;
        private readonly ObjectPool<AudioSource> _pool;
        private readonly Dictionary<string, float> _lastPlayedAt = new();
        private readonly List<ActiveShot> _active = new();
        private int _nextShotId;

        public SfxPlayer(AudioService owner, ObjectPool<AudioSource> pool)
        {
            _owner = owner;
            _pool = pool;
        }

        public void Play(AudioCatalog.Entry entry, float extraScale, float delaySeconds = 0f)
        {
            if (!entry.HasDirectClips)
            {
                Debug.LogError(entry.AddressableClip != null && entry.AddressableClip.RuntimeKeyIsValid()
                    ? $"[AudioService] Sfx entry '{entry.Key}' only has an addressable clip — addressable SFX is not supported in this version."
                    : $"[AudioService] Sfx entry '{entry.Key}' has no clips.");
                return;
            }

            // Retrigger throttle: a key replayed inside its window is silently dropped. Stamped at
            // call time — the delay below schedules the spawn, it does not re-open the window.
            if (!string.IsNullOrEmpty(entry.Key) && entry.MinRetriggerInterval > 0f)
            {
                if (_lastPlayedAt.TryGetValue(entry.Key, out var last)
                    && Time.unscaledTime - last < entry.MinRetriggerInterval)
                {
                    return;
                }

                _lastPlayedAt[entry.Key] = Time.unscaledTime;
            }

            var clip = PickClip(entry.Clips);
            var baseScale = entry.VolumeScale * extraScale;
            var pitch = 1f + Random.Range(-entry.PitchVariance, entry.PitchVariance);

            if (delaySeconds > 0f)
                DelayedSpawnAsync(clip, baseScale, pitch, entry.Key, entry.MaxConcurrentVoices, delaySeconds);
            else
                Spawn(clip, baseScale, pitch, entry.Key, entry.MaxConcurrentVoices);
        }

        public void PlayDirect(AudioClip clip, float volumeScale, float pitch)
        {
            // Direct clips carry no key: no throttle, no voice cap (documented).
            Spawn(clip, volumeScale, pitch, key: null, maxVoices: 0);
        }

        public void StopByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Key == key) RecycleAt(i);
            }
        }

        public void StopAll()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
                RecycleAt(i);
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

        private async void DelayedSpawnAsync(AudioClip clip, float baseScale, float pitch, string key, int maxVoices,
            float delaySeconds)
        {
            try
            {
                var elapsed = 0f;
                while (elapsed < delaySeconds)
                {
                    await Awaitable.NextFrameAsync();
                    if (_owner.IsDisposed) return;

                    elapsed += Time.unscaledDeltaTime;
                }

                Spawn(clip, baseScale, pitch, key, maxVoices);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void Spawn(AudioClip clip, float baseScale, float pitch, string key, int maxVoices)
        {
            if (!clip) return;
            if (_owner.EffectiveSfxVolume <= 0f) return; // don't spawn silent shots

            if (maxVoices > 0 && !string.IsNullOrEmpty(key))
                CullOldestOverCap(key, maxVoices);

            var source = _pool.Get();
            source.clip = clip;
            source.pitch = Mathf.Max(0.01f, pitch);
            source.loop = false;
            source.volume = baseScale * _owner.EffectiveSfxVolume;
            source.Play();

            var id = _nextShotId++;
            _active.Add(new ActiveShot(id, source, baseScale, key));
            ReturnAfterAsync(id, source, clip.length / source.pitch);
        }

        // Makes room so that AFTER this returns there are at most maxVoices-1 live voices of the key
        // (the caller then adds one). Culls oldest-first — the first matching shot in append order.
        private void CullOldestOverCap(string key, int maxVoices)
        {
            var count = 0;
            foreach (var shot in _active)
            {
                if (shot.Key == key) count++;
            }

            while (count >= maxVoices)
            {
                for (var i = 0; i < _active.Count; i++)
                {
                    if (_active[i].Key == key)
                    {
                        RecycleAt(i);
                        count--;
                        break;
                    }
                }
            }
        }

        private void RecycleAt(int index)
        {
            var shot = _active[index];
            _active.RemoveAt(index);

            if (shot.Source)
            {
                shot.Source.Stop();
                shot.Source.clip = null;
                _pool.Return(shot.Source);
            }
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

        private async void ReturnAfterAsync(int id, AudioSource source, float seconds)
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

                // Only recycle if THIS shot is still active. A cull / StopByKey already recycled it
                // (and the id guards against the source having been reused by a later shot).
                for (var i = 0; i < _active.Count; i++)
                {
                    if (_active[i].Id == id)
                    {
                        RecycleAt(i);
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
