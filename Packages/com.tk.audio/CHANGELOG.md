# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - Unreleased

Settings are now **game-owned** (the whole suite's settings-screen toggles use one model — Sound/Music/Vibration/Notifications all owned by the game's settings service, single source of truth). Tag after game-shikaku re-verification.

### Changed (breaking)

- **Removed the `ISaveSystem` constructor parameter and built-in persistence.** `MusicEnabled`/`SfxEnabled`/`MusicVolume`/`SfxVolume` are game-owned runtime state (default on/1) — the game pushes them from its settings service and persists them there; the package no longer writes them. Constructor is now `AudioService(AudioCatalog catalog = null, int sfxPoolSize = 8)`. **Migration:** drop the `saveSystem` argument (`new AudioService(catalog, saveSystem: null)` → `new AudioService(catalog)`); load your saved values and push them via the setters on boot (subscribe to `Changed` to persist). Removes the two-sources-of-truth footgun when a game has its own settings service.
- `TK.Audio` no longer references `TK.Core.Save` (still depends on `TK.Core.Utilities` for the object pool / ref-count lock — com.tk.core is still a prerequisite).

## [0.3.0] - 2026-07-08

Music & settings polish. Approved design: `docs/specs/2026-07-08-tk-audio-v0.3-design.md`. Play-mode verified in game-shikaku (ad-mute froze `AudioSource.time` and resumed in place; settings-toggle restarted from the top; `FadeChannelVolume` rode 1.0→0.3 without touching the setting; `Changed` fired on real changes only; SFX mute unchanged) — also a cross-editor check (game on 6000.5.2f1 vs the 6000.3.6f1 harness). Addressable-music preload is play-mode verified here, not consumer-covered (the game has no addressable music). **Behavior change (music mute):** an ad mute now PAUSES music instead of silencing it, and turning the music setting off STOPS it (re-enabling replays the remembered track from the top). SFX mute is unchanged.

### Added

- **`PauseMusic()` / `ResumeMusic()`** — explicit game-driven music pause (app-pause / phone-call); composes with ad mute (music is frozen while EITHER holds) and resumes exactly where it was.
- **`FadeChannelVolume(channel, target, seconds)`** — smoothly fades a channel's volume for cutscene ducking / pause. A TRANSIENT multiplier: it does not change or persist `MusicVolume`/`SfxVolume` and does not raise `Changed`. The latest fade per channel supersedes any earlier one.
- **`Changed` event** — raised after any durable setting (enabled/volume) actually changes, so a bound settings slider reflects code-side changes (a no-op write doesn't fire it). Instance-only.
- **`PreloadAsync(musicKey)`** — warms an addressable music clip so the first `PlayMusic` reuses the resident asset (no load hitch); no-op for direct-clip entries, held until `Dispose`.
- **`IsMusicPaused`** — true while music is frozen by an ad mute or `PauseMusic`.

### Changed

- **Ad mute (`PushMute`/`PopMute`) now pauses music** (`AudioSource.Pause()`, position frozen) instead of volume-gating it — resumes seamlessly when the ad ends, even mid-crossfade (the elapsed-based fade loops freeze while paused). SFX one-shots have nothing to pause, so their mute stays a volume-gate + no-spawn.
- **`MusicEnabled = false` now stops music** and remembers the request; `= true` replays it from the top. Booting with music disabled starts nothing until it's enabled. (Replaces the 0.1–0.2 volume-gating, which kept music advancing silently.)

## [0.2.0] - 2026-07-08

SFX control + editor authoring, from the MasterAudio-teardown backlog (loop/stop and string-key authoring were the two highest-leverage gaps). Additive; music/settings polish is v0.3. Approved design: `docs/specs/2026-07-08-tk-audio-v0.2-design.md`. Play-mode verified in game-shikaku (loop handle live, voice cap, editor dropdowns against the real catalog; the 0.1.0 freeze fix rode in) before tagging.

### Added

- **Looping SFX** — `AudioHandle PlaySfxLoop(string key | AudioClip, …)`; the returned `AudioHandle` (a default-safe struct — `default` and stale handles are silent no-ops) exposes `IsPlaying`, `Stop()`, `FadeOutAndStop(seconds)`. Loops use a dedicated source pool (a loop's lifetime is unbounded, so it must not consume the auto-return one-shot pool) and honor mute/volume like one-shots.
- **`StopSfx(string key)` / `StopAllSfx()`** — stop one-shots and loops by key, or everything (music untouched).
- **Per-key voice cap** — `AudioCatalog` entry field `maxConcurrentVoices` (0 = unlimited); at the cap the oldest voice of that key is culled before the new one plays.
- **Delayed one-shot** — `PlaySfx(string key, float volumeScale, float delaySeconds)` (unscaled; the retrigger throttle is stamped at call time, not after the delay).
- **`[AudioKey]` / `[AudioPlaylistKey]`** field attributes + inspector dropdowns (new `TK.Audio.Editor` assembly) — keys collected from every `AudioCatalog` in the project; a value no catalog defines is shown tagged `(missing)`, never cleared.
- **Catalog auto-populate** — a drag-and-drop area on the `AudioCatalog` inspector: drop `AudioClip`s to append Sfx entries (name → key, existing keys skipped, sane defaults set).

### Changed

- `AudioCatalog` exposes `EntryKeys()` / `PlaylistKeys()` enumerations (used by the drawers and the populate tool).

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
