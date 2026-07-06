using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.App;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.Core.Tests
{
    [TestFixture]
    public class NavigationGateTests
    {
        [Test]
        public async Task RunAsync_LocksWhileRunning_AndReleasesAfter()
        {
            var gate = new NavigationGate();
            var source = new AwaitableCompletionSource();

            var run = gate.RunAsync(() => source.Awaitable);

            Assert.IsTrue(gate.IsTransitioning);
            Assert.IsFalse(gate.CanNavigate);

            source.SetResult();
            await run;

            Assert.IsFalse(gate.IsTransitioning);
            Assert.IsTrue(gate.CanNavigate);
        }

        [Test]
        public async Task RunAsync_ReentrantCall_IsDropped()
        {
            var gate = new NavigationGate();
            var source = new AwaitableCompletionSource();
            var secondRan = false;

            var first = gate.RunAsync(() => source.Awaitable);
            await gate.RunAsync(() =>
            {
                secondRan = true;
                return TestAwaitables.Completed();
            });

            Assert.IsFalse(secondRan, "A call made while the gate is held must be dropped, not queued.");

            source.SetResult();
            await first;
        }

        [Test]
        public async Task RunAsync_OperationThrows_LogsAndReleasesGate()
        {
            var gate = new NavigationGate();

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            await gate.RunAsync(() => throw new InvalidOperationException("boom"));

            Assert.IsTrue(gate.CanNavigate, "An exception must release the gate.");

            var ranAfter = false;
            await gate.RunAsync(() =>
            {
                ranAfter = true;
                return TestAwaitables.Completed();
            });
            Assert.IsTrue(ranAfter);
        }
    }
}
