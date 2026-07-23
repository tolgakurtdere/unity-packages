namespace TK.Core.App
{
    /// <summary>What startup does with <c>Debug.unityLogger</c>.</summary>
    public enum LogPolicy
    {
        /// <summary>Leave logging alone.</summary>
        LeaveDefault,

        /// <summary>
        /// Silence logs in release player builds only — the Editor and development builds keep theirs.
        /// This is the distinction the old <c>AppBootstrapper.disableLogsInReleaseBuilds</c> field never
        /// made: it keyed on "not the Editor", so it silenced test builds too and hid failures there.
        /// </summary>
        DisableInReleaseBuilds,

        /// <summary>Silence logs in every player build, development ones included.</summary>
        DisableInAllPlayerBuilds
    }
}
