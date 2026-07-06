using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using Object = UnityEngine.Object;
using TestLayout = TK.Core.Tests.TestUi.TestLayout;

namespace TK.Core.Tests
{
    [TestFixture]
    public class LayoutSlideNavigatorTests
    {
        /// <summary>Scripted transition: returns a fixed result without any frames.</summary>
        private sealed class FakeOrderedTransition : IOrderedTabTransition
        {
            public TabTransitionResult Result = TabTransitionResult.CompletedAt(0);
            public int PlayCalls;
            public float LastStartPosition = float.NaN;
            public int LastTargetIndex = -1;

            public Awaitable<TabTransitionResult> PlayAsync(IReadOnlyList<LayoutBase> orderedLayouts,
                float startPosition, int targetIndex, RectTransform container, TabTransitionSettings settings,
                Func<bool> shouldInterrupt)
            {
                PlayCalls++;
                LastStartPosition = startPosition;
                LastTargetIndex = targetIndex;
                var source = new AwaitableCompletionSource<TabTransitionResult>();
                source.SetResult(Result);
                return source.Awaitable;
            }
        }

        private GameObject _uiManagerGo;
        private GameObject _containerGo;
        private RectTransform _container;
        private LayoutSlideNavigator _navigator;
        private FakeOrderedTransition _fake;

        [SetUp]
        public void SetUp()
        {
            // LayoutBase.OnUIEnter/OnUIExit route through UIManager.Instance (found by type).
            _uiManagerGo = new GameObject("UIManager", typeof(UIManager));
            _containerGo = new GameObject("Container", typeof(RectTransform));
            _container = (RectTransform)_containerGo.transform;
            _container.sizeDelta = new Vector2(800f, 600f); // deterministic width (no Screen.width fallback)
            _navigator = new LayoutSlideNavigator();
            _fake = new FakeOrderedTransition();
            _navigator.OrderedTransition = _fake;
        }

        [TearDown]
        public void TearDown()
        {
            if (_containerGo) Object.DestroyImmediate(_containerGo);
            if (_uiManagerGo) Object.DestroyImmediate(_uiManagerGo);
            _containerGo = null;
            _uiManagerGo = null;
        }

        private TestLayout CreateLayout(string name, string registerKey = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(_container, worldPositionStays: false);
            var layout = go.AddComponent<TestLayout>();
            layout.InvokeAwake();
            if (registerKey != null) _navigator.Register(registerKey, layout);
            return layout;
        }

        private static RectTransform Rect(LayoutBase layout) => (RectTransform)layout.transform;

        [Test]
        public void Register_TracksLayoutsAndCapturesContainerFromFirstRegistration()
        {
            var a = CreateLayout("A", "a");
            var b = CreateLayout("B", "b");

            Assert.AreSame(_container, _navigator.Container);
            Assert.IsTrue(_navigator.TryGet("a", out var fetched));
            Assert.AreSame(a, fetched);
            Assert.IsTrue(_navigator.IsRegistered(b));
            Assert.IsFalse(_navigator.TryGet("missing", out _));
            Assert.IsFalse(_navigator.IsRegistered(null));
        }

        [Test]
        public void SetCurrent_AdoptsLayoutShownThroughAnotherPath()
        {
            var a = CreateLayout("A", "a");

            _navigator.SetCurrent(a);

            Assert.AreSame(a, _navigator.Current);
            Assert.IsFalse(_navigator.HasVisualPosition, "Adoption without an index has no visual position.");

            _navigator.SetCurrent(a, 2);

            Assert.AreEqual(2f, _navigator.VisualPosition);
            Assert.IsTrue(_navigator.HasVisualPosition);

            _navigator.SetCurrent(a, -1);

            Assert.IsFalse(_navigator.HasVisualPosition, "A negative index means 'not on the ordered strip'.");
        }

        [Test]
        public void SetLayoutsInteractable_TogglesRaycastBlockingOnEveryRegisteredLayout()
        {
            var a = CreateLayout("A", "a");
            var b = CreateLayout("B", "b");

            _navigator.SetLayoutsInteractable(false);

            Assert.IsFalse(a.ExposedCanvasGroup.blocksRaycasts);
            Assert.IsFalse(b.ExposedCanvasGroup.blocksRaycasts);

            _navigator.SetLayoutsInteractable(true);

            Assert.IsTrue(a.ExposedCanvasGroup.blocksRaycasts);
            Assert.IsTrue(b.ExposedCanvasGroup.blocksRaycasts);
        }

        [Test]
        public async Task SlideThroughAsync_Completed_ArrivesAndHidesEveryOtherRegisteredLayout()
        {
            var a = CreateLayout("A", "a");
            var b = CreateLayout("B", "b");
            var c = CreateLayout("C", "c"); // registered but OUTSIDE the slide range
            Rect(c).anchoredPosition = new Vector2(500f, 0f); // leftover offset from an earlier interrupt
            await c.SetActivationStateAsync(true, skipAnimations: true);
            _fake.Result = TabTransitionResult.CompletedAt(1);

            var completed = await _navigator.SlideThroughAsync(new LayoutBase[] { a, b, c }, 0f, 1);

            Assert.IsTrue(completed);
            Assert.AreSame(b, _navigator.Current);
            Assert.AreEqual(1f, _navigator.VisualPosition);
            Assert.IsTrue(b.IsShown);
            Assert.AreEqual(Vector2.zero, Rect(b).anchoredPosition, "The arrived layout is re-centred.");
            Assert.IsFalse(a.IsShown, "Completed slides hide all registered layouts except the target.");
            Assert.IsFalse(c.IsShown, "…including layouts outside the slide range (interrupt-leftover cleanup).");
            Assert.AreEqual(Vector2.zero, Rect(c).anchoredPosition);
            Assert.AreEqual(0f, _fake.LastStartPosition);
            Assert.AreEqual(1, _fake.LastTargetIndex);
        }

        [Test]
        public async Task SlideThroughAsync_Interrupted_KeepsOffsetsAndNeverAdoptsTheAbandonedTarget()
        {
            var a = CreateLayout("A", "a");
            var b = CreateLayout("B", "b");
            await a.SetActivationStateAsync(true, skipAnimations: true);
            _navigator.SetCurrent(a, 0);
            _fake.Result = TabTransitionResult.InterruptedAt(0.4f);

            var completed = await _navigator.SlideThroughAsync(new LayoutBase[] { a, b }, 0f, 1);

            Assert.IsFalse(completed);
            Assert.AreSame(a, _navigator.Current, "Current must never point at an abandoned slide target.");
            Assert.AreEqual(0.4f, _navigator.VisualPosition, "The fractional position is kept for retargeting.");
            Assert.IsTrue(_navigator.HasVisualPosition);
            Assert.IsTrue(b.IsShown, "Interrupted slides keep layouts on screen — no settle.");
            Assert.Greater(Rect(b).anchoredPosition.x, 0f, "Offsets are kept so the next slide retargets seamlessly.");
        }

        [Test]
        public async Task SlideThroughAsync_AlreadyAtTarget_ArrivesWithoutRunningTheTransition()
        {
            var a = CreateLayout("A", "a");
            var b = CreateLayout("B", "b");

            var completed = await _navigator.SlideThroughAsync(new LayoutBase[] { a, b }, 1f, 1);

            Assert.IsTrue(completed);
            Assert.AreSame(b, _navigator.Current);
            Assert.IsTrue(b.IsShown);
            Assert.AreEqual(0, _fake.PlayCalls, "No motion needed — the transition must not run.");
        }

        [Test]
        public async Task SlideThroughAsync_InvalidTarget_FailsWithoutTouchingState()
        {
            var a = CreateLayout("A", "a");

            var tooHigh = await _navigator.SlideThroughAsync(new LayoutBase[] { a }, 0f, 5);
            var negative = await _navigator.SlideThroughAsync(new LayoutBase[] { a }, 0f, -1);
            var nullList = await _navigator.SlideThroughAsync(null, 0f, 0);

            Assert.IsFalse(tooHigh);
            Assert.IsFalse(negative);
            Assert.IsFalse(nullList);
            Assert.IsFalse(_navigator.HasVisualPosition);
            Assert.IsNull(_navigator.Current);
            Assert.AreEqual(0, _fake.PlayCalls);
        }

        [Test]
        public async Task SettleAsync_RecentresCurrentAndCleansUpAbandonedSlides()
        {
            var a = CreateLayout("A", "a");
            var b = CreateLayout("B", "b");
            await a.SetActivationStateAsync(true, skipAnimations: true);
            _navigator.SetCurrent(a, 0);
            // Simulate an interrupted slide that was then abandoned (e.g. a failed load).
            await b.SetActivationStateAsync(true, skipAnimations: true);
            Rect(b).anchoredPosition = new Vector2(300f, 0f);
            Rect(a).anchoredPosition = new Vector2(-120f, 0f);

            await _navigator.SettleAsync();

            Assert.IsFalse(_navigator.HasVisualPosition);
            Assert.IsTrue(a.IsShown, "The last fully shown layout stays.");
            Assert.AreEqual(Vector2.zero, Rect(a).anchoredPosition, "…and is re-centred.");
            Assert.IsFalse(b.IsShown, "Everything else is hidden.");
            Assert.AreEqual(Vector2.zero, Rect(b).anchoredPosition);
        }
    }
}
