using NUnit.Framework;
using TK.Localization;
using TMPro;
using UnityEngine;

namespace TK.Localization.Tests
{
    public sealed class TmpFontApplierTests
    {
        private GameObject _go;
        private TextMeshProUGUI _text;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TmpTarget");
            _text = _go.AddComponent<TextMeshProUGUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Apply_SetsRightToLeftDirection()
        {
            var info = ScriptableObject.CreateInstance<LocaleFontInfo>();
            var so = new UnityEditor.SerializedObject(info);
            so.FindProperty("_rightToLeft").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            _text.isRightToLeftText = false;
            TmpFontApplier.Apply(_text, info);

            Assert.IsTrue(_text.isRightToLeftText);
            Object.DestroyImmediate(info);
        }

        [Test]
        public void Apply_NullMaterialAndNullArgs_DoNotThrowAndLeaveMaterialUntouched()
        {
            // info with no font, no material, RTL=false (all defaults).
            var info = ScriptableObject.CreateInstance<LocaleFontInfo>();
            var originalMaterial = _text.fontSharedMaterial;

            Assert.DoesNotThrow(() => TmpFontApplier.Apply(_text, info));
            Assert.AreSame(originalMaterial, _text.fontSharedMaterial,
                "Null material in info must leave fontSharedMaterial untouched.");

            // Null-safety on both arguments.
            Assert.DoesNotThrow(() => TmpFontApplier.Apply(_text, null));
            Assert.DoesNotThrow(() => TmpFontApplier.Apply(null, info));

            Object.DestroyImmediate(info);
        }
    }
}
