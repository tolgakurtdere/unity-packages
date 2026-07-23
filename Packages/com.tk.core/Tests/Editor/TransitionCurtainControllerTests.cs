using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.Core.Tests
{
    [TestFixture]
    public class TransitionCurtainControllerTests
    {
        private sealed class FakeCurtainView : ITransitionCurtainView
        {
            public int ShowCalls, HideCalls;
            public bool Covered;
            public Exception ThrowOnShow;
            public AwaitableCompletionSource PendingShow; // set to make ShowAsync wait

            public Awaitable ShowAsync()
            {
                ShowCalls++;
                if (ThrowOnShow != null) throw ThrowOnShow;
                if (PendingShow != null) return PendingShow.Awaitable;
                Covered = true;
                return TestAwaitables.Completed();
            }

            public Awaitable HideAsync()
            {
                HideCalls++;
                Covered = false;
                return TestAwaitables.Completed();
            }
        }

        private FakeCurtainView _view;
        private int _resolveCalls, _pushes, _pops;
        private readonly List<float> _requestedWaits = new();
        private AwaitableCompletionSource _pendingWait;

        private TransitionCurtainController NewController(bool fakeWait = false) => new(
            resolveView: () =>
            {
                _resolveCalls++;
                var source = new AwaitableCompletionSource<ITransitionCurtainView>();
                source.SetResult(_view);
                return source.Awaitable;
            },
            onCoverBegin: () => _pushes++,
            onOpenEnd: () => _pops++,
            waitSeconds: fakeWait
                ? seconds => { _requestedWaits.Add(seconds); _pendingWait = new AwaitableCompletionSource(); return _pendingWait.Awaitable; }
                : null);

        [SetUp]
        public void SetUp()
        {
            _view = new FakeCurtainView();
            _resolveCalls = _pushes = _pops = 0;
            _requestedWaits.Clear();
            _pendingWait = null;
        }

        [Test]
        public async Task RunAsync_CoversBeforeWork_OpensAfter()
        {
            var controller = NewController();
            var coveredDuringWork = false;

            await controller.RunAsync(() =>
            {
                coveredDuringWork = _view.Covered;
                return TestAwaitables.Completed();
            });

            Assert.IsTrue(coveredDuringWork, "Work must only run behind a fully covered curtain.");
            Assert.IsFalse(_view.Covered);
            Assert.AreEqual(1, _view.ShowCalls);
            Assert.AreEqual(1, _view.HideCalls);
            Assert.IsFalse(controller.IsCovered);
        }

        [Test]
        public async Task RunAsync_Nested_ShowsOnce_HidesOnce()
        {
            var controller = NewController();

            await controller.RunAsync(() => controller.RunAsync(() => TestAwaitables.Completed()));

            Assert.AreEqual(1, _view.ShowCalls);
            Assert.AreEqual(1, _view.HideCalls);
            Assert.AreEqual(1, _resolveCalls, "The view must be resolved exactly once, then cached.");
        }

        [Test]
        public async Task RunAsync_WorkThrows_StillOpens_AndPropagates()
        {
            var controller = NewController();
            var caught = false;

            try { await controller.RunAsync(() => throw new InvalidOperationException("boom")); }
            catch (InvalidOperationException) { caught = true; }

            Assert.IsTrue(caught, "The work's exception must propagate to the caller.");
            Assert.AreEqual(1, _view.HideCalls, "An exception must still reopen the curtain.");
            Assert.IsFalse(controller.IsCovered);
        }

        [Test]
        public async Task RunAsync_ShowThrows_WorkNeverRuns_StateResets()
        {
            var controller = NewController();
            _view.ThrowOnShow = new InvalidOperationException("show-fail");
            var workRan = false;
            var caught = false;

            try { await controller.RunAsync(() => { workRan = true; return TestAwaitables.Completed(); }); }
            catch (InvalidOperationException) { caught = true; }

            Assert.IsTrue(caught);
            Assert.IsFalse(workRan, "Work must not run when the curtain failed to cover.");
            Assert.AreEqual(_pushes, _pops, "Suppression must balance on the failure path.");

            // The controller must have fully reset: a later run works.
            _view.ThrowOnShow = null;
            await controller.RunAsync(() => TestAwaitables.Completed());
            Assert.IsFalse(controller.IsCovered);
        }

        [Test]
        public async Task Suppression_OnePushOnePop_PerCycle_EvenNested()
        {
            var controller = NewController();

            await controller.RunAsync(() => controller.RunAsync(() => TestAwaitables.Completed()));

            Assert.AreEqual(1, _pushes);
            Assert.AreEqual(1, _pops);
        }

        [Test]
        public async Task ManualShowHide_RefCounted()
        {
            var controller = NewController();

            await controller.ShowAsync();
            await controller.ShowAsync();
            Assert.AreEqual(1, _view.ShowCalls);
            Assert.IsTrue(controller.IsCovered);

            await controller.HideAsync();
            Assert.IsTrue(controller.IsCovered, "The second holder must keep the curtain closed.");
            Assert.AreEqual(0, _view.HideCalls);

            await controller.HideAsync();
            Assert.IsFalse(controller.IsCovered);
            Assert.AreEqual(1, _view.HideCalls);
        }

        [Test]
        public async Task Hide_WithoutShow_WarnsAndIgnores()
        {
            var controller = NewController();

            LogAssert.Expect(LogType.Warning, "[TransitionCurtain] Hide without a matching Show — ignored.");
            await controller.HideAsync();

            Assert.AreEqual(0, _view.HideCalls);
        }

        [Test]
        public async Task ShowWhileCovering_WaitsForCover_SingleViewShow()
        {
            var controller = NewController();
            _view.PendingShow = new AwaitableCompletionSource();

            var first = controller.ShowAsync();
            var second = controller.ShowAsync();
            var secondDone = false;
            var track = Track(second, () => secondDone = true);

            Assert.IsFalse(secondDone, "A Show during the cover animation must wait for full cover.");

            _view.Covered = true;
            _view.PendingShow.SetResult();
            await first;
            await track;

            Assert.IsTrue(secondDone);
            Assert.AreEqual(1, _view.ShowCalls);

            await controller.HideAsync();
            await controller.HideAsync();
        }

        [Test]
        public async Task MinCover_HoldsCurtain_UntilTimerElapses()
        {
            var controller = NewController(fakeWait: true);
            var runDone = false;
            var run = controller.RunAsync(() => TestAwaitables.Completed(), minCoverSeconds: 5f);
            var track = Track(run, () => runDone = true);

            Assert.AreEqual(new[] { 5f }, _requestedWaits.ToArray());
            Assert.IsFalse(runDone, "Instant work must not open the curtain before the min-cover hold.");
            Assert.AreEqual(0, _view.HideCalls);

            _pendingWait.SetResult();
            await track;   // Track is the ONLY awaiter of `run` — Awaitable is single-consumer.

            Assert.IsTrue(runDone);
            Assert.AreEqual(1, _view.HideCalls);
        }

        [Test]
        public async Task MinCover_ZeroDefault_NeverRequestsWait()
        {
            var controller = NewController(fakeWait: true);

            await controller.RunAsync(() => TestAwaitables.Completed());

            Assert.IsEmpty(_requestedWaits);
        }

        [Test]
        public async Task MinCover_WorkThrows_SkipsRemainingHold()
        {
            var controller = NewController(fakeWait: true);
            var caught = false;

            try { await controller.RunAsync(() => throw new InvalidOperationException("boom"), minCoverSeconds: 5f); }
            catch (InvalidOperationException) { caught = true; }

            Assert.IsTrue(caught);
            Assert.AreEqual(1, _view.HideCalls, "An exception must reopen immediately, not sit out the hold.");
        }

        // Awaitable is single-consumer: observe completion via a side task without also awaiting
        // the original in the assertion path.
        private static async Task Track(Awaitable awaitable, Action onDone)
        {
            await awaitable;
            onDone();
        }
    }
}
