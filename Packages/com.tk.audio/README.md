# TK Audio

Audio framework for Unity games: two channels (Music / Sfx) with optional save-backed settings, a ref-counted temporary mute (ads-ready), pooled one-shot SFX with variations and retrigger throttling, and a crossfading music player with playlists. Music clips can be Addressables — loaded on demand, released when no longer played.

## Install

Requires `com.tk.core` installed first (uses its `TK.Core.Utilities` + `TK.Core.Save` assemblies).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.audio
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.audio#com.tk.audio/0.2.0
```

## Quickstart

Construct once at your composition root, register/bind, dispose on teardown:

```csharp
using TK.Audio;

// catalog is optional (direct-clip overloads work without one);
// saveSystem is optional (null = runtime-only, your settings service owns persistence)
var audio = new AudioService(audioCatalog, saveSystem: null);
Context.Register(audio);   // AppContext-style container
Audio.Bind(audio);         // optional static sugar: Audio.PlaySfx("click") anywhere
```

Play things:

```csharp
Audio.PlaySfx("click");                 // catalog entry: variations, pitch variance, throttle, voice cap
Audio.PlaySfx(myClip, 0.8f);            // direct clip — no catalog needed
Audio.PlaySfx("stinger", 1f, 0.3f);     // play after a 0.3 s (unscaled) delay
Audio.PlayMusic("music_menu");          // crossfades from whatever is playing
Audio.PlayPlaylist("gameplay");         // auto-advancing, crossfading playlist
Audio.StopMusic();
```

## SFX control

Beyond one-shots, SFX can loop and be stopped:

```csharp
AudioHandle wind = Audio.PlaySfxLoop("wind");  // ambient / engine loops
// …later
wind.FadeOutAndStop(1f);                       // or wind.Stop();  (default/stale handles are safe no-ops)

Audio.StopSfx("click");   // stop every one-shot AND loop of this key
Audio.StopAllSfx();       // stop all SFX; music keeps playing
```

- **`AudioHandle`** is a default-safe struct: `default(AudioHandle)` and a handle to an already-stopped loop are silent no-ops, so you never need null/liveness checks.
- **Voice cap:** set an entry's `Max Concurrent Voices` (0 = unlimited) to cap simultaneous voices of that key — at the cap the oldest is culled first. This is the count axis; `Min Retrigger Interval` is the time axis (drops same-frame spam). They compose.
- Loops use a dedicated source pool (not the one-shot pool) and honor mute/volume like one-shots.

## Editor authoring

Annotate string fields to get a catalog-backed dropdown instead of a raw text field (no more typo-prone keys):

```csharp
[AudioKey] public string clickSfx;          // dropdown of every entry key in the project's catalogs
[AudioPlaylistKey] public string menuMusic; // dropdown of every playlist key
```

A value no catalog defines is shown tagged `(missing)` rather than silently cleared.

To fill a catalog fast, select it and **drag `AudioClip`s onto the drop area** in its inspector — one Sfx entry per clip (name → key, existing keys skipped, `volumeScale`/throttle defaults set).

**Settings wiring (game-owned persistence, à la a SettingsService):**

```csharp
settings.Changed += () =>
{
    audio.MusicEnabled = settings.MusicEnabled;
    audio.SfxEnabled = settings.SoundEnabled;
};
```

Or pass an `ISaveSystem` to the constructor and the service persists `MusicEnabled`/`SfxEnabled`/volumes itself (key `tk_audio_settings`). Pick ONE owner — don't do both.

**Ads mute glue (com.tk.ads):**

```csharp
adsOptions.AudioMuteSetter = muted =>
{
    if (muted) audio.PushMute();
    else audio.PopMute();
};
```

`PushMute`/`PopMute` is a ref-counted temporary suppression on top of the durable settings — the package never writes your settings, and overlapping suppressions (ad + cutscene) compose. Muting silences in-flight SFX immediately and volume-gates music **without stopping it**, so toggling music back on resumes where the player expects.

## Catalog

Create one via `Create → TK → Audio Catalog`. Each entry: key, channel, one or more direct clips (a random one is picked per play — variations), or an addressable clip (music-oriented), plus `volumeScale`, `pitchVariance` (SFX pitch = 1 ± variance) and `minRetriggerInterval` (rapid replays of the same key are dropped — the same-frame coin-spam guard).

Playlists live in the same asset: a key, ordered Music entry keys, optional shuffle (once per start), loop on by default. Tracks crossfade into each other (`MusicCrossfadeSeconds`, default 0.5; 0 = hard cut).

## Gotchas

- **A fresh catalog entry added to an EMPTY list starts zeroed** (Unity creates serialized list elements without running field initializers): `volumeScale` 0 means a silent sound and playlist `loop` starts off — fill the values after adding. Growing a non-empty list duplicates the previous element instead.
- **Direct-clip `PlaySfx(clip, …)` bypasses catalog tuning, the retrigger throttle, AND the voice cap** — the catalog is where central tuning lives.
- **Addressable-only SFX entries are rejected in this version** (error log): on-demand loading fits music; SFX wants zero-latency direct refs. Addressable SFX with a warm-release window is on the roadmap.
- The service owns a hidden `[TK.Audio]` host object with its AudioSources; call `Dispose()` on teardown (or let app shutdown take it).
- SFX playback is timeScale-independent (pause-menu clicks work); music fades and playlist advance run on unscaled time.
- With two music sources, retargeting during an active crossfade hard-cuts the oldest tail — an accepted trade-off.
