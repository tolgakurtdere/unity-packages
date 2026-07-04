using System;

namespace TK.Ads
{
    /// <summary>Exponential backoff for ad load retries: min(2^attempt, 64) seconds. One instance per format.</summary>
    public sealed class LoadRetryPolicy
    {
        public const float MaxDelaySeconds = 64f;

        private int _failedAttempts;

        public int FailedAttempts => _failedAttempts;

        /// <summary>Register a failure; returns the delay to wait before the next load.</summary>
        public float OnFailed()
        {
            _failedAttempts++;
            var delay = Math.Pow(2, _failedAttempts);
            return (float)Math.Min(delay, MaxDelaySeconds);
        }

        public void OnSucceeded() => _failedAttempts = 0;
    }
}
