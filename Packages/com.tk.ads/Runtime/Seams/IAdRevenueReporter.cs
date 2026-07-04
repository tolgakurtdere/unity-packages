namespace TK.Ads
{
    /// <summary>
    /// Seam for analytics/backend revenue reporting (e.g. Firebase ad_impression). Called on the
    /// main thread AFTER each paid impression. Implementations must not throw — the service logs
    /// and continues if they do.
    /// </summary>
    public interface IAdRevenueReporter
    {
        void OnAdRevenue(AdRevenueInfo info);
    }
}
