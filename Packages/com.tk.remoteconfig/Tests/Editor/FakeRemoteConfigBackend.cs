using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.RemoteConfig;

namespace TK.RemoteConfig.Tests
{
    /// <summary>In-memory backend for deterministic EditMode tests.</summary>
    public sealed class FakeRemoteConfigBackend : IRemoteConfigBackend
    {
        public bool IsReady { get; private set; }

        // Knobs
        public bool ThrowOnInit;
        public bool FailFetch;
        public bool NextFetchActivates = true;

        // Recorded
        public int InitializeCalls;
        public int FetchCalls;
        public IReadOnlyDictionary<string, object> ReceivedDefaults;

        // Values the backend serves (set by tests to simulate activated remote values)
        public readonly Dictionary<string, object> Values = new();

        public void SetLong(string key, long v) => Values[key] = v;
        public void SetDouble(string key, double v) => Values[key] = v;
        public void SetBool(string key, bool v) => Values[key] = v;
        public void SetString(string key, string v) => Values[key] = v;

        public Task InitializeAsync(IReadOnlyDictionary<string, object> defaults)
        {
            InitializeCalls++;
            ReceivedDefaults = defaults;
            if (ThrowOnInit) throw new InvalidOperationException("fake: init threw");
            IsReady = true;
            return Task.CompletedTask;
        }

        public Task<bool> FetchAndActivateAsync()
        {
            FetchCalls++;
            if (FailFetch) return Task.FromException<bool>(new Exception("fake: fetch failed"));
            return Task.FromResult(NextFetchActivates);
        }

        public bool TryGetLong(string key, out long value)
        {
            if (Values.TryGetValue(key, out var o) && o is long l) { value = l; return true; }
            value = 0; return false;
        }

        public bool TryGetDouble(string key, out double value)
        {
            if (Values.TryGetValue(key, out var o) && o is double d) { value = d; return true; }
            value = 0; return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (Values.TryGetValue(key, out var o) && o is bool b) { value = b; return true; }
            value = false; return false;
        }

        public bool TryGetString(string key, out string value)
        {
            if (Values.TryGetValue(key, out var o) && o is string s) { value = s; return true; }
            value = null; return false;
        }
    }
}
