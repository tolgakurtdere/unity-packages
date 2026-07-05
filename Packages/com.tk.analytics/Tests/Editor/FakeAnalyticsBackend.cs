using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.Analytics;

namespace TK.Analytics.Tests
{
    /// <summary>Recording backend for deterministic EditMode tests.</summary>
    public sealed class FakeAnalyticsBackend : IAnalyticsBackend
    {
        public string Name { get; }
        public FakeAnalyticsBackend(string name = "fake") => Name = name;

        // Knobs
        public bool ThrowOnInit;
        public bool ThrowOnLogEvent;

        // Recorded
        public int InitializeCalls;
        public int FlushCalls;
        public readonly List<AnalyticsEvent> Events = new();
        public readonly List<AnalyticsPurchase> Purchases = new();
        public readonly List<AnalyticsAdRevenue> AdRevenues = new();
        public readonly Dictionary<string, string> UserProperties = new();
        public string UserId;
        /// <summary>Ordered trace of every op, for order-sensitive assertions.</summary>
        public readonly List<string> Trace = new();

        public Task InitializeAsync()
        {
            InitializeCalls++;
            if (ThrowOnInit) throw new InvalidOperationException("fake: init threw");
            return Task.CompletedTask;
        }

        public void LogEvent(AnalyticsEvent evt)
        {
            if (ThrowOnLogEvent) throw new InvalidOperationException("fake: logevent threw");
            Events.Add(evt);
            Trace.Add($"event:{evt.Name}");
        }

        public void LogPurchase(AnalyticsPurchase purchase)
        {
            Purchases.Add(purchase);
            Trace.Add($"purchase:{purchase.ProductId}");
        }

        public void LogAdRevenue(AnalyticsAdRevenue adRevenue)
        {
            AdRevenues.Add(adRevenue);
            Trace.Add($"adrevenue:{adRevenue.AdUnitId}");
        }

        public void SetUserProperty(string key, string value)
        {
            UserProperties[key] = value;
            Trace.Add($"userprop:{key}={value}");
        }

        public void SetUserId(string userId)
        {
            UserId = userId;
            Trace.Add($"userid:{userId}");
        }

        public void Flush()
        {
            FlushCalls++;
            Trace.Add("flush");
        }
    }
}
