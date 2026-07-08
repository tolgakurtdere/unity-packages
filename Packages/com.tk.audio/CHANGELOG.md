# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-07

Fresh design (no portable in-house reference — the prior project wrapped DarkTonic MasterAudio); keeps MA's proven ideas (per-entry Addressables, playlists, retrigger throttling, clip variations) and structurally fixes the pains its wrapper documented (manual ad-mute latch, music toggle restarting playlists). Approved design: `docs/specs/2026-07-07-tk-audio-design.md`. Play-mode verified in game-shikaku (clipless boot fails safe with no freeze; with clips, music plays, zero errors) before tagging.

### Added

- **`AudioService`** — plain-class engine constructed at the composition root: owns a hidden `[TK.Audio]` host with two crossfading music sources and a pooled SFX bank (`ObjectPool<AudioSource>`); `Dispose()` tears it down.
- **Channels** — `MusicEnabled`/`SfxEnabled` + `MusicVolume`/`SfxVolume`, durable and never written by the package. **Optional persistence:** pass an `ISaveSystem` and the service loads/persists them itself (key `tk_audio_settings`); pass null and your settings service owns them (push on change).
- **Two-level mute** — `PushMute()`/`PopMute()`: ref-counted temporary suppression (`RefCountLock`, unbalanced pop throws) on top of the settings; `IsMuted`, `EffectiveMusicVolume`/`EffectiveSfxVolume`. One-line glue for `com.tk.ads`' `AudioMuteSetter`. Muting silences in-flight SFX immediately and volume-gates music without stopping it (position and playlist progression survive a toggle).
- **SFX** — `PlaySfx(key | clip, …)`: pooled one-shots, per-entry clip variations (random pick), pitch = 1 ± `pitchVariance`, per-key `minRetriggerInterval` throttle (same-frame spam guard), timeScale-independent playback with unscaled return timers. Disabled/muted channel spawns nothing; direct-clip overload bypasses catalog tuning and throttle (documented).
- **Music + playlists** — `PlayMusic(key | clip, loop)`, `PlayPlaylist(key)`, `StopMusic()`, `MusicCrossfadeSeconds` (0 = hard cut), `ActiveMusicKey`/`ActivePlaylistKey`. Generation-guarded async flows (latest request wins); idempotent re-requests; playlists shuffle once per start and advance one crossfade early; broken tracks are skipped with errors.
- **`AudioCatalog`** (`Create → TK → Audio Catalog`, optional) — string-keyed entries `{key, channel, clips[], addressableClip, volumeScale, pitchVariance, minRetriggerInterval}` + playlists `{key, entryKeys[], shuffle, loop}`. **Addressable music**: loaded on demand inside the play pipeline, released when the source drops the clip (or the load is abandoned). Addressable-only SFX entries are rejected in this version.
- **`Audio` static façade** — `Bind`/`Unbind` + mirrored members; warn-once no-op when unbound; domain-reload-off reset.
