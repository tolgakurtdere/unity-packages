using TK.Ads;
using UnityEngine;

namespace TK.Ads.Samples.IntegrationExamples
{
    /// <summary>
    /// Reference <see cref="IAdRevenueReporter"/> implementation that reports paid ad impressions to
    /// Firebase Analytics as an <c>ad_impression</c> event, mirroring the parameter set commonly used
    /// for AppLovin MAX ILRD (impression-level revenue data) reporting: <c>ad_platform</c> is always
    /// "AppLovin" (MAX is the sole mediation source this package integrates), <c>ad_source</c> is the
    /// winning mediated network's name, <c>ad_unit_name</c>/<c>ad_format</c>/<c>value</c>/<c>currency</c>
    /// map directly from <see cref="AdRevenueInfo"/>. Compiles WITHOUT the Firebase SDK present — the
    /// actual <c>FirebaseAnalytics.LogEvent</c> call is commented out below; uncomment it (and add the
    /// Firebase Analytics package) to wire it up for real.
    /// </summary>
    public sealed class FirebaseAdRevenueReporterExample : IAdRevenueReporter
    {
        // Event/parameter names follow Firebase's own recommended ad_impression convention.
        private const string AdImpressionEvent = "ad_impression";
        private const string ParamAdPlatform = "ad_platform";
        private const string ParamAdSource = "ad_source";
        private const string ParamAdUnitName = "ad_unit_name";
        private const string ParamAdFormat = "ad_format";
        private const string ParamValue = "value";
        private const string ParamCurrency = "currency";

        public void OnAdRevenue(AdRevenueInfo info)
        {
            Debug.Log($"[FirebaseAdRevenueReporterExample] Reporting '{AdImpressionEvent}' — " +
                      $"format={info.Format} network={info.NetworkName} adUnit={info.AdUnitId} " +
                      $"revenue={info.Revenue} {info.Currency} placement={info.Placement}.");

            // FirebaseAnalytics.LogEvent(AdImpressionEvent,
            //     new Parameter(ParamAdPlatform, "AppLovin"),
            //     new Parameter(ParamAdSource, info.NetworkName),
            //     new Parameter(ParamAdUnitName, info.AdUnitId),
            //     new Parameter(ParamAdFormat, info.Format),
            //     new Parameter(ParamValue, info.Revenue),
            //     new Parameter(ParamCurrency, info.Currency));

            // Optional: also log info.Placement as a custom parameter if you want per-placement
            // revenue breakdowns — it's omitted above because ad_impression's standard schema
            // doesn't define a placement field, but nothing stops you from adding one.
        }
    }
}
