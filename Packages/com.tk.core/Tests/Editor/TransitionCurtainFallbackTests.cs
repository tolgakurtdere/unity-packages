using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TK.Core.Tests
{
    [TestFixture]
    public class TransitionCurtainFallbackTests
    {
        private GameObject _parentGo;

        [SetUp]
        public void SetUp()
        {
            _parentGo = new GameObject("Container", typeof(RectTransform));
            // Pre-existing siblings — the curtain must slot in BELOW them (first sibling).
            new GameObject("TaskOverlay", typeof(RectTransform)).transform.SetParent(_parentGo.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_parentGo) Object.DestroyImmediate(_parentGo);
            _parentGo = null;
        }

        [Test]
        public void CreateFallbackCurtain_BuildsAFullStretchBlackBlockingCurtain()
        {
            var curtain = UIManager.CreateFallbackCurtain(_parentGo.transform);

            Assert.IsNotNull(curtain);
            var rect = (RectTransform)curtain.transform;
            Assert.AreEqual(_parentGo.transform, rect.parent);
            Assert.AreEqual(0, rect.GetSiblingIndex(), "The curtain must render below the TaskOverlay.");
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.AreEqual(Vector2.zero, rect.offsetMin);
            Assert.AreEqual(Vector2.zero, rect.offsetMax);

            var image = curtain.GetComponent<Image>();
            Assert.AreEqual(Color.black, image.color);
            Assert.IsTrue(image.raycastTarget, "The curtain must swallow clicks while covered.");
        }

        [Test]
        public void CreateFallbackCurtain_StartsOpen()
        {
            var curtain = UIManager.CreateFallbackCurtain(_parentGo.transform);

            var group = curtain.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, group.alpha, "EditMode never runs Awake — the factory itself must produce the open state.");
            Assert.IsFalse(group.blocksRaycasts);
            Assert.IsFalse(group.interactable);
        }

        [Test]
        public async Task CoverCurtainInstantly_FallbackPath_CoversInTheSameCall()
        {
            // The UIManager transform must be a RectTransform: the resolve path parents the
            // fallback curtain under it when no taskOverlayContainer is assigned.
            var managerGo = new GameObject("UIManager", typeof(RectTransform), typeof(UIManager));
            try
            {
                var manager = managerGo.GetComponent<UIManager>();

                var cover = manager.CoverCurtainInstantlyAsync();

                // No catalog → synchronous fallback creation + ShowInstantly: covered before any await.
                var curtain = managerGo.transform.Find("TransitionCurtain (Default)");
                Assert.IsNotNull(curtain, "The fallback curtain must exist synchronously.");
                var group = curtain.GetComponent<CanvasGroup>();
                Assert.AreEqual(1f, group.alpha, "Covered in the same call — the boot guarantee.");
                Assert.IsTrue(group.blocksRaycasts);
                Assert.IsFalse(manager.IsBackInputActive, "Back input must be suppressed while covered.");

                await cover;
            }
            finally
            {
                // Covered curtain holds a back-input suppression on this manager instance; the
                // instance dies with the GameObject, so no cross-test state leaks. Do NOT hide
                // here — the default FadeCurtainView durations would await real frames.
                Object.DestroyImmediate(managerGo);
            }
        }
    }
}
