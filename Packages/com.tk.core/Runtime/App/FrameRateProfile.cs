using System;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>How a platform picks its <c>Application.targetFrameRate</c> at startup.</summary>
    public enum FrameRateMode
    {
        /// <summary>Write nothing — whatever the platform defaults to stands (30 on mobile).</summary>
        PlatformDefault,

        /// <summary>Use the profile's explicit value.</summary>
        Fixed,

        /// <summary>
        /// Match the display's refresh rate. The way out on panels a fixed rate does not divide into:
        /// 60 lands on a whole refresh at 120 Hz and 60 Hz, but beats against a 90 Hz panel.
        /// </summary>
        MatchRefreshRate,

        /// <summary>Half the refresh rate — a battery cadence that still lands on whole refreshes.</summary>
        HalfRefreshRate
    }

    /// <summary>
    /// One platform's frame-rate choice. <see cref="fixedFps"/> is read only when <see cref="mode"/>
    /// is <see cref="FrameRateMode.Fixed"/>.
    /// </summary>
    [Serializable]
    public struct FrameRateProfile
    {
        public FrameRateMode mode;

        [Min(1)] public int fixedFps;
    }
}
