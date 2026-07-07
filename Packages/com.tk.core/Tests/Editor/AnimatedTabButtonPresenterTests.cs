using System.Text.RegularExpressions;
using NUnit.Framework;
using TK.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TK.Core.Tests
{
    /// <summary>
    /// Frame-free coverage via the ApplyProgress seam (the async loop parks on its first frame
    /// await in EditMode, so progress application is driven manually and deterministically).
    /// Label (TMP) channels stay untested here — the suite avoids TMP instantiation; they are
    /// play-mode verified in the consuming game.
    /// </summary>
    [TestFixture]
    public class AnimatedTabButtonPresenterTests
    {
        private sealed class TestPresenter : AnimatedTabButtonPresenter
        {
            public void ApplyPublic(float easedProgress) => ApplyProgress(easedProgress);
        }

        private GameObject _go;
        private TestPresenter _presenter;
        private Button _button;
        private Image _image;
        private LayoutElement _layoutElement;
        private Sprite _normalSprite;
        private Sprite _selectedSprite;

        private static readonly Color NormalColor = Color.red;
        private static readonly Color SelectedColor = Color.green;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TabButton", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            ((RectTransform)_go.transform).pivot = new Vector2(0.5f, 0f);
            _image = _go.GetComponent<Image>();
            _button = _go.GetComponent<Button>();
            _layoutElement = _go.GetComponent<LayoutElement>();
            _presenter = _go.AddComponent<TestPresenter>();

            _normalSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _selectedSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

            Configure(("normalBackgroundSprite", _normalSprite), ("selectedBackgroundSprite", _selectedSprite));
            var so = new SerializedObject(_presenter);
            so.FindProperty("normalBackgroundColor").colorValue = NormalColor;
            so.FindProperty("selectedBackgroundColor").colorValue = SelectedColor;
            so.FindProperty("normalWidth").floatValue = 100f;
            so.FindProperty("selectedWidth").floatValue = 160f;
            so.FindProperty("selectedScale").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void Configure(params (string property, Object value)[] refs)
        {
            var so = new SerializedObject(_presenter);
            foreach (var (property, value) in refs)
                so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            if (_normalSprite) Object.DestroyImmediate(_normalSprite);
            if (_selectedSprite) Object.DestroyImmediate(_selectedSprite);
            _go = null;
        }

        private void InitializeNormal() => _presenter.Initialize(new TabButtonData("Home", "HOME", 0), _button);

        [Test]
        public void InstantSelect_SnapsEveryChannel()
        {
            InitializeNormal(); // ends in instant-normal state

            Assert.AreEqual(NormalColor, _image.color);
            Assert.AreSame(_normalSprite, _image.sprite);
            Assert.AreEqual(100f, _layoutElement.preferredWidth);
            Assert.AreEqual(1f, _go.transform.localScale.x, 1e-4f);

            _presenter.SetSelected(true, instant: true);

            Assert.AreEqual(SelectedColor, _image.color);
            Assert.AreSame(_selectedSprite, _image.sprite);
            Assert.AreEqual(160f, _layoutElement.preferredWidth);
            Assert.AreEqual(1.2f, _go.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void NullSprites_LeaveTheCurrentSpriteUntouched()
        {
            Configure(("normalBackgroundSprite", null), ("selectedBackgroundSprite", null));
            _image.sprite = _normalSprite; // pretend the prefab's own art

            InitializeNormal();
            _presenter.SetSelected(true, instant: true);

            Assert.AreSame(_normalSprite, _image.sprite, "Null sprite fields must not clear the prefab's art.");
        }

        [Test]
        public void Retarget_CapturesFromTheCurrentLiveValues()
        {
            InitializeNormal();

            _presenter.SetSelected(true, instant: false); // loop parks on its first frame await
            _presenter.ApplyPublic(0f);
            Assert.AreEqual(100f, _layoutElement.preferredWidth, 0.5f, "Progress 0 must equal the FROM (normal) state.");
            _presenter.ApplyPublic(1f);
            Assert.AreEqual(160f, _layoutElement.preferredWidth, 0.5f);

            // Land mid-flight, then retarget back to normal: FROM must be the mid values.
            _presenter.ApplyPublic(0.5f);
            var midWidth = _layoutElement.preferredWidth;
            var midScale = _go.transform.localScale.x;
            Assert.AreEqual(130f, midWidth, 0.5f);

            _presenter.SetSelected(false, instant: false);
            _presenter.ApplyPublic(0f);

            Assert.AreEqual(midWidth, _layoutElement.preferredWidth, 0.5f, "Retarget must start from the on-screen values.");
            Assert.AreEqual(midScale, _go.transform.localScale.x, 1e-3f);

            _presenter.ApplyPublic(1f);
            Assert.AreEqual(100f, _layoutElement.preferredWidth, 0.5f, "…and finish at the new target.");
        }

        [Test]
        public void Overshoot_LerpsScaleAndWidthUnclamped_ButClampsColors()
        {
            InitializeNormal();
            _presenter.SetSelected(true, instant: false);

            _presenter.ApplyPublic(1.1f);

            Assert.Greater(_layoutElement.preferredWidth, 160f, "Width must overshoot past its target.");
            Assert.Greater(_go.transform.localScale.x, 1.2f, "Scale must overshoot past its target.");
            Assert.AreEqual(SelectedColor, _image.color, "Colors must clamp at the endpoint.");
        }

        [Test]
        public void Initialize_WarnsWhenThePivotIsNotBottomCentre()
        {
            ((RectTransform)_go.transform).pivot = new Vector2(0.5f, 0.5f);

            LogAssert.Expect(LogType.Warning, new Regex(@"scale grows from the pivot"));
            InitializeNormal();
        }

        [Test]
        public void Initialize_WarnsAndSkipsWidth_WithoutALayoutElement()
        {
            Object.DestroyImmediate(_layoutElement);

            LogAssert.Expect(LogType.Warning, new Regex(@"width animation needs a LayoutElement"));
            InitializeNormal();

            Assert.DoesNotThrow(() => _presenter.SetSelected(true, instant: true));
            Assert.AreEqual(1.2f, _go.transform.localScale.x, 1e-4f, "The other channels keep working.");
        }

        [Test]
        public void Initialize_AppliesTheConfigIcon()
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_go.transform, worldPositionStays: false);
            var iconImage = iconGo.GetComponent<Image>();
            Configure(("icon", iconImage));

            _presenter.Initialize(new TabButtonData("Home", "HOME", 0, _selectedSprite), _button);

            Assert.AreSame(_selectedSprite, iconImage.sprite);
        }
    }
}
