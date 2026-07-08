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

        private sealed class LoopVoice
        {
            public AudioSource Source;
            public float BaseScale;
            public float FadeWeight = 1f;
            public string Key;
            public int FadeGen;
        }

        private readonly AudioService _owner;
        private readonly ObjectPool<AudioSource> _pool;
        private readonly ObjectPool<AudioSource> _loopPool;
        private readonly Dictionary<string, float> _lastPlayedAt = new();
        private readonly List<ActiveShot> _active = new();
        private readonly Dictionary<int, LoopVoice> _loops = new();
        private int _nextShotId;
        private int _nextLoopId = 1; // 0 is reserved: a default AudioHandle carries id 0 and must no-op

        public SfxPlayer(AudioService owner, ObjectPool<AudioSource> pool, ObjectPool<AudioSource> loopPool)
        {
            _owner = owner;
            _pool = pool;
            _loopPool = loopPool;
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

        // ---------- Looping SFX ----------

        public int PlayLoop(AudioCatalog.Entry entry, float extraScale)
        {
            if (!entry.HasDirectClips)
            {
                Debug.LogError(entry.AddressableClip != null && entry.AddressableClip.RuntimeKeyIsValid()
                    ? $"[AudioService] Sfx entry '{entry.Key}' only has an addressable clip — addressable SFX is not supported in this version."
                    : $"[AudioService] Sfx entry '{entry.Key}' has no clips.");
                return 0;
            }

            var clip = PickClip(entry.Clips);
            var baseScale = entry.VolumeScale * extraScale;
            var pitch = 1f + Random.Range(-entry.PitchVariance, entry.PitchVariance);
            return StartLoop(clip, baseScale, pitch, entry.Key);
        }

        public int PlayLoopDirect(AudioClip clip, float volumeScale, float pitch)
        {
            return StartLoop(clip, volumeScale, pitch, key: null);
        }

        public bool IsLoopPlaying(int id)
        {
            return id != 0 && _loops.TryGetValue(id, out var voice) && voice.Source && voice.Source.isPlaying;
        }

        public void StopLoop(int id)
        {
            if (id != 0 && _loops.TryGetValue(id, out var voice))
                RecycleLoop(id, voice);
        }

        public void FadeLoop(int id, float seconds)
        {
            if (id == 0 || !_loops.TryGetValue(id, out var voice)) return;

            if (seconds <= 0f)
            {
                RecycleLoop(id, voice);
                return;
            }

            voice.FadeGen++; // supersede any fade already running on this voice
            FadeLoopAsync(id, voice, voice.FadeGen, seconds);
        }

        private int StartLoop(AudioClip clip, float baseScale, float pitch, string key)
        {
            if (!clip) return 0;
            if (_owner.EffectiveSfxVolume <= 0f) return 0; // don't start a loop while the channel is silent

            var source = _loopPool.Get();
            source.clip = clip;
            source.pitch = Mathf.Max(0.01f, pitch);
            source.loop = true;
            source.volume = baseScale * _owner.EffectiveSfxVolume;
            source.Play();

            var id = _nextLoopId++;
            _loops[id] = new LoopVoice { Source = source, BaseScale = baseScale, FadeWeight = 1f, Key = key };
            return id;
        }

        private void RecycleLoop(int id, LoopVoice voice)
        {
            _loops.Remove(id);

            if (voice.Source)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.loop = false;
                _loopPool.Return(voice.Source);
            }
        }

        private async void FadeLoopAsync(int id, LoopVoice voice, int fadeGen, float seconds)
        {
            try
            {
                var startWeight = voice.FadeWeight;
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    await Awaitable.NextFrameAsync();
                    // Bail if disposed, recycled, or superseded by a newer fade / a Stop.
                    if (_owner.IsDisposed || !voice.Source) return;
                    if (!_loops.TryGetValue(id, out var current) || current != voice || voice.FadeGen != fadeGen) return;

                    elapsed += Time.unscaledDeltaTime;
                    voice.FadeWeight = Mathf.Lerp(startWeight, 0f, Mathf.Clamp01(elapsed / seconds));
                    voice.Source.volume = voice.BaseScale * voice.FadeWeight * _owner.EffectiveSfxVolume;
                }

                if (_loops.TryGetValue(id, out var still) && still == voice && voice.FadeGen == fadeGen)
                    RecycleLoop(id, voice);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // ---------- Stop / volume (both one-shots and loops) ----------

        public void StopByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Key == key) RecycleAt(i);
            }

            foreach (var id in LoopIdsWithKey(key))
            {
                if (_loops.TryGetValue(id, out var voice)) RecycleLoop(id, voice);
            }
        }

        public void StopAll()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
                RecycleAt(i);

            foreach (var id in new List<int>(_loops.Keys))
            {
                if (_loops.TryGetValue(id, out var voice)) RecycleLoop(id, voice);
            }
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

            foreach (var voice in _loops.Values)
            {
                if (voice.Source) voice.Source.volume = voice.BaseScale * voice.FadeWeight * effective;
            }
        }

        private List<int> LoopIdsWithKey(string key)
        {
            var ids = new List<int>();
            foreach (var pair in _loops)
            {
                if (pair.Value.Key == key) ids.Add(pair.Key);
            }

            return ids;
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
