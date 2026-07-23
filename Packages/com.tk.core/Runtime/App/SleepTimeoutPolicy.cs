namespace TK.Core.App
{
    /// <summary>What startup does with <c>Screen.sleepTimeout</c>.</summary>
    public enum SleepTimeoutPolicy
    {
        /// <summary>Leave it alone.</summary>
        LeaveDefault,

        /// <summary>
        /// Keep the screen awake. For games the player reads without touching — a puzzle board studied
        /// for a minute otherwise dims mid-level on the OS idle timer.
        /// </summary>
        NeverSleep,

        /// <summary>Follow the OS idle timer.</summary>
        SystemSetting
    }
}
