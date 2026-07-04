using System;
using System.Threading;
using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// Marshals gateway callbacks to the Unity main thread. Captured at gateway init
    /// (which the service calls from the main thread). No-op when already on main.
    /// </summary>
    internal sealed class MainThreadDispatcher
    {
        private SynchronizationContext _mainContext;
        private int _mainThreadId;

        public void CaptureMainThread()
        {
            _mainContext = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public void Post(Action action)
        {
            if (action == null) return;

            if (_mainContext == null || Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                Run(action);
                return;
            }

            _mainContext.Post(_ => Run(action), null);
        }

        private static void Run(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
