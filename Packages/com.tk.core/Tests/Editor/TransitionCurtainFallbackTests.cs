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
    }
}
