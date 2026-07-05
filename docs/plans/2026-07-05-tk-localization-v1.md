# com.tk.localization v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This package is executed and reviewed **Opus-only** per the user's request — dispatch every implementer and reviewer on the Opus model.

**Goal:** A professional localization package (`com.tk.localization`) over Unity's Localization package: per-locale TMP font swapping, an RTL text-shaping pipeline, and an injectable locale selection/persistence service — with the reference project's Odin dependency, wrong-locale font fallback, silent null-font, and fragile listener wiring all fixed.

**Architecture:** Approved committed spec: `docs/specs/2026-07-05-tk-localization-design.md` (READ IT FIRST — behavioral contracts are binding). Font mapping via `LocaleFontMap`/`LocaleFontInfo` ScriptableObjects with a correct fallback chain + mandatory fallback; a pure `RtlText` pipeline ported from the reference; `FontLocalizer` (font on locale change) and `LocalizedTmpText` (string+RTL+font, all-in-one) components that self-subscribe to Unity Localization events; a `LocaleService` + `ILocalePersistence` seam for selection/persistence. Depends on `com.unity.localization` + `com.unity.ugui` (TMP); no Odin, no com.tk.core.

**Tech Stack:** Unity 6000.3.6f1 host, C#, NUnit EditMode. Deps: `com.unity.localization` (newest stable) + `com.unity.ugui` (TMP). Pure logic (RTL, font-map, selection, persistence) is unit-tested; Unity-Localization-touching parts (LocaleService set/init, component subscriptions) are review-verified.

## Global Constraints

- Repo: `/Users/tolgahankurtdere/Documents/GitHub/unity-packages`, branch `main`. Base = the current `main` tip when execution begins (the commit that adds this plan, on top of spec commit `6bf0828`). New package root: `Packages/com.tk.localization/`.
- **NEVER run Unity CLI against the host repo project** (the user's editor may be open). Harness: `/private/tmp/claude-501/-Users-tolgahankurtdere-Documents-GitHub-unity-packages/125643b5-4b33-48e0-b763-cca5d06442d8/scratchpad/tk-verify` (already wired with the seven TK packages + AppLovin & OpenUPM scoped registries — **DO NOT remove them**). If the harness is missing (new session), recreate: `Assets/` + `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.3.6f1`) + `Packages/manifest.json` with `com.unity.test-framework` 1.6.0, the eight TK packages as `file:` absolute paths, `testables` listing all eight, and the two scoped registries.
- Gate command (from the harness dir; **NEVER combine `-quit` with `-runTests`**; Bash timeout 600000):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$(pwd)/results.xml" -logFile "$(pwd)/unity.log"
  ```
  Success = exit 0 AND every `TK.Localization.Tests` case `result="Passed"` AND zero `error CS` / `warning CS` under `Packages/com.tk.localization`. Baseline before Task 1 = current harness total after notification (**~241 — trust results.xml, not arithmetic**). Report the exact `TK.Localization.Tests` count each task.
- package.json (exact): name `com.tk.localization`, version `0.1.0`, displayName `TK Localization`, description `Localization framework over Unity Localization: per-locale TMP font swapping, an RTL text-shaping pipeline, and an injectable locale selection/persistence service.`, unity `6000.0`, dependencies EXACTLY `{ "com.unity.localization": "<newest stable>", "com.unity.ugui": "2.0.0" }` — verify the newest stable `com.unity.localization` on Unity's **default registry** at execution and pin it (reference uses `1.5.9`; if still newest, use it), author `{ "name": "Tolga Kurtdere", "url": "https://github.com/tolgakurtdere" }`, keywords `["tk", "localization", "i18n", "tmp", "rtl"]`. (Samples array added in Task 5.)
- Asmdefs: `TK.Localization` (rootNamespace `TK.Localization`, `"references": ["Unity.Localization", "Unity.TextMeshPro"]` — **RE-VERIFY these exact asmdef names** against the installed packages at execution and correct if needed, `"autoReferenced": true`); `TK.Localization.Tests` (`"includePlatforms": ["Editor"]`, references `["TK.Localization", "Unity.Localization", "Unity.TextMeshPro", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll"]`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"autoReferenced": false`).
- **No Odin/third-party types** (`Sirenix`/`OdinInspector`) anywhere — the repo standard is Odin-free (verified). Public API may use Unity's own `Locale`/`LocalizedString`/`TMP_FontAsset` (those are the domain).
- Namespaces: `TK.Localization` (runtime), `TK.Localization.Tests` (tests).
- `LocaleService`, the components, and the RTL static buffer are **main-thread-affine** (Unity Localization + TMP are main-thread; documented).
- Every file/folder under `Packages/com.tk.localization` gets a committed `.meta` (the harness gate generates them). Conventional commits ending with the trailer `Co-Authored-By: Claude <noreply@anthropic.com>` — **NO model name**. Committing to `docs/` is fine; do NOT push mid-plan; do NOT commit `.superpowers/` or unrelated host churn.
- **Reference source to port/verify against** (read-only): RTL pipeline at `/Users/tolgahankurtdere/Documents/GitHub/g-brain_test_5/Assets/UnicoStudio/UnicoLibs/UnityLocalizationExtensions/Core/Scripts/Utilities/RTLSupport/` (`RTLSupport.cs`, `GlyphFixer.cs`, `TashkeelFixer.cs`, `LigatureFixer.cs`, `RichTextFixer.cs`, `TextUtils.cs`, `FastStringBuilder.cs`); font layer at `.../Core/Scripts/` (`FontLocalizer.cs`, `TextMeshProFontMapInfoAsset.cs`, `TextMeshProFontInfoAsset.cs`). The reference has NO RTL unit tests (verified) — write invariant + detection tests.

---

### Task 1: Skeleton + font ScriptableObjects + TMP font applier + harness wiring

**Files:**
- Create: `Packages/com.tk.localization/package.json`, `Runtime/TK.Localization.asmdef`
- Create: `Runtime/Fonts/LocaleFontInfo.cs`, `Runtime/Fonts/LocaleFontMap.cs`, `Runtime/Fonts/TmpFontApplier.cs`
- Create: `Tests/Editor/TK.Localization.Tests.asmdef`, `Tests/Editor/LocaleFontMapTests.cs`, `Tests/Editor/TmpFontApplierTests.cs`
- Modify: harness `Packages/manifest.json`

**Interfaces produced:** `LocaleFontInfo`, `LocaleFontMap` (with `Resolve`), `TmpFontApplier` — Tasks 4 consume these.

- [ ] **Step 1: package.json + both asmdefs** (exact values from Global Constraints; pin the verified newest `com.unity.localization`).

- [ ] **Step 2: LocaleFontInfo.cs** (full code):

```csharp
using TMPro;
using UnityEngine;

namespace TK.Localization
{
    /// <summary>One locale's TMP font bundle: font asset, optional material preset, and RTL direction flag.</summary>
    [CreateAssetMenu(fileName = "LocaleFontInfo", menuName = "TK/Localization/Locale Font Info")]
    public sealed class LocaleFontInfo : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Material _material;   // optional; may be null
        [SerializeField] private bool _rightToLeft;

        public TMP_FontAsset Font => _font;
        public Material Material => _material;
        public bool RightToLeft => _rightToLeft;
    }
}
```

- [ ] **Step 3: LocaleFontMap.cs** (full code — note the fallback fix vs the reference):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace TK.Localization
{
    /// <summary>
    /// Maps locales to <see cref="LocaleFontInfo"/> with a mandatory fallback. Resolve() tries the requested
    /// locale, then that locale's own fallbacks, then the mandatory Fallback — never returns null while
    /// Fallback is assigned. (Reference used SelectedLocale's fallbacks and could return null; both fixed.)
    /// </summary>
    [CreateAssetMenu(fileName = "LocaleFontMap", menuName = "TK/Localization/Locale Font Map")]
    public sealed class LocaleFontMap : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public Locale Locale;
            public LocaleFontInfo Font;
        }

        [SerializeField] private LocaleFontInfo _fallback;
        [SerializeField] private List<Entry> _entries = new();

        public LocaleFontInfo Fallback => _fallback;

        public LocaleFontInfo Resolve(Locale locale)
        {
            if (locale != null)
            {
                var direct = Find(locale.Identifier.Code);
                if (direct != null) return direct;

                foreach (var fallback in locale.GetFallbacks())   // the REQUESTED locale's fallbacks
                {
                    if (fallback == null) continue;
                    var hit = Find(fallback.Identifier.Code);
                    if (hit != null) return hit;
                }
            }

            if (_fallback == null)
                Debug.LogError($"[TK.Localization] LocaleFontMap '{name}' has no Fallback assigned; returning null.");
            return _fallback;
        }

        private LocaleFontInfo Find(string code)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry?.Locale != null && entry.Font != null && entry.Locale.Identifier.Code == code)
                    return entry.Font;
            }
            return null;
        }
    }
}
```
NOTE: `Locale.GetFallbacks()` is a Unity Localization extension (`UnityEngine.Localization`). RE-VERIFY the exact method/namespace against the installed package at execution; if the extension lives elsewhere, add the correct `using` (a `Locale` with no fallback metadata returns an empty sequence, which is fine — Resolve then goes to `_fallback`).

- [ ] **Step 4: TmpFontApplier.cs** (full code — the shared apply helper, used by both components in Task 4, DRY):

```csharp
using TMPro;

namespace TK.Localization
{
    /// <summary>Applies a resolved <see cref="LocaleFontInfo"/> to a TMP_Text (font, optional material, direction).</summary>
    public static class TmpFontApplier
    {
        public static void Apply(TMP_Text text, LocaleFontInfo info)
        {
            if (text == null || info == null) return;
            if (info.Font != null) text.font = info.Font;
            if (info.Material != null) text.fontSharedMaterial = info.Material;
            text.isRightToLeftText = info.RightToLeft;
        }
    }
}
```

- [ ] **Step 5: LocaleFontMapTests.cs** (complete NUnit code, namespace `TK.Localization.Tests`; build assets with `ScriptableObject.CreateInstance<LocaleFontInfo>()` / `<LocaleFontMap>()` and populate `_entries`/`_fallback` via `SerializedObject` or a small reflection/test helper — OR add an `internal` test-only setter; prefer a `#if UNITY_INCLUDE_TESTS internal void` seam if SerializedObject is awkward; `Locale.CreateLocale("xx")` for locales). Tests (5):
  1. `Resolve_MappedLocale_ReturnsItsFont` — entry (Locale "tr" → fontA); `Resolve(Locale "tr")` == fontA.
  2. `Resolve_UnmappedLocale_ReturnsFallback` — no entry for "ja"; `Resolve(Locale "ja")` == the Fallback info.
  3. `Resolve_NullLocale_ReturnsFallback` — `Resolve(null)` == Fallback.
  4. `Resolve_NoFallbackAssigned_LogsErrorAndReturnsNull` — `_fallback` null, unmapped locale → `LogAssert.Expect(LogType.Error, ...)`, returns null.
  5. `Resolve_NeverReturnsNull_WhenFallbackSet` — several unmapped locales all return the Fallback (non-null).
  (The requested-locale `GetFallbacks()` chain branch is review-verified — setting fallback metadata on a `CreateLocale`d locale is fiddly; if the installed API makes it easy, add `Resolve_FallbackChain_UsesRequestedLocaleFallbacks`.)

- [ ] **Step 6: TmpFontApplierTests.cs** (complete NUnit code, 2 tests): create a `GameObject` with a `TextMeshProUGUI`; `TmpFontApplier.Apply(text, info)` where info has a font + `RightToLeft=true` → `text.font` set, `text.isRightToLeftText` true; `Apply(text, infoWithNullMaterial)` does not throw and leaves `fontSharedMaterial` untouched. (Build a `LocaleFontInfo` via `CreateInstance` + a test font, or a dummy `TMP_FontAsset` — if creating a real `TMP_FontAsset` is heavy, assert the `isRightToLeftText`/null-safety paths and review-verify the font assignment.) `[TearDown]` destroys the GameObject.

- [ ] **Step 7: harness wiring** — in the harness `Packages/manifest.json`: add `"com.tk.localization": "file:/Users/tolgahankurtdere/Documents/GitHub/unity-packages/Packages/com.tk.localization"` to `dependencies` and `"com.tk.localization"` to `testables`. The embedded package's deps (`com.unity.localization`, `com.unity.ugui`) resolve from the default registry automatically — confirm `com.unity.localization` lands in the harness packages-lock after the gate. Leave the seven existing packages and the two scoped registries untouched.

- [ ] **Step 8: gate** (baseline + 7). **Step 9: commit** — `feat(localization): add com.tk.localization skeleton with locale font map and applier`.

---

### Task 2: RTL text-shaping pipeline (port) + RtlText facade

**Files:**
- Create (PORT from reference): `Runtime/Rtl/RtlSupport.cs`, `Runtime/Rtl/GlyphFixer.cs`, `Runtime/Rtl/TashkeelFixer.cs`, `Runtime/Rtl/LigatureFixer.cs`, `Runtime/Rtl/RichTextFixer.cs`, `Runtime/Rtl/RtlTextUtils.cs`, `Runtime/Rtl/RtlStringBuilder.cs` (+ any glyph/ligature data files the reference has)
- Create: `Runtime/Rtl/RtlText.cs` (public facade)
- Create: `Tests/Editor/RtlTextTests.cs`

**Interfaces produced:** `RtlText.IsRtl(string)`, `RtlText.Fix(string)` — Task 4's `LocalizedTmpText` consumes these.

This is a faithful PORT of a complex, working algorithm (Arabic/Farsi glyph shaping) — do NOT reimplement from scratch. Copy the reference files, then adapt.

- [ ] **Step 1: port the 7 pipeline files** from `/Users/tolgahankurtdere/Documents/GitHub/g-brain_test_5/Assets/UnicoStudio/UnicoLibs/UnityLocalizationExtensions/Core/Scripts/Utilities/RTLSupport/` into `Runtime/Rtl/` (rename `RTLSupport.cs`→`RtlSupport.cs`, `TextUtils.cs`→`RtlTextUtils.cs`, `FastStringBuilder.cs`→`RtlStringBuilder.cs`; keep `GlyphFixer`/`TashkeelFixer`/`LigatureFixer`/`RichTextFixer`). Rules:
  - Change the namespace to `TK.Localization`.
  - Make every ported type `internal` (they are implementation detail — only `RtlText` is public).
  - **Preserve the algorithm byte-faithfully** — glyph tables, ligature maps, shaping logic. Do not "improve" the shaping.
  - **RE-VERIFY the ported code is self-contained**: no remaining references to Odin (`Sirenix`), game types, or other UnicoLibs. Grep the ported files for `Sirenix`, `Unico`, and any `using` that points outside `System`/`UnityEngine`/`TK.Localization`. If a glyph/ligature DATA file (e.g. a `.cs` table or an asset) is referenced, port it too. If anything external remains, STOP and report.
  - Fix internal type-name references to the renamed files (`RTLSupport`→`RtlSupport`, `TextUtils`→`RtlTextUtils`, `FastStringBuilder`→`RtlStringBuilder`).

- [ ] **Step 2: RtlText.cs** (public facade — adapt to the ported signatures; RE-VERIFY `RtlSupport.FixRTL(...)` and `RtlStringBuilder`/`RtlTextUtils` member names against the files you just ported and adjust this code to match):

```csharp
namespace TK.Localization
{
    /// <summary>
    /// Public RTL text utilities. IsRtl detects RTL content; Fix shapes Arabic/Farsi (glyphs, tashkeel,
    /// ligatures), preserves rich-text tags, and reverses for display. Main-thread-affine (reuses a static
    /// buffer, matching the reference). Pure — no Unity Localization dependency.
    /// </summary>
    public static class RtlText
    {
        // Reuse a static buffer like the reference (main-thread only). RE-VERIFY the RtlStringBuilder ctor.
        private static readonly RtlStringBuilder s_buffer = new RtlStringBuilder(new char[512]);

        public static bool IsRtl(string text)
        {
            return !string.IsNullOrEmpty(text) && RtlTextUtils.IsRTLInput(text);
        }

        public static string Fix(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            s_buffer.Clear();
            RtlSupport.FixRTL(text, s_buffer, farsi: true, fixTextTags: true, preserveNumbers: false);
            s_buffer.Reverse();
            return s_buffer.ToString();
        }
    }
}
```
(This mirrors the reference's `FontLocalizer.GetRightToLeftFixedText` + `TextUtils.IsRTLInput`. If the ported `FixRTL` signature or `RtlStringBuilder` API differs, adjust — the ported source is the source of truth.)

- [ ] **Step 3: RtlTextTests.cs** (complete NUnit code, 8 tests — invariant + detection, since the reference has no RTL tests to port):
  1. `IsRtl_ArabicString_True` — `RtlText.IsRtl("مرحبا")` true.
  2. `IsRtl_EnglishString_False` — `RtlText.IsRtl("Hello")` false.
  3. `IsRtl_Empty_False` and `IsRtl_Null_False`.
  4. `Fix_LtrInput_Unchanged` — `RtlText.Fix("Hello 123")` == "Hello 123".
  5. `Fix_Empty_ReturnsInput` — `Fix("")` == "" and `Fix(null)` == null.
  6. `Fix_ArabicInput_NonEmpty_AndTransformed` — `Fix("مرحبا")` is non-empty and NOT equal to the raw input (shaping+reversal changed it).
  7. `Fix_PreservesRichTextTag` — `Fix("<b>مرحبا</b>")` still contains `"<b>"` and `"</b>"`.
  8. `Fix_IsDeterministic` — calling `Fix` twice on the same Arabic input yields the same result (guards the shared static buffer against cross-call contamination).

- [ ] **Step 4: gate** (prev + 8). **Step 5: commit** — `feat(localization): port RTL shaping pipeline with public RtlText facade`.

---

### Task 3: Locale selection + persistence (ILocalePersistence, LocaleSelection, LocaleService)

**Files:**
- Create: `Runtime/Locale/ILocalePersistence.cs`, `Runtime/Locale/PlayerPrefsLocalePersistence.cs`, `Runtime/Locale/LocaleSelection.cs`, `Runtime/Locale/LocaleService.cs`
- Create: `Tests/Editor/LocaleSelectionTests.cs`, `Tests/Editor/PlayerPrefsLocalePersistenceTests.cs`, `Tests/Editor/FakeLocalePersistence.cs`

**Interfaces produced:** `ILocalePersistence`, `PlayerPrefsLocalePersistence`, `LocaleSelection.Choose`, `LocaleService`.

- [ ] **Step 1: ILocalePersistence.cs + LocaleSelection.cs** (full code):

```csharp
namespace TK.Localization
{
    /// <summary>Persists the user's chosen locale code. Implementations decide the storage + key.</summary>
    public interface ILocalePersistence
    {
        string Load();                 // saved locale code, or null if none
        void Save(string localeCode);
    }
}
```
```csharp
using System.Collections.Generic;

namespace TK.Localization
{
    /// <summary>Pure locale-choice policy: saved (if available) → device (if available) → first available → null.</summary>
    public static class LocaleSelection
    {
        public static string Choose(string savedCode, string deviceCode, IReadOnlyList<string> availableCodes)
        {
            if (availableCodes == null || availableCodes.Count == 0) return null;
            if (!string.IsNullOrEmpty(savedCode) && Contains(availableCodes, savedCode)) return savedCode;
            if (!string.IsNullOrEmpty(deviceCode) && Contains(availableCodes, deviceCode)) return deviceCode;
            return availableCodes[0];
        }

        private static bool Contains(IReadOnlyList<string> list, string code)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i] == code) return true;
            return false;
        }
    }
}
```

- [ ] **Step 2: PlayerPrefsLocalePersistence.cs** (full code):

```csharp
using System;
using UnityEngine;

namespace TK.Localization
{
    /// <summary>ILocalePersistence backed by PlayerPrefs under a game-provided key.</summary>
    public sealed class PlayerPrefsLocalePersistence : ILocalePersistence
    {
        private readonly string _key;

        public PlayerPrefsLocalePersistence(string playerPrefsKey)
        {
            if (string.IsNullOrEmpty(playerPrefsKey))
                throw new ArgumentException("A non-empty PlayerPrefs key is required.", nameof(playerPrefsKey));
            _key = playerPrefsKey;
        }

        public string Load()
        {
            var value = PlayerPrefs.GetString(_key, null);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public void Save(string localeCode) => PlayerPrefs.SetString(_key, localeCode ?? string.Empty);
    }
}
```

- [ ] **Step 3: LocaleService.cs** (full code — thin orchestrator over Unity Localization; the LocalizationSettings touches are review-verified, the decision + persistence are delegated to the tested pure pieces):

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TK.Localization
{
    /// <summary>
    /// Locale selection + persistence over Unity Localization. Picks saved → device → first available on init,
    /// persists on change, exposes culture/RTL/change-event. Main-thread-affine. Never throws on an unknown code.
    /// </summary>
    public sealed class LocaleService
    {
        private readonly ILocalePersistence _persistence;

        public LocaleService(ILocalePersistence persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public event Action<Locale> OnLocaleChanged;

        public IReadOnlyList<Locale> Available => LocalizationSettings.AvailableLocales.Locales;
        public Locale Current => LocalizationSettings.SelectedLocale;
        public CultureInfo CurrentCulture =>
            Current != null ? Current.Identifier.CultureInfo : CultureInfo.InvariantCulture;
        public bool IsRtl => CurrentCulture.TextInfo.IsRightToLeft;

        public async Task InitializeAsync()
        {
            await LocalizationSettings.InitializationOperation.Task;
            var codes = ToCodes(Available);
            var device = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var chosen = LocaleSelection.Choose(_persistence.Load(), device, codes);
            if (chosen != null) SetLocale(chosen);
        }

        public bool SetLocale(string localeCode)
        {
            var locale = FindAvailable(localeCode);
            if (locale == null) return false;
            SetLocale(locale);
            return true;
        }

        public void SetLocale(Locale locale)
        {
            if (locale == null) throw new ArgumentNullException(nameof(locale));
            LocalizationSettings.SelectedLocale = locale;
            _persistence.Save(locale.Identifier.Code);
            OnLocaleChanged?.Invoke(locale);
        }

        private Locale FindAvailable(string code)
        {
            var locales = Available;
            for (var i = 0; i < locales.Count; i++)
                if (locales[i] != null && locales[i].Identifier.Code == code) return locales[i];
            return null;
        }

        private static IReadOnlyList<string> ToCodes(IReadOnlyList<Locale> locales)
        {
            var codes = new List<string>(locales.Count);
            for (var i = 0; i < locales.Count; i++)
                if (locales[i] != null) codes.Add(locales[i].Identifier.Code);
            return codes;
        }
    }
}
```
RE-VERIFY at execution: `LocalizationSettings.AvailableLocales.Locales`, `LocalizationSettings.SelectedLocale`, `LocalizationSettings.InitializationOperation.Task`, `Locale.Identifier.Code/CultureInfo` against the installed package.

- [ ] **Step 4: FakeLocalePersistence.cs + tests** — `FakeLocalePersistence : ILocalePersistence` (an in-memory `string` field; records `SaveCount`). Then:
  - `LocaleSelectionTests.cs` (5): `Choose_SavedAvailable_ReturnsSaved`; `Choose_SavedUnavailable_DeviceAvailable_ReturnsDevice`; `Choose_NeitherAvailable_ReturnsFirst`; `Choose_EmptyAvailable_ReturnsNull`; `Choose_NullSaved_FallsToDeviceThenFirst`.
  - `PlayerPrefsLocalePersistenceTests.cs` (3): `Save_Then_Load_RoundTrips` (`[TearDown]` `PlayerPrefs.DeleteKey`); `Load_WhenUnset_ReturnsNull`; `Ctor_EmptyKey_Throws`.
  NOTE: `LocaleService`'s LocalizationSettings-touching members (`InitializeAsync`, `SetLocale`, `Available`, `Current`, `IsRtl`) are **review-verified** — they require a configured Localization project the harness does not provide; the tested pure pieces (`LocaleSelection`, persistence) are the decision + storage logic the service wires together. Do NOT stand up a full Localization config just to test the thin wiring.

- [ ] **Step 5: gate** (prev + 8). **Step 6: commit** — `feat(localization): add LocaleService with locale-selection policy and persistence seam`.

---

### Task 4: Components — FontLocalizer + LocalizedTmpText

**Files:**
- Create: `Runtime/Fonts/FontLocalizer.cs`, `Runtime/Text/LocalizedTmpText.cs`

**Interfaces:**
- Consumes: `LocaleFontMap`/`LocaleFontInfo`/`TmpFontApplier` (Task 1), `RtlText` (Task 2).
- Produces: two drop-on components. Mostly review-verified (they self-subscribe to Unity Localization runtime events, which the harness can't exercise without a configured project); `TmpFontApplier` (their apply core) is already tested in Task 1.

- [ ] **Step 1: FontLocalizer.cs** (full code — RE-VERIFY the event name `LocalizationSettings.SelectedLocaleChanged` against the installed package):

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TK.Localization
{
    /// <summary>
    /// Applies the active locale's font/material/direction to this TMP_Text, and re-applies automatically when
    /// the selected locale changes. Handles the FONT only — the text/string is managed elsewhere (e.g. a
    /// LocalizeStringEvent, or code). Subscribes in OnEnable, unsubscribes in OnDisable (no persisted wiring).
    /// </summary>
    [AddComponentMenu("TK Localization/Font Localizer")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FontLocalizer : MonoBehaviour
    {
        [SerializeField] private LocaleFontMap _map;

        private TMP_Text _text;

        private void OnEnable()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            Apply();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale locale) => ApplyFor(locale);

        public void Apply() => ApplyFor(LocalizationSettings.SelectedLocale);

        private void ApplyFor(Locale locale)
        {
            if (_map == null || _text == null) return;
            TmpFontApplier.Apply(_text, _map.Resolve(locale));
        }
    }
}
```

- [ ] **Step 2: LocalizedTmpText.cs** (full code — RE-VERIFY `LocalizedString.StringChanged` + `RefreshString()` against the installed package):

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TK.Localization
{
    /// <summary>
    /// All-in-one localized TMP text: resolves a LocalizedString, RTL-shapes it when needed, writes it to the
    /// TMP_Text, and (if a font map is assigned) applies the locale font. Uses LocalizedString.StringChanged,
    /// which fires immediately and on every locale change — no LocalizeStringEvent wiring required.
    /// </summary>
    [AddComponentMenu("TK Localization/Localized TMP Text")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTmpText : MonoBehaviour
    {
        [SerializeField] private LocalizedString _string;
        [SerializeField] private LocaleFontMap _map;   // optional

        private TMP_Text _text;

        private void OnEnable()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            _string.StringChanged += OnStringChanged;
        }

        private void OnDisable()
        {
            _string.StringChanged -= OnStringChanged;
        }

        public void Refresh() => _string.RefreshString();

        private void OnStringChanged(string value)
        {
            if (_text == null) return;
            _text.text = RtlText.IsRtl(value) ? RtlText.Fix(value) : value;
            if (_map != null)
                TmpFontApplier.Apply(_text, _map.Resolve(LocalizationSettings.SelectedLocale));
        }
    }
}
```

- [ ] **Step 3: gate** — the components compile; TK.Localization.Tests count is unchanged from Task 3 (these are review-verified; no new unit tests — their apply core is `TmpFontApplier`, tested in Task 1). Confirm zero `error CS`/`warning CS` under the package. **Step 4: commit** — `feat(localization): add FontLocalizer and LocalizedTmpText components`.

---

### Task 5: Samples + docs + root README/ROADMAP + host wiring + final gate

**Files:**
- Create: `Packages/com.tk.localization/Samples~/FontSetup/README.md`
- Create: `Packages/com.tk.localization/Samples~/IntegrationExamples/LanguageSelectorExample.cs`, `LocaleBootstrapExample.cs`, `GameLocalizationFacadeExample.cs`, `README.md`
- Create: `Packages/com.tk.localization/README.md`, `CHANGELOG.md`
- Modify: `Packages/com.tk.localization/package.json` (samples array); root `README.md`; `ROADMAP.md`; HOST `Packages/manifest.json` (testables)

- [ ] **Step 1: IntegrationExamples samples** —
  - `LanguageSelectorExample.cs`: a generic `MonoBehaviour` (NOT `PopupBase`) that, given a `LocaleService`, lists `service.Available` and on a button calls `service.SetLocale(locale)`. Namespace `TK.Localization.Samples.IntegrationExamples` (mirror the existing sample namespace convention — check `Packages/com.tk.notification/Samples~/IntegrationExamples/*.cs`).
  - `LocaleBootstrapExample.cs`: `async void Start()` → `var service = new LocaleService(new PlayerPrefsLocalePersistence("your.game.localeCode")); await service.InitializeAsync();` with a comment that the key is the game's choice.
  - `GameLocalizationFacadeExample.cs`: a thin game-owned static façade over string tables (the cleaned-up equivalent of the reference's `LocalizationHelper`) — a couple of example table-name constants + a `Get(table, entry)` using `LocalizationSettings.StringDatabase.GetLocalizedString(table, entry)`, with a comment: table names + key conventions are the GAME's, they live here (in the game), not in the package.
  - Sample `README.md`: the wire-up one-liners for FontLocalizer / LocalizedTmpText / LocaleService.

- [ ] **Step 2: FontSetup sample** — `Samples~/FontSetup/README.md`: a walkthrough for building a `LocaleFontMap` (create `LocaleFontInfo` assets per locale with their TMP font + optional material + RTL flag; add entries; assign the mandatory Fallback), and attaching `FontLocalizer`/`LocalizedTmpText` to TMP texts.

- [ ] **Step 3: package README.md** — sections: **What's inside** (table: `LocaleFontMap`/`LocaleFontInfo`, `FontLocalizer`, `LocalizedTmpText`, `RtlText`, `LocaleService` + `ILocalePersistence`); **Install** (git URL `https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.localization` + tag pin `#com.tk.localization/0.1.0`; deps `com.unity.localization` + `com.unity.ugui`, both on Unity's default registry → NO scoped registry; the GAME sets up Localization Settings, string tables, locales, and font assets); **Quickstart** (attach `LocalizedTmpText` to a TMP text + assign a LocalizedString + a LocaleFontMap; bootstrap `LocaleService.InitializeAsync()`); **Fonts** (LocaleFontMap + mandatory Fallback; fallback chain uses the requested locale); **RTL** (`RtlText.IsRtl/Fix`; LocalizedTmpText auto-shapes; main-thread); **Locale selection** (`LocaleService`, game-provided persistence key, `IsRtl` via culture); **Gotchas** (main-thread; you provide the Unity Localization project config; `LocalizedTmpText` uses `LocalizedString.StringChanged` so no LocalizeStringEvent needed; `FontLocalizer` is font-only). `CHANGELOG.md`: keep-a-changelog `## [0.1.0] - 2026-07-05`.

- [ ] **Step 4: package.json samples array** — `[{ "displayName": "Font Setup", "description": "How to build a LocaleFontMap and wire the localizer components.", "path": "Samples~/FontSetup" }, { "displayName": "Integration Examples", "description": "Generic language selector, LocaleService bootstrap, and a game-owned string-table façade.", "path": "Samples~/IntegrationExamples" }]`.

- [ ] **Step 5: root README + ROADMAP + host wiring** — root `README.md`: add a `com.tk.localization` row (0.1.0, Unity 6000.0+, no scoped registries; deps `com.unity.localization` + `com.unity.ugui`) to the package table AND the Shipped table, its install URL, and `com.tk.localization/0.1.0` in the Versioning tag list (match existing format). `ROADMAP.md`: add `com.tk.localization` to the **Shipped** table (it was not a prior candidate — the user added it). HOST `Packages/manifest.json` (the repo's own project manifest): add `"com.tk.localization"` to `testables` (do NOT touch scopedRegistries/dependencies). VERIFY all existing README install pins still match their package.json versions (they were aligned earlier — a quick confirm, fix any drift).

- [ ] **Step 6: final gate** (all `TK.Localization.Tests` green; zero `com.tk.localization` warnings; the `Samples~` files are not compiled). Verify Samples~ tracked: `git check-ignore -v Packages/com.tk.localization/Samples~/IntegrationExamples/LocaleBootstrapExample.cs` must report **NOT ignored**. `git status` clean apart from known host churn — stage `Packages/packages-lock.json` if it records the new embedded package + its `com.unity.localization` resolution. **Step 7: commit** — `docs(localization): add samples, package docs and host wiring`. (Push + the `com.tk.localization/0.1.0` tag happen AFTER the final whole-branch review, in the finishing step.)

---

## Notes for the executor

- **Opus-only** for every implementer and reviewer subagent.
- The spec (`docs/specs/2026-07-05-tk-localization-design.md`) is the binding contract; if code and spec disagree, stop and reconcile.
- Between tasks: two-stage review (per-task + whole-branch at the end). The whole-branch review must confirm: deps are exactly `com.unity.localization` + `com.unity.ugui` (no Odin, no core); `LocaleFontMap.Resolve` uses the REQUESTED locale's fallbacks and never returns null while Fallback is set; the RTL pipeline was ported faithfully and is self-contained (no Odin/game/UnicoLibs leftovers); the components self-subscribe in OnEnable and unsubscribe in OnDisable (no persisted-listener wiring, no leaked handlers); `RtlText`/`LocaleSelection`/persistence/font-map logic are genuinely unit-tested; the Unity-Localization-touching parts are correct against the installed API.
- Task 4 adds no unit tests (components are review-verified; their apply core is tested in Task 1). Do NOT stand up a full Localization project to test thin subscription wiring.
- RE-VERIFY every Unity Localization / TMP member against the installed packages at execution; on drift, trust the installed source and note it.
- Do NOT push or tag mid-plan. Report the exact `TK.Localization.Tests` count after every gate.
