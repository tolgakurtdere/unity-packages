using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using TestLayout = TK.Core.Tests.TestUi.TestLayout;

namespace TK.Core.Tests
{
    /// <summary>
    /// Covers the synchronous paths of DefaultOrderedTabTransition (invalid input, immediate
    /// interrupt). The frame-driven motion loop needs real frames and is play-mode verified
    /// in the consuming game; the interrupt tests below still pin the load-bearing ordering:
    /// shouldInterrupt is polled BEFORE any motion is applied.
    /// </summary>
    [TestFixture]
    public class DefaultOrderedTabTransitionTests
    {
        private GameObject _containerGo;
        private RectTransform _container;

        [SetUp]
        public void SetUp()
        {
            _containerGo = new GameObject("Container", typeof(RectTransform));
            _container = (RectTransform)_containerGo.transform;
            _container.sizeDelta = new Vector2(800f, 600f); // deterministic width (no Screen.width fallback)
        }

        [TearDown]
        public void TearDown()
        {
            if (_containerGo) Object.DestroyImmediate(_containerGo);
            _containerGo = null;
        }

        private TestLayout CreateLayout(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(_container, worldPositionStays: false);
            return go.AddComponent<TestLayout>();
        }

        [Test]
        public async Task PlayAsync_NullLayouts_ReportsInterruptedAtStartPosition()
        {
            var transition = new DefaultOrderedTabTransition();

            var result = await transition.PlayAsync(null, 1.5f, 0, _container, null, null);

            Assert.IsFalse(result.Completed);
            Assert.AreEqual(1.5f, result.Position);
        }

        [Test]
        public async Task PlayAsync_TargetOutOfRange_ReportsInterruptedAtStartPosition()
        {
            var transition = new DefaultOrderedTabTransition();
            var layouts = new LayoutBase[] { CreateLayout("A") };

            var lowResult = await transition.PlayAsync(layouts, 0f, -1, _container, null, null);
            var highResult = await transition.PlayAsync(layouts, 0f, 1, _container, null, null);

            Assert.IsFalse(lowResult.Completed);
            Assert.IsFalse(highResult.Completed);
        }

        [Test]
        public async Task PlayAsync_DestroyedTarget_ReportsInterrupted()
        {
            var transition = new DefaultOrderedTabTransition();
            var a = CreateLayout("A");
            var b = CreateLayout("B");
            var layouts = new LayoutBase[] { a, b };
            Object.DestroyImmediate(b.gameObject);

            var result = await transition.PlayAsync(layouts, 0f, 1, _container, null, null);

            Assert.IsFalse(result.Completed);
        }

        [Test]
        public async Task PlayAsync_InterruptIsPolledBeforeAnyMotion()
        {
            var transition = new DefaultOrderedTabTransition();
            var a = CreateLayout("A");
            var b = CreateLayout("B");
            var layouts = new LayoutBase[] { a, b };
            var polls = 0;

            var result = await transition.PlayAsync(layouts, 0f, 1, _container, TabTransitionSettings.Default,
                () => { polls++; return true; });

            Assert.AreEqual(1, polls, "Interrupt must be polled exactly once, before the first motion frame.");
            Assert.IsFalse(result.Completed);
            Assert.AreEqual(0f, result.Position, "Interrupted before motion — reached position is the start.");
        }

        [Test]
        public async Task PlayAsync_ImmediateInterrupt_LeavesLayoutsAtStartOffsets()
        {
            var transition = new DefaultOrderedTabTransition();
            var a = CreateLayout("A");
            var b = CreateLayout("B");
            var layouts = new LayoutBase[] { a, b };

            await transition.PlayAsync(layouts, 0f, 1, _container, TabTransitionSettings.Default, () => true);

            // The single pre-loop ApplyPositions ran: A centred at the start position, B one
            // screen-width to the right, ready for the next retarget.
            Assert.AreEqual(0f, ((RectTransform)a.transform).anchoredPosition.x, 1e-3f);
            Assert.Greater(((RectTransform)b.transform).anchoredPosition.x, 0f);
        }
    }
}
