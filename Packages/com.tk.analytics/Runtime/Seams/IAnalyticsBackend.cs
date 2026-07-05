using System.Threading.Tasks;

namespace TK.Analytics
{
    /// <summary>
    /// Analytics backend adapter seam. The package ships no vendor backend (Firebase/Adjust are Samples)
    /// beyond the built-in ConsoleAnalyticsBackend. Implementations MUST NOT throw — the service wraps
    /// every call in try/catch, logs, and continues, so one bad backend never blocks the others.
    /// A backend with no native revenue API maps LogPurchase/LogAdRevenue onto a LogEvent internally;
    /// a backend that does not care about a call simply no-ops.
    /// </summary>
    public interface IAnalyticsBackend
    {
        string Name { get; }
        Task InitializeAsync();
        void LogEvent(AnalyticsEvent evt);
        void LogPurchase(AnalyticsPurchase purchase);
        void LogAdRevenue(AnalyticsAdRevenue adRevenue);
        void SetUserProperty(string key, string value);
        void SetUserId(string userId);
        void Flush();
    }
}
