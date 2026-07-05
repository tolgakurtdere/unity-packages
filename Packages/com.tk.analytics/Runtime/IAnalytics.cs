using System.Threading.Tasks;

namespace TK.Analytics
{
    /// <summary>
    /// Analytics façade. Main-thread-affine (background callers marshal first). Log calls are gated by
    /// enabled/consent/started state and buffered (in order, with parameters preserved) until dispatch
    /// is allowed. Lifecycle/config (StartAsync, SetConsent, IsEnabled, Flush) is driven by the instance;
    /// the static Analytics façade exposes only the log verbs.
    /// </summary>
    public interface IAnalytics
    {
        /// <summary>Runtime kill-switch. False = inert: new ops dropped, flush paused (buffer kept).</summary>
        bool IsEnabled { get; set; }

        void LogEvent(string name);
        void LogEvent(string name, params AnalyticsParam[] parameters);
        void LogPurchase(AnalyticsPurchase purchase);
        void LogAdRevenue(AnalyticsAdRevenue adRevenue);
        void SetUserProperty(string key, string value);
        void SetUserId(string userId);

        /// <summary>true → flush the buffer and allow dispatch; false → clear the buffer and block.</summary>
        void SetConsent(bool granted);

        /// <summary>Ask each backend to flush its own native buffer (no-op if dispatch not yet allowed).</summary>
        void Flush();

        /// <summary>Initialize all backends, mark started, then flush if the gate is open. Single-flight.</summary>
        Task StartAsync();
    }
}
