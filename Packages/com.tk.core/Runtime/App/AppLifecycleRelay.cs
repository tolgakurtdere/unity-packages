using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Forwards Unity application lifecycle callbacks to AppContext events.
    /// AppFlowBase adds one automatically; add manually if you don't use AppFlowBase.
    /// </summary>
    public sealed class AppLifecycleRelay : MonoBehaviour
    {
        private AppContext _context;

        public void Initialize(AppContext context) => _context = context;

        private void OnApplicationPause(bool pauseStatus) => _context?.RaiseAppPause(pauseStatus);
        private void OnApplicationFocus(bool hasFocus) => _context?.RaiseAppFocus(hasFocus);
        private void OnApplicationQuit() => _context?.RaiseAppQuit();
    }
}
