using System.Collections.Generic;
using System.Threading.Tasks;

namespace TK.RemoteConfig
{
    /// <summary>
    /// Remote-config backend seam. The package ships no backend (a Firebase adapter is a Sample);
    /// tests inject a fake. Implementations serve last-activated values; a missing key returns false
    /// from TryGet* so the service falls back to the parameter default.
    /// </summary>
    public interface IRemoteConfigBackend
    {
        /// <summary>True once the backend core is initialized enough to answer TryGet* safely.</summary>
        bool IsReady { get; }

        /// <summary>Register defaults and initialize the backend core. Completes when values are safe to read.</summary>
        Task InitializeAsync(IReadOnlyDictionary<string, object> defaults);

        /// <summary>Fetch + activate remote values. Returns true if new values were activated.</summary>
        Task<bool> FetchAndActivateAsync();

        bool TryGetLong(string key, out long value);
        bool TryGetDouble(string key, out double value);
        bool TryGetBool(string key, out bool value);
        bool TryGetString(string key, out string value);
    }
}
