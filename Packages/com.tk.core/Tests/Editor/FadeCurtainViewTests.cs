using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TK.Core.Tests
{
    [TestFixture]
    public class FadeCurtainViewTests
    {
        // EditMode never calls Awake on creation — expose it.
        private sealed class TestFadeCurtain : FadeCurtainView
        {
            public void RunAwake() => Awake();
        }

        private GameObject _go;
        private TestFadeCurtain _curtain;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Curtain", typeof(CanvasGroup), typeof(TestFadeCurtain));
            _curtain = _go.GetComponent<TestFadeCurtain>();
            _group = _go.GetComponent<CanvasGroup>();

            // Zero-duration = fully synchronous fade path (the EditMode-safe path).
            _curtain.ShowDuration = 0f;
            _curtain.HideDuration = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void Awake_StartsOpen_NotBlocking()
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;

            _curtain.RunAwake();

            Assert.AreEqual(0f, _group.alpha);
            Assert.IsFalse(_group.blocksRaycasts);
            Assert.IsFalse(_group.interactable);
        }

        [Test]
        public async Task ShowAsync_ZeroDuration_CoversAndBlocksSynchronously()
        {
            _curtain.RunAwake();

            await _curtain.ShowAsync();

            Assert.AreEqual(1f, _group.alpha, "ShowAsync must return with the screen fully covered.");
            Assert.IsTrue(_group.blocksRaycasts, "The curtain must swallow input while covered.");
        }

        [Test]
        public async Task HideAsync_ZeroDuration_OpensAndUnblocksSynchronously()
        {
            _curtain.RunAwake();
            await _curtain.ShowAsync();

            await _curtain.HideAsync();

            Assert.AreEqual(0f, _group.alpha);
            Assert.IsFalse(_group.blocksRaycasts);
        }

        [Test]
        public void Durations_ClampNegativeToZero()
        {
            _curtain.ShowDuration = -1f;
            _curtain.HideDuration = -2f;

            Assert.AreEqual(0f, _curtain.ShowDuration);
            Assert.AreEqual(0f, _curtain.HideDuration);
        }

        [Test]
        public void IsAssignableToTheSeam()
        {
            Assert.IsInstanceOf<ITransitionCurtainView>(_curtain);
            Assert.IsInstanceOf<TransitionCurtainView>(_curtain);
        }

        [Test]
        public void ShowInstantly_SnapsCoveredSynchronously()
        {
            _curtain.RunAwake();

            _curtain.ShowInstantly();

            Assert.AreEqual(1f, _group.alpha, "ShowInstantly must return with the screen fully covered.");
            Assert.IsTrue(_group.blocksRaycasts);
        }

        [Test]
        public void HideInstantly_SnapsOpenSynchronously()
        {
            _curtain.RunAwake();
            _curtain.ShowInstantly();

            _curtain.HideInstantly();

            Assert.AreEqual(0f, _group.alpha);
            Assert.IsFalse(_group.blocksRaycasts);
        }
    }
}
