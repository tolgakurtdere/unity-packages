namespace TK.Ads
{
    /// <summary>Everything analytics/backend needs about a single paid ad impression.</summary>
    public readonly struct AdRevenueInfo
    {
        /// <summary>"banner", "interstitial" or "rewarded".</summary>
        public string Format { get; }
        public string NetworkName { get; }
        public string AdUnitId { get; }
        public double Revenue { get; }
        /// <summary>ISO currency code. Always "USD" — the only currency MAX reports revenue in.</summary>
        public string Currency { get; }
        /// <summary>Caller-supplied placement tag for the impression that earned revenue. May be null.</summary>
        public string Placement { get; }

        public AdRevenueInfo(string format, string networkName, string adUnitId, double revenue, string currency, string placement)
        {
            Format = format;
            NetworkName = networkName;
            AdUnitId = adUnitId;
            Revenue = revenue;
            Currency = currency;
            Placement = placement;
        }
    }
}
