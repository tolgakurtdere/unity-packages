using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TK.Core.Tests
{
    /// <summary>
    /// Covers the color-swap contract without TMP: the label reference is optional, so the
    /// presenter must work (and swap the background) when only an Image is present.
    /// </summary>
    [TestFixture]
    public class DefaultTabButtonPresenterTests
    {
        private static readonly Color SelectedBackground = new(0.357f, 0.357f, 0.839f);
        private static readonly Color NormalBackground = new(0.937f, 0.929f, 0.973f);

        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void Initialize_ResolvesTheButtonImageAndAppliesTheNormalState()
        {
            _go = new GameObject("TabButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var presenter = _go.AddComponent<DefaultTabButtonPresenter>();

            presenter.Initialize(new TabButtonData("Home", "HOME", 0), _go.GetComponent<Button>());

            Assert.AreEqual(NormalBackground, _go.GetComponent<Image>().color);
        }

        [Test]
        public void SetSelected_SwapsTheBackgroundColor()
        {
            _go = new GameObject("TabButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var presenter = _go.AddComponent<DefaultTabButtonPresenter>();
            presenter.Initialize(new TabButtonData("Home", "HOME", 0), _go.GetComponent<Button>());

            presenter.SetSelected(true, instant: false);
            Assert.AreEqual(SelectedBackground, _go.GetComponent<Image>().color);

            presenter.SetSelected(false, instant: false);
            Assert.AreEqual(NormalBackground, _go.GetComponent<Image>().color);
        }
    }
}
