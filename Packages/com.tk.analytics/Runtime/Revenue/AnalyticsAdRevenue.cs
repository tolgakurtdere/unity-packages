namespace TK.Analytics
{
    /// <summary>Neutral, package-owned ad-revenue record. Bridges map com.tk.ads's AdRevenueInfo into this.</summary>
    public readonly struct AnalyticsAdRevenue
    {
        public string Format { get; }         // "banner"/"interstitial"/"rewarded"
        public string AdNetwork { get; }      // winning mediated network
        public string AdUnitId { get; }
        public double Revenue { get; }
        public string Currency { get; }       // ISO 4217 (usually "USD")
        public string Placement { get; }      // may be null

        public AnalyticsAdRevenue(string format, string adNetwork, string adUnitId,
            double revenue, string currency, string placement)
        {
            Format = format;
            AdNetwork = adNetwork;
            AdUnitId = adUnitId;
            Revenue = revenue;
            Currency = currency;
            Placement = placement;
        }
    }
}
