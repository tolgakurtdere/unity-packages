using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Random = UnityEngine.Random;

namespace TK.Audio
{
    /// <summary>
    /// Two-source crossfading music engine with playlists. Every async flow (addressable load,
    /// crossfade, end-of-track monitor) is guarded by a generation counter — the latest request
    /// wins, superseded flows exit at their next await. While music is paused (ad mute or
    /// PauseMusic, applied via <see cref="SetPaused"/>) the sources are AudioSource.Pause()d and
    /// the elapsed-based fade loops freeze, so playback resumes exactly where it was; the
    /// position-based end monitor is naturally frozen (a paused source's time doesn't advance).
    /// The service owns the enabled/stop and requested-replay policy — this player just plays,
    /// crossfades, pauses, and stops what it's told. With two slots, a retarget during an active
    /// crossfade hard-cuts the oldest tail — accepted trade-off.
    /// </summary>
    internal sealed class MusicPlayer
    {
        private sealed class Slot
        {
            public AudioSource Source;
            public AsyncOperationHandle<AudioClip>? Handle;
            public float FadeWeight;
            public float EntryScale = 1f;
        }

        private readonly struct TrackRequest
        {
            public TrackRequest(string key, AudioClip clip, AssetReferenceT<AudioClip> addressable,
                float volumeScale, bool loopTrack)
            {
                Key = key;
                Clip = clip;
                Addressable = addressable;
                VolumeScale = volumeScale;
                LoopTrack = loopTrack;
            }

            public string Key { get; }
            public AudioClip Clip { get; }
            public AssetReferenceT<AudioClip> Addressable { get; }
            public float VolumeScale { get; }
            public bool LoopTrack { get; }
        }

        private readonly AudioService _owner;
        private readonly Slot[] _slots;
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _preloaded = new();
        private int _activeIndex = -1;
        private int _generation;

        private List<string> _playlistOrder;
        private int _playlistIndex;
        private bool _playlistLoop;
        private int _consecutiveTrackFailures;

        public string ActiveKey { get; private set; }
        public string ActivePlaylistKey { get; private set; }

        public MusicPlayer(AudioService owner, AudioSource a, AudioSource b)
        {
            _owner = owner;
            _slots = new[] { new Slot { Source = a }, new Slot { Source = b } };
        }

        public void PlayEntry(AudioCatalog.Entry entry, bool loop)
        {
            if (ActivePlaylistKey == null && ActiveKey == entry.Key && HasActiveClip) return; // idempotent

            ClearPlaylist();
            StartTrack(BuildRequest(entry, loop));
        }

        public void PlayClip(AudioClip clip, bool loop)
        {
            if (ActivePlaylistKey == null && ActiveKey == null && HasActiveClip
                && _slots[_activeIndex].Source.clip == clip) return; // idempotent

            ClearPlaylist();
            StartTrack(new TrackRequest(null, clip, null, 1f, loop));
        }

        public void PlayPlaylist(string key, AudioCatalog.Playlist playlist)
        {
            if (ActivePlaylistKey == key) return; // idempotent while running

            var order = BuildPlaylistOrder(playlist);
            if (order.Count == 0)
            {
                Debug.LogError($"[AudioService] Playlist '{key}' has no playable Music entries.");
                return;
            }

            ActivePlaylistKey = key;
            _playlistOrder = order;
            _playlistLoop = playlist.Loop;
            _playlistIndex = 0;
            _consecutiveTrackFailures = 0;
            StartPlaylistTrack();
        }

        public void Stop()
        {
            ClearPlaylist();
            ActiveKey = null;
            FadeOutAndStopAsync(++_generation);
        }

        public void ApplyVolumes()
        {
            // Pause/stop do the mute gating; the source volume is just the playing level.
            var effective = _owner.MusicPlayVolume;
            foreach (var slot in _slots)
            {
                if (slot.Source) slot.Source.volume = slot.FadeWeight * slot.EntryScale * effective;
            }
        }

        /// <summary>Pauses (position kept) or resumes both slot sources. The volume is unchanged.</summary>
        public void SetPaused(bool paused)
        {
            foreach (var slot in _slots)
            {
                if (!slot.Source || !slot.Source.clip) continue;
                if (paused) slot.Source.Pause();
                else slot.Source.UnPause();
            }
        }

        public void DisposeNow()
        {
            _generation++;
            foreach (var slot in _slots) ReleaseSlot(slot);
            foreach (var handle in _preloaded.Values) Addressables.Release(handle);
            _preloaded.Clear();
        }

        /// <summary>
        /// Warms an addressable music entry so a later play reuses the resident clip (no first-play
        /// hitch). No-op for direct-clip entries (already resident) and idempotent per key. The
        /// cached handle is held until Dispose.
        /// </summary>
        public async Awaitable PreloadAsync(AudioCatalog.Entry entry)
        {
            if (entry.HasDirectClips) return; // direct clips are already resident
            if (entry.AddressableClip == null || !entry.AddressableClip.RuntimeKeyIsValid()) return;
            if (_preloaded.ContainsKey(entry.Key)) return; // already warm

            var load = entry.AddressableClip.LoadAssetAsync<AudioClip>();
            await load.Task;

            if (_owner.IsDisposed)
            {
                Addressables.Release(load);
                return;
            }

            if (load.Status != AsyncOperationStatus.Succeeded || !load.Result)
            {
                Debug.LogError($"[AudioService] Preload of addressable music '{entry.Key}' failed.");
                Addressables.Release(load);
                return;
            }

            if (_preloaded.ContainsKey(entry.Key))
            {
                Addressables.Release(load); // a concurrent preload of the same key won the race
                return;
            }

            _preloaded[entry.Key] = load;
        }

        // ---------- internals ----------

        private bool HasActiveClip =>
            _activeIndex >= 0 && _slots[_activeIndex].Source && _slots[_activeIndex].Source.clip;

        private bool IsCurrent(int generation) => generation == _generation && !_owner.IsDisposed && _owner.HostAlive;

        private static TrackRequest BuildRequest(AudioCatalog.Entry entry, bool loopTrack)
        {
            AudioClip direct = null;
            if (entry.HasDirectClips)
            {
                // Variation-lite: random pick among the assigned clips.
                var clips = entry.Clips;
                var start = Random.Range(0, clips.Count);
                for (var i = 0; i < clips.Count; i++)
                {
                    var candidate = clips[(start + i) % clips.Count];
                    if (candidate)
                    {
                        direct = candidate;
                        break;
                    }
                }
            }

            return new TrackRequest(entry.Key, direct, direct ? null : entry.AddressableClip, entry.VolumeScale, loopTrack);
        }

        private List<string> BuildPlaylistOrder(AudioCatalog.Playlist playlist)
        {
            var order = new List<string>();
            if (playlist.EntryKeys == null) return order;

            foreach (var entryKey in playlist.EntryKeys)
            {
                if (_owner.Catalog != null && _owner.Catalog.TryGetEntry(entryKey, out var entry)
                    && entry.Channel == AudioChannel.Music)
                {
                    order.Add(entryKey);
                }
                else
                {
                    Debug.LogError($"[AudioService] Playlist entry '{entryKey}' is missing or not a Music entry — skipped.");
                }
            }

            if (playlist.Shuffle)
            {
                for (var i = order.Count - 1; i > 0; i--)
                {
                    var j = Random.Range(0, i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
            }

            return order;
        }

        private void ClearPlaylist()
        {
            ActivePlaylistKey = null;
            _playlistOrder = null;
        }

        private void StartPlaylistTrack()
        {
            // Skip unresolvable tracks (deleted entries) without spinning forever.
            for (var attempts = 0; attempts < _playlistOrder.Count; attempts++)
            {
                var entryKey = _playlistOrder[_playlistIndex];
                if (_owner.Catalog.TryGetEntry(entryKey, out var entry))
                {
                    StartTrack(BuildRequest(entry, loopTrack: false));
                    return;
                }

                if (!TryAdvanceIndex()) break;
            }

            Stop();
        }

        private bool TryAdvanceIndex()
        {
            _playlistIndex++;
            if (_playlistIndex < _playlistOrder.Count) return true;

            if (_playlistLoop)
            {
                _playlistIndex = 0;
                return true;
            }

            return false;
        }

        private void StartTrack(TrackRequest request)
        {
            ActiveKey = request.Key;
            RunTrackAsync(++_generation, request);
        }

        private async void RunTrackAsync(int generation, TrackRequest request)
        {
            try
            {
                var clip = request.Clip;
                AsyncOperationHandle<AudioClip>? handle = null;

                if (!clip && request.Addressable != null && request.Addressable.RuntimeKeyIsValid())
                {
                    if (request.Key != null && _preloaded.TryGetValue(request.Key, out var warm)
                        && warm.IsValid() && warm.Result)
                    {
                        // Already resident from PreloadAsync — reuse it. The cache owns that handle,
                        // so the slot's Handle stays null and ReleaseSlot won't free it.
                        clip = warm.Result;
                    }
                    else
                    {
                        var load = request.Addressable.LoadAssetAsync<AudioClip>();
                        handle = load;
                        await load.Task;

                        if (!IsCurrent(generation))
                        {
                            Addressables.Release(load); // abandoned mid-load
                            return;
                        }

                        if (load.Status != AsyncOperationStatus.Succeeded || !load.Result)
                        {
                            Debug.LogError($"[AudioService] Addressable music clip for '{request.Key}' failed to load.");
                            Addressables.Release(load);
                            OnTrackUnavailable(generation);
                            return;
                        }

                        clip = load.Result;
                    }
                }

                if (!clip)
                {
                    Debug.LogError($"[AudioService] Music entry '{request.Key}' has no direct clip and no valid addressable.");
                    OnTrackUnavailable(generation);
                    return;
                }

                var nextIndex = _activeIndex == 0 ? 1 : 0;
                var next = _slots[nextIndex];
                var previous = _activeIndex >= 0 ? _slots[_activeIndex] : null;

                ReleaseSlot(next); // frees a stale handle; hard-cuts the oldest tail on rapid retargets
                next.Source.clip = clip;
                next.Source.loop = request.LoopTrack;
                next.Handle = handle;
                next.EntryScale = request.VolumeScale;
                next.FadeWeight = 0f;
                _activeIndex = nextIndex;
                ApplyVolumes();
                next.Source.Play();
                _consecutiveTrackFailures = 0; // a track that actually starts resets the failure run

                var fade = Mathf.Max(0f, _owner.MusicCrossfadeSeconds);
                if (fade <= 0f)
                {
                    next.FadeWeight = 1f;
                    if (previous != null) ReleaseSlot(previous);
                    ApplyVolumes();
                }
                else
                {
                    // Retarget-safe: fade FROM the weights currently on screen.
                    var nextStart = next.FadeWeight;
                    var previousStart = previous?.FadeWeight ?? 0f;
                    var elapsed = 0f;
                    while (elapsed < fade)
                    {
                        await Awaitable.NextFrameAsync();
                        if (!IsCurrent(generation)) return;
                        if (_owner.IsMusicPaused) continue; // freeze the crossfade while paused

                        elapsed += Time.unscaledDeltaTime;
                        var t = Mathf.Clamp01(elapsed / fade);
                        next.FadeWeight = Mathf.Lerp(nextStart, 1f, t);
                        if (previous != null) previous.FadeWeight = Mathf.Lerp(previousStart, 0f, t);
                        ApplyVolumes();
                    }

                    if (previous != null) ReleaseSlot(previous);
                }

                if (request.LoopTrack) return; // looping single track — nothing to monitor

                await MonitorTrackEndAsync(generation, next);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Awaitable MonitorTrackEndAsync(int generation, Slot slot)
        {
            // Always yield once before the first check: a clip shorter than the advance lead
            // would otherwise chain advances synchronously (unbounded recursion on a looping
            // playlist of very short clips).
            await Awaitable.NextFrameAsync();

            // Playlist tracks advance one crossfade early (seamless chaining); a plain non-looping
            // track just plays out and clears the active state at its end.
            while (IsCurrent(generation))
            {
                var source = slot.Source;
                if (!source || !source.clip) return;

                var lead = ActivePlaylistKey != null
                    ? Mathf.Max(Mathf.Max(0f, _owner.MusicCrossfadeSeconds), 0.05f)
                    : 0.02f;
                var remaining = source.clip.length - source.time;

                if (remaining <= lead)
                {
                    if (ActivePlaylistKey != null && _playlistOrder != null)
                    {
                        if (TryAdvanceIndex()) StartPlaylistTrack();
                        else Stop();
                    }
                    else
                    {
                        Stop();
                    }

                    return;
                }

                await Awaitable.NextFrameAsync();
            }
        }

        private void OnTrackUnavailable(int generation)
        {
            if (!IsCurrent(generation)) return;

            // A broken playlist track skips forward; a broken single track just clears state.
            if (ActivePlaylistKey != null && _playlistOrder != null)
            {
                // A full lap of consecutive failures means nothing in the list can play —
                // without this cap a looping playlist would advance forever.
                _consecutiveTrackFailures++;
                if (_consecutiveTrackFailures >= _playlistOrder.Count)
                {
                    Debug.LogError($"[AudioService] Playlist '{ActivePlaylistKey}' has no playable tracks " +
                                   $"({_playlistOrder.Count} consecutive failures) — stopping.");
                    Stop();
                    return;
                }

                if (TryAdvanceIndex())
                {
                    AdvanceAfterYieldAsync(generation);
                    return;
                }
            }

            Stop();
        }

        private async void AdvanceAfterYieldAsync(int generation)
        {
            try
            {
                // One-frame yield before retrying: a failing track must never chain into the
                // next one synchronously — RunTrackAsync can fail without reaching any await,
                // and a looping playlist of unplayable tracks would otherwise recurse on the
                // main thread until stack overflow (caught by the first consumer's play-mode gate).
                await Awaitable.NextFrameAsync();
                if (!IsCurrent(generation)) return;

                StartPlaylistTrack();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async void FadeOutAndStopAsync(int generation)
        {
            try
            {
                var active = _activeIndex >= 0 ? _slots[_activeIndex] : null;
                if (active == null || !active.Source || !active.Source.clip) return;

                var fade = Mathf.Max(0f, _owner.MusicCrossfadeSeconds);
                var start = active.FadeWeight;
                var elapsed = 0f;
                while (fade > 0f && elapsed < fade)
                {
                    await Awaitable.NextFrameAsync();
                    if (!IsCurrent(generation)) return;
                    if (_owner.IsMusicPaused) continue; // freeze the fade-out while paused

                    elapsed += Time.unscaledDeltaTime;
                    active.FadeWeight = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / fade));
                    ApplyVolumes();
                }

                if (!IsCurrent(generation)) return;
                ReleaseSlot(active);
                _activeIndex = -1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ReleaseSlot(Slot slot)
        {
            if (slot.Source)
            {
                slot.Source.Stop();
                slot.Source.clip = null;
            }

            if (slot.Handle.HasValue)
            {
                Addressables.Release(slot.Handle.Value);
                slot.Handle = null;
            }

            slot.FadeWeight = 0f;
            slot.EntryScale = 1f;
        }
    }
}
