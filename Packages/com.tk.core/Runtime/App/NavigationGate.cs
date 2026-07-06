using System;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// A drop-on-reentry navigation lock: RunAsync executes one operation at a time and silently
    /// DROPS calls made while one is running (spam-safe) — bind button interactability to
    /// CanNavigate for feedback. Exceptions are logged, never thrown, and always release the gate.
    /// AppRootBase uses one internally (RunTransitionAsync); construct your own for
    /// composition-first setups that skip the base classes entirely.
    /// </summary>
    public class NavigationGate
    {
        /// <summary>True while an operation is running.</summary>
        public bool IsTransitioning { get; private set; }

        public bool CanNavigate => !IsTransitioning;

        /// <summary>
        /// Runs an operation under the lock. Re-entrant calls are DROPPED. The operation body only
        /// evaluates once the lock is held.
        /// </summary>
        public async Awaitable RunAsync(Func<Awaitable> operation)
        {
            if (IsTransitioning) return;

            IsTransitioning = true;
            try
            {
                await operation();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                IsTransitioning = false;
            }
        }
    }
}
