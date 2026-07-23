using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Orchestrates a transition curtain: ref-counted Show/Hide over a single, lazily resolved
    /// view, with back-input suppression callbacks around the covered period. Plain class —
    /// UIManager wires one for its curtain API; construct your own only for setups that bypass
    /// UIManager entirely.
    /// </summary>
    public sealed class TransitionCurtainController
    {
        private readonly Func<Awaitable<ITransitionCurtainView>> _resolveView;
        private readonly Action _onCoverBegin;
        private readonly Action _onOpenEnd;
        private readonly Func<float, Awaitable> _waitSeconds;

        private ITransitionCurtainView _view;
        private int _holders;
        private bool _viewCovered;
        private bool _settling;
        // One-shot: the next cover pass snaps via ShowInstantly instead of animating.
        // Armed by CoverInstantlyAsync; consumed/cleared at four points (cover entry,
        // cover success end, cover failure, loop-top covered-flush) so it can never leak into a later cover.
        private bool _coverInstantly;
        private readonly List<AwaitableCompletionSource> _coveredWaiters = new();
        private readonly List<AwaitableCompletionSource> _openWaiters = new();

        /// <summary>True while the curtain is fully covering the screen.</summary>
        public bool IsCovered => _viewCovered;

        // The seam is an interface, but shipped views are MonoBehaviours — a destroyed view
        // (scene reload) must read as "no view", not as a live reference to call into.
        private bool ViewIsGone() => _view is UnityEngine.Object unityView && !unityView;

        public TransitionCurtainController(
            Func<Awaitable<ITransitionCurtainView>> resolveView,
            Action onCoverBegin = null,
            Action onOpenEnd = null,
            Func<float, Awaitable> waitSeconds = null)
        {
            _resolveView = resolveView ?? throw new ArgumentNullException(nameof(resolveView));
            _onCoverBegin = onCoverBegin ?? (() => { });
            _onOpenEnd = onOpenEnd ?? (() => { });
            _waitSeconds = waitSeconds ?? WaitUnscaledAsync;
        }

        /// <summary>
        /// Covers the screen, runs the work, reopens — reopening is guaranteed even when the work
        /// throws (the exception still propagates). minCoverSeconds keeps the curtain closed at
        /// least that long (unscaled) after cover; an exception skips the remaining hold.
        /// If covering itself fails, the work never runs and that exception propagates.
        /// </summary>
        public async Awaitable RunAsync(Func<Awaitable> work, float minCoverSeconds = 0f)
        {
            await ShowAsync();
            try
            {
                var minHold = minCoverSeconds > 0f ? _waitSeconds(minCoverSeconds) : null;
                await work();
                if (minHold != null) await minHold;
            }
            finally
            {
                await HideAsync();
            }
        }

        /// <summary>
        /// Takes one hold and returns once the screen is fully covered. Every successful Show must
        /// pair with one <see cref="HideAsync"/>. If this throws, the hold was NOT taken — do not
        /// call Hide for it.
        /// </summary>
        public Awaitable ShowAsync() => ShowCoreAsync(instant: false);

        /// <summary>
        /// Takes one hold and covers WITHOUT animation (the view's ShowInstantly). With a
        /// synchronously-resolving view (the no-catalog fallback path) this completes in the
        /// same frame — the boot guarantee. The open side is always animated; pair with
        /// <see cref="HideAsync"/> exactly like a normal Show. If an animated cover is already
        /// in flight, this call joins it (no mid-animation fast-forward).
        /// </summary>
        public Awaitable CoverInstantlyAsync() => ShowCoreAsync(instant: true);

        private Awaitable ShowCoreAsync(bool instant)
        {
            _holders++;
            if (_viewCovered && !_settling) return CompletedAwaitable();
            if (instant) _coverInstantly = true;
            var waiter = AddWaiter(_coveredWaiters);
            _ = SettleAsync();
            return waiter;
        }

        /// <summary>
        /// Releases one hold; the curtain reopens (and this returns) once the LAST holder
        /// releases and the open animation finishes. Unbalanced calls warn and do nothing.
        /// </summary>
        public Awaitable HideAsync()
        {
            if (_holders == 0)
            {
                Debug.LogWarning("[TransitionCurtain] Hide without a matching Show — ignored.");
                return CompletedAwaitable();
            }

            _holders--;
            if (_holders > 0 || !_viewCovered && !_settling) return CompletedAwaitable();
            var waiter = AddWaiter(_openWaiters);
            _ = SettleAsync();
            return waiter;
        }

        // Single worker that moves the view toward the desired state (covered iff holders > 0),
        // looping until stable — a Show landing mid-open (or vice versa) is settled in the next
        // pass instead of interleaving animations.
        private async Awaitable SettleAsync()
        {
            if (_settling) return;
            _settling = true;
            try
            {
                while (true)
                {
                    var wantCovered = _holders > 0;
                    if (wantCovered == _viewCovered)
                    {
                        // Waiters queued during a flush cascade (a reentrant Show/Hide that arrived while
                        // _settling was still true) match the state we just settled into — complete them,
                        // then re-evaluate: their continuations may have changed the demand again.
                        // An instant wish among them is moot when already covered — clear it.
                        if (_viewCovered) _coverInstantly = false;
                        var pending = _viewCovered ? _coveredWaiters : _openWaiters;
                        if (pending.Count == 0) break;
                        FlushWaiters(pending);
                        continue;
                    }

                    if (wantCovered)
                    {
                        var instant = _coverInstantly;
                        _coverInstantly = false;
                        InvokeGuarded(_onCoverBegin);
                        try
                        {
                            if (ViewIsGone()) _view = null;
                            _view ??= await _resolveView();
                            if (instant) _view.ShowInstantly();
                            else await _view.ShowAsync();
                        }
                        catch (Exception exception)
                        {
                            // Failed to cover: force-open, fail every waiting Show, drop all
                            // holds — a throwing ShowAsync must never leave the screen black.
                            TryForceOpen();
                            InvokeGuarded(_onOpenEnd);
                            _holders = 0;
                            // A failed cover drops ALL the demand it carried — including a joined instant wish.
                            _coverInstantly = false;
                            FailWaiters(_coveredWaiters, exception);
                            FlushWaiters(_openWaiters);
                            // A waiter continuation may have re-entered with a new Show/Hide
                            // (_holders was just reset to 0) — re-evaluate instead of exiting,
                            // or that new demand would be stranded.
                            continue;
                        }
                        _viewCovered = true;
                        // Instant requests that arrived DURING this (possibly animated) cover joined
                        // it — their wish must not re-arm a future cover.
                        _coverInstantly = false;
                        FlushWaiters(_coveredWaiters);
                    }
                    else
                    {
                        // A destroyed view has no visual left to animate — treat it as already
                        // open rather than calling into the dead MonoBehaviour.
                        if (!ViewIsGone())
                        {
                            try
                            {
                                await _view.HideAsync();
                            }
                            catch (Exception exception)
                            {
                                // Never propagate hide failures — in RunAsync's finally they would
                                // mask the work's own exception. Log, force-open, carry on.
                                Debug.LogException(exception);
                                TryForceOpen();
                            }
                        }
                        _viewCovered = false;
                        InvokeGuarded(_onOpenEnd);
                        FlushWaiters(_openWaiters);
                    }
                }
            }
            finally
            {
                _settling = false;
            }
        }

        // Callback failures must never break curtain settling — waiters would otherwise hang
        // (never flushed) or a caught exception downstream would mask the original.
        private static void InvokeGuarded(Action callback)
        {
            try { callback(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void TryForceOpen()
        {
            _viewCovered = false;
            if (_view == null || ViewIsGone()) return;
            try { _view.HideInstantly(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static Awaitable AddWaiter(List<AwaitableCompletionSource> waiters)
        {
            var source = new AwaitableCompletionSource();
            waiters.Add(source);
            return source.Awaitable;
        }

        // Copy-then-clear before completing: continuations run synchronously and may re-enter
        // (a completed waiter immediately calling Show/Hide again would mutate the list).
        private static void FlushWaiters(List<AwaitableCompletionSource> waiters)
        {
            if (waiters.Count == 0) return;
            var flushed = waiters.ToArray();
            waiters.Clear();
            foreach (var waiter in flushed) waiter.SetResult();
        }

        private static void FailWaiters(List<AwaitableCompletionSource> waiters, Exception exception)
        {
            if (waiters.Count == 0) return;
            var failed = waiters.ToArray();
            waiters.Clear();
            foreach (var waiter in failed) waiter.SetException(exception);
        }

        private static Awaitable CompletedAwaitable()
        {
            var source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }

        private static async Awaitable WaitUnscaledAsync(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                await Awaitable.NextFrameAsync();
                elapsed += Time.unscaledDeltaTime;
            }
        }
    }
}
