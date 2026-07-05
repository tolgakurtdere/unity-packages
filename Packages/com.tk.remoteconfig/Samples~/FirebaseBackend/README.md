# Firebase Backend

A reference `IRemoteConfigBackend` implementation backed by **Firebase Remote Config**
(`FirebaseRemoteConfigBackend`). Copy it into your project and adapt it.

`com.tk.remoteconfig` ships **no** Firebase dependency — the backend seam keeps the package
backend-agnostic, and this sample is the Firebase adapter you plug in. It references
`Firebase.RemoteConfig`, so it only compiles once your project has the Firebase Remote Config SDK
installed.

## 1. Install Firebase Remote Config (your choice)

Firebase is the *game's* dependency, not this package's. Add the Firebase Remote Config SDK the way
you normally would — any one of:

- Google's Firebase Unity registry (scoped registry), then add
  `com.google.firebase.remote-config`, **or**
- the Firebase Unity SDK tarballs (`FirebaseRemoteConfig.tgz` + its dependencies), **or**
- the Firebase Unity `.unitypackage`.

Use the **latest stable** Firebase Unity SDK (13.x as of this writing). The Remote Config API this
sample uses is stable across 12.x → 13.x, so a newer minor should also work; the version is yours to
pick.

Also drop your `google-services.json` / `GoogleService-Info.plist` into the project as Firebase
requires.

## 2. Wire the backend into `RemoteConfigService`

Initialize Firebase first (check/fix dependencies), then hand the backend to the service:

```csharp
using Firebase;
using TK.RemoteConfig;
using TK.RemoteConfig.Samples.FirebaseBackend;

async Task BootRemoteConfig()
{
    // Firebase must be initialized before the backend reads/writes Remote Config.
    var status = await FirebaseApp.CheckAndFixDependenciesAsync();
    if (status != DependencyStatus.Available)
    {
        Debug.LogError($"Firebase deps unavailable: {status}");
        return;
    }

    var rc = new RemoteConfigService(new FirebaseRemoteConfigBackend());

    // Declare params BEFORE InitializeAsync so their defaults are registered with Firebase.
    var startCoins = rc.Int("starting_coins", 100);

    await rc.InitializeAsync(); // SetDefaults → fetch → activate

    Debug.Log($"starting_coins = {startCoins.Value}");
}
```

That is the whole integration: `new RemoteConfigService(new FirebaseRemoteConfigBackend())`. Declare
your typed params, `await InitializeAsync()`, read them anywhere via `.Value` (or implicit
conversion).

## How it maps to Firebase

| `IRemoteConfigBackend` | Firebase Remote Config |
| --- | --- |
| `InitializeAsync(defaults)` | `FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(...)`, then sets `IsReady`. |
| `FetchAndActivateAsync()` | `FetchAsync(TimeSpan.Zero)` → check `Info.LastFetchStatus == Success` → `await ActivateAsync()` (returns whether new values activated). |
| `TryGetLong/Double/Bool/String` | `DefaultInstance.GetValue(key)` → `.LongValue/.DoubleValue/.BooleanValue/.StringValue`; an empty `ByteArrayValue` means "no value" → returns `false`. |
| `IsReady` | `true` once `SetDefaultsAsync` completed. |

## Notes

- **`TryGet*` never throws.** `RemoteConfigService`'s raw reads call these without a try/catch and
  rely on a `false` return to fall back to the parameter default, so every accessor here is wrapped —
  any Firebase conversion exception (or a missing value) becomes `false`. Keep that contract if you
  edit the file.
- **iOS safety.** Firebase Remote Config values are only safe to read after `SetDefaultsAsync`
  completes. This backend gates `IsReady` on that call, and the service gates its reads on the
  backend being initialized, so early reads return your declared defaults instead of crashing.
- **Fetch cadence.** `TimeSpan.Zero` bypasses the fetch cache so Firebase console changes appear
  immediately — good for development. For production, prefer the default (12h) or a few minutes to
  respect Firebase's fetch quotas.
