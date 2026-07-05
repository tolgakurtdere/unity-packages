using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Analytics
{
    /// <summary>
    /// Built-in, dependency-free backend that writes every call to the Unity console. Useful in-editor
    /// and for QA; add it alongside real backends or use it alone. Requires no SDK.
    /// </summary>
    public sealed class ConsoleAnalyticsBackend : IAnalyticsBackend
    {
        public string Name => "Console";

        public Task InitializeAsync() => Task.CompletedTask;

        public void LogEvent(AnalyticsEvent evt)
        {
            var sb = new StringBuilder();
            sb.Append("[Analytics] event '").Append(evt.Name).Append('\'');
            if (evt.Parameters.Count > 0)
            {
                sb.Append(" {");
                for (var i = 0; i < evt.Parameters.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(evt.Parameters[i]);
                }
                sb.Append('}');
            }
            Debug.Log(sb.ToString());
        }

        public void LogPurchase(AnalyticsPurchase p) =>
            Debug.Log($"[Analytics] purchase '{p.ProductId}' {p.Price} {p.Currency} (tx {p.TransactionId}, qty {p.Quantity}, restore {p.IsRestore})");

        public void LogAdRevenue(AnalyticsAdRevenue a) =>
            Debug.Log($"[Analytics] adRevenue {a.Format} {a.AdNetwork} {a.AdUnitId} {a.Revenue} {a.Currency} placement={a.Placement}");

        public void SetUserProperty(string key, string value) =>
            Debug.Log($"[Analytics] userProperty {key}={value}");

        public void SetUserId(string userId) =>
            Debug.Log($"[Analytics] userId={userId}");

        public void Flush() => Debug.Log("[Analytics] flush");
    }
}
