namespace TK.Core.App
{
    /// <summary>What startup does with <c>Debug.unityLogger</c>.</summary>
    public enum LogPolicy
    {
        /// <summary>Leave logging alone.</summary>
        LeaveDefault,

        /// <summary>
        /// Silence logs in release player builds only — the Editor and test builds keep theirs. A test
        /// build is a Development Build, or any build compiled with the <c>TK_TEST_BUILD</c> scripting
        /// define for pipelines that mark test builds their own way. This is the distinction the old
        /// <c>AppBootstrapper.disableLogsInReleaseBuilds</c> field never made: it keyed on "not the
        /// Editor", so it silenced test builds too and hid failures exactly where you look for them.
        /// </summary>
        DisableInReleaseBuilds,

        /// <summary>Silence logs in every player build, development ones included.</summary>
        DisableInAllPlayerBuilds
    }
}
