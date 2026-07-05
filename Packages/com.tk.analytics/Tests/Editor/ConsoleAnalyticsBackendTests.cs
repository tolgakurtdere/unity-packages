using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TK.Analytics;

namespace TK.Analytics.Tests
{
    [TestFixture]
    public sealed class ConsoleAnalyticsBackendTests
    {
        // 1
        [Test]
        public void InitializeAsync_Completes()
        {
            var t = new ConsoleAnalyticsBackend().InitializeAsync();

            Assert.IsTrue(t.IsCompleted);
        }

        // 2
        [Test]
        public void LogEvent_WritesToConsole()
        {
            // The backend writes name and params on one console line, e.g. "[Analytics] event 'e' {n=3}".
            // A single regex asserts both fragments; two separate Expect calls would each demand their own
            // matching log entry (there is only one), so match name and the "n=3" param fragment together.
            LogAssert.Expect(LogType.Log, new Regex("event 'e'.*n=3"));

            new ConsoleAnalyticsBackend().LogEvent(
                new AnalyticsEvent("e", new[] { AnalyticsParam.Long("n", 3) }));
        }

        // 3
        [Test]
        public void Flush_WritesToConsole()
        {
            LogAssert.Expect(LogType.Log, new Regex("flush"));

            new ConsoleAnalyticsBackend().Flush();
        }
    }
}
