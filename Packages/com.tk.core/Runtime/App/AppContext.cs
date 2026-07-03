using System;
using System.Collections.Generic;
using TK.Core.Save;

namespace TK.Core.App
{
    /// <summary>
    /// App-level service container created at the composition root and passed down explicitly.
    /// Package-owned services are typed properties; everything else goes through the generic
    /// registry (Register/Get/TryGet). Future packages add typed sugar via extension methods,
    /// so this class never needs to change.
    /// Discipline: resolve services at the composition root and constructor-inject downward —
    /// don't pass the context deep into gameplay code as a service locator.
    /// </summary>
    public class AppContext
    {
        public ISaveSystem SaveSystem { get; }

        public event Action<GameEndResult> OnGameEnded;

        // App lifecycle signals, relayed from AppLifecycleRelay. Ads/IAP/analytics hook these.
        public event Action<bool> OnAppPause;
        public event Action<bool> OnAppFocus;
        public event Action OnAppQuit;

        private readonly Dictionary<Type, object> _services = new();

        public AppContext(ISaveSystem saveSystem)
        {
            SaveSystem = saveSystem ?? throw new ArgumentNullException(nameof(saveSystem));
        }

        /// <summary>Broadcasts a game-end event (the flow layer listens to advance/show results).</summary>
        public void RaiseGameEnded(GameEndResult result) => OnGameEnded?.Invoke(result);

        /// <summary>Registers a service instance. One instance per type; duplicates throw.</summary>
        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (!_services.TryAdd(typeof(T), service))
                throw new InvalidOperationException($"[AppContext] {typeof(T).Name} is already registered.");
        }

        /// <summary>Resolves a registered service; throws if missing (register at the composition root).</summary>
        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var service)) return service;
            throw new InvalidOperationException($"[AppContext] {typeof(T).Name} is not registered.");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var value))
            {
                service = (T)value;
                return true;
            }

            service = null;
            return false;
        }

        internal void RaiseAppPause(bool isPaused) => OnAppPause?.Invoke(isPaused);
        internal void RaiseAppFocus(bool hasFocus) => OnAppFocus?.Invoke(hasFocus);
        internal void RaiseAppQuit() => OnAppQuit?.Invoke();
    }
}
