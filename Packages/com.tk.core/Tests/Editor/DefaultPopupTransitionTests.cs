using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;

namespace TK.Core.Tests
{
    [TestFixture]
    public class DefaultPopupTransitionTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public async Task ZeroDuration_ShowAndHide_ApplyEndStates()
        {
            _go = new GameObject("TransitionTarget", typeof(RectTransform), typeof(CanvasGroup));
            var container = _go.GetComponent<RectTransform>();
            var canvasGroup = _go.GetComponent<CanvasGroup>();
            var ctx = new UITransitionContext(canvasGroup, container, backgroundDisabler: null, duration: 0f);
            var transition = new DefaultPopupTransition();

            // Zero duration completes synchronously and must land on the END state, not the start state.
            await transition.PlayShowAsync(ctx);

            Assert.AreEqual(1f, canvasGroup.alpha);
            Assert.AreEqual(Vector3.one, container.localScale);

            await transition.PlayHideAsync(ctx);

            Assert.AreEqual(0f, canvasGroup.alpha);
            Assert.AreEqual(0.8f, container.localScale.x, 1e-4f);
        }
    }
}
