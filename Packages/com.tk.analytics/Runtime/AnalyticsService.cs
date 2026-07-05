using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Analytics
{
    /// <summary>
    /// Multi-backend analytics service. Fans every operation out to all backends, gates dispatch on
    /// enabled/consent/started, and buffers (in order, params preserved) whatever cannot dispatch yet.
    /// Each backend call is isolated in try/catch so one throwing backend never blocks the others.
    /// Main-thread-affine: no locking; background-thread callers marshal to the main thread first.
    /// </summary>
    public sealed class AnalyticsService : IAnalytics
    {
        private readonly List<IAnalyticsBackend> _backends;
        private readonly Queue<Action<IAnalyticsBackend>> _queue = new();
        private Task _startTask;
        private bool _enabled = true;

        public AnalyticsService(IEnumerable<IAnalyticsBackend> backends)
        {
            if (backends == null) throw new ArgumentNullException(nameof(backends));
            _backends = new List<IAnalyticsBackend>(backends);
        }

        public AnalyticsConsent Consent { get; private set; } = AnalyticsConsent.Unknown;
        public bool IsStarted { get; private set; }

        public bool IsEnabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value) TryFlushQueue();   // re-enabling resumes a paused flush
            }
        }

        private bool IsDispatchAllowed => IsStarted && _enabled && Consent == AnalyticsConsent.Granted;

        // ── Ingestion ──

        public void LogEvent(string name) => Ingest(b => b.LogEvent(new AnalyticsEvent(name)));

        public void LogEvent(string name, params AnalyticsParam[] parameters)
            => Ingest(b => b.LogEvent(new AnalyticsEvent(name, parameters)));

        public void LogPurchase(AnalyticsPurchase purchase) => Ingest(b => b.LogPurchase(purchase));

        public void LogAdRevenue(AnalyticsAdRevenue adRevenue) => Ingest(b => b.LogAdRevenue(adRevenue));

        public void SetUserProperty(string key, string value) => Ingest(b => b.SetUserProperty(key, value));

        public void SetUserId(string userId) => Ingest(b => b.SetUserId(userId));

        private void Ingest(Action<IAnalyticsBackend> op)
        {
            if (!_enabled) return;                            // kill-switch: drop
            if (Consent == AnalyticsConsent.Denied) return;   // GDPR: never send
            if (IsDispatchAllowed) Dispatch(op);              // live
            else _queue.Enqueue(op);                          // buffer (consent Unknown or not started)
        }

        private void Dispatch(Action<IAnalyticsBackend> op)
        {
            for (var i = 0; i < _backends.Count; i++)
            {
                try { op(_backends[i]); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void TryFlushQueue()
        {
            if (!IsDispatchAllowed) return;
            while (_queue.Count > 0)
                Dispatch(_queue.Dequeue());
        }

        // ── Consent ──

        public void SetConsent(bool granted)
        {
            Consent = granted ? AnalyticsConsent.Granted : AnalyticsConsent.Denied;
            if (granted) TryFlushQueue();
            else _queue.Clear();   // denied: discard buffered ops, never send
        }

        // ── Lifecycle ──

        public Task StartAsync()
        {
            if (_startTask != null) return _startTask;   // single-flight
            _startTask = StartInternalAsync();
            return _startTask;
        }

        private async Task StartInternalAsync()
        {
            for (var i = 0; i < _backends.Count; i++)
            {
                try { await _backends[i].InitializeAsync(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }

            IsStarted = true;
            TryFlushQueue();
        }

        public void Flush()
        {
            if (!IsDispatchAllowed) return;
            Dispatch(b => b.Flush());
        }
    }
}
