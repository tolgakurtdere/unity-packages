using NUnit.Framework;
using TK.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.TestTools;

namespace TK.Localization.Tests
{
    public sealed class LocaleFontMapTests
    {
        private LocaleFontInfo _fontA;
        private LocaleFontInfo _fallback;
        private LocaleFontMap _map;

        [SetUp]
        public void SetUp()
        {
            _fontA = ScriptableObject.CreateInstance<LocaleFontInfo>();
            _fallback = ScriptableObject.CreateInstance<LocaleFontInfo>();
            _map = ScriptableObject.CreateInstance<LocaleFontMap>();
            _map.name = "TestMap";
        }

        [TearDown]
        public void TearDown()
        {
            if (_fontA != null) Object.DestroyImmediate(_fontA);
            if (_fallback != null) Object.DestroyImmediate(_fallback);
            if (_map != null) Object.DestroyImmediate(_map);
        }

        // --- SerializedObject helpers: populate private [SerializeField] state without a runtime seam. ---

        private static void SetFallback(LocaleFontMap map, LocaleFontInfo fallback)
        {
            var so = new SerializedObject(map);
            so.FindProperty("_fallback").objectReferenceValue = fallback;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddEntry(LocaleFontMap map, Locale locale, LocaleFontInfo font)
        {
            var so = new SerializedObject(map);
            var entries = so.FindProperty("_entries");
            var index = entries.arraySize;
            entries.arraySize = index + 1;
            var element = entries.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Locale").objectReferenceValue = locale;
            element.FindPropertyRelative("Font").objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Resolve_MappedLocale_ReturnsItsFont()
        {
            var tr = Locale.CreateLocale("tr");
            SetFallback(_map, _fallback);
            AddEntry(_map, tr, _fontA);

            Assert.AreSame(_fontA, _map.Resolve(Locale.CreateLocale("tr")));
        }

        [Test]
        public void Resolve_UnmappedLocale_ReturnsFallback()
        {
            SetFallback(_map, _fallback);
            AddEntry(_map, Locale.CreateLocale("tr"), _fontA);

            Assert.AreSame(_fallback, _map.Resolve(Locale.CreateLocale("ja")));
        }

        [Test]
        public void Resolve_NullLocale_ReturnsFallback()
        {
            SetFallback(_map, _fallback);

            Assert.AreSame(_fallback, _map.Resolve(null));
        }

        [Test]
        public void Resolve_NoFallbackAssigned_LogsErrorAndReturnsNull()
        {
            // No fallback assigned.
            LogAssert.Expect(LogType.Error,
                "[TK.Localization] LocaleFontMap 'TestMap' has no Fallback assigned; returning null.");

            Assert.IsNull(_map.Resolve(Locale.CreateLocale("ja")));
        }

        [Test]
        public void Resolve_NeverReturnsNull_WhenFallbackSet()
        {
            SetFallback(_map, _fallback);
            AddEntry(_map, Locale.CreateLocale("tr"), _fontA);

            foreach (var code in new[] { "ja", "de", "fr", "ar", "zz" })
                Assert.AreSame(_fallback, _map.Resolve(Locale.CreateLocale(code)),
                    $"Expected fallback for unmapped locale '{code}'.");
        }
    }
}
