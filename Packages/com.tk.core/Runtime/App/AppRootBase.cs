using System;
using TK.Core.Save;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Level-free composition root: owns AppContext (with the save system), the AppLifecycleRelay,
    /// the RegisterServices hook, and the navigation/transition lock — and runs the boot policy
    /// (OnBootAsync) on Start. No level concepts: subclass this directly for endless / one-run
    /// games and define your own verbs (e.g. StartNewRunAsync) over RunTransitionAsync;
    /// level-based games use AppFlowBase, which derives from this and adds level progression.
    /// The App module's three adoption tiers: AppContext only → AppRootBase → AppFlowBase —
    /// each opt-in, none forcing the next.
    /// </summary>
    public abstract class AppRootBase : MonoBehaviour
    {
        private readonly NavigationGate _gate = new();

        protected AppContext Context { get; private set; }

        /// <summary>True while a navigation transition is running. UI can bind buttons to CanNavigate.</summary>
        public bool IsTransitioning => _gate.IsTransitioning;

        public bool CanNavigate => _gate.CanNavigate;

        /// <summary>Swap the save backend if needed (cloud, file, ...).</summary>
        protected virtual ISaveSystem CreateSaveSystem() => new PlayerPrefsJsonSaveSystem();

        /// <summary>Register game services into the context (called once, end of Awake).</summary>
        protected virtual void RegisterServices(AppContext context) { }

        /// <summary>
        /// Runs right after Context exists and before RegisterServices — derived presets create
        /// their own services here (AppFlowBase constructs LevelProgress). Call base when
        /// overriding.
        /// </summary>
        protected virtual void OnContextCreated() { }

        /// <summary>
        /// Boot policy: what the app enters on Start. AppFlowBase overrides this with
        /// resume-or-menu; a root-only game decides for itself. Verbs a subclass defines over
        /// RunTransitionAsync are safe to call from here — they take the lock themselves.
        /// </summary>
        protected abstract Awaitable OnBootAsync();

        /// <summary>Game-end hook (show result popup, advance flow, ...). Fired via AppContext.RaiseGameEnded.</summary>
        protected virtual void OnGameEnded(GameEndResult result) { }

        protected virtual void Awake()
        {
            Context = new AppContext(CreateSaveSystem());
            OnContextCreated();
            Context.OnGameEnded += HandleGameEnded;
            gameObject.AddComponent<AppLifecycleRelay>().Initialize(Context);
            RegisterServices(Context);
        }

        protected virtual async void Start()
        {
            try
            {
                await OnBootAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        protected virtual void OnDestroy()
        {
            if (Context != null) Context.OnGameEnded -= HandleGameEnded;
        }

        /// <summary>
        /// Runs a navigation operation under the transition lock. Re-entrant calls while a
        /// transition is running are DROPPED (spam-safe) — bind button interactability to
        /// CanNavigate for feedback. The operation body only evaluates once the lock is held.
        /// </summary>
        protected Awaitable RunTransitionAsync(Func<Awaitable> operation) => _gate.RunAsync(operation);

        // Internal dispatch seam: AppFlowBase appends its async after-end hook here while keeping
        // the sync OnGameEnded order unchanged. Not visible outside this assembly.
        private protected virtual void HandleGameEnded(GameEndResult result) => OnGameEnded(result);
    }
}
