namespace TK.Ads
{
    /// <summary>Outcome of a rewarded ad request.</summary>
    public enum RewardedResult
    {
        /// <summary>The ads service was not initialized when the request was made.</summary>
        NotInitialized = 0,
        /// <summary>No rewarded ad was loaded and ready to show.</summary>
        NotReady,
        /// <summary>The user watched the ad and earned the reward.</summary>
        Rewarded,
        /// <summary>The ad was closed before the user earned the reward.</summary>
        Cancelled,
        /// <summary>The ad failed to display after being requested.</summary>
        FailedToShow
    }
}
