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
        private readonly List<AwaitableCompletionSource> _coveredWaiters = new();
        private readonly List<AwaitableCompletionSource> _openWaiters = new();

        /// <summary>True while the curtain is fully covering the screen.</summary>
        public bool IsCovered => _viewCovered;

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
        public Awaitable ShowAsync()
        {
            _holders++;
            if (_viewCovered && !_settling) return CompletedAwaitable();
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
            if (_holders > 0) return CompletedAwaitable();
            if (!_viewCovered && !_settling) return CompletedAwaitable();
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
                    if (wantCovered == _viewCovered) break;

                    if (wantCovered)
                    {
                        _onCoverBegin();
                        try
                        {
                            _view ??= await _resolveView();
                            await _view.ShowAsync();
                        }
                        catch (Exception exception)
                        {
                            // Failed to cover: force-open, fail every waiting Show, drop all
                            // holds — a throwing ShowAsync must never leave the screen black.
                            TryForceOpen();
                            _onOpenEnd();
                            _holders = 0;
                            FailWaiters(_coveredWaiters, exception);
                            FlushWaiters(_openWaiters);
                            break;
                        }
                        _viewCovered = true;
                        FlushWaiters(_coveredWaiters);
                    }
                    else
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
                        _viewCovered = false;
                        _onOpenEnd();
                        FlushWaiters(_openWaiters);
                    }
                }
            }
            finally
            {
                _settling = false;
            }
        }

        private void TryForceOpen()
        {
            _viewCovered = false;
            if (_view == null) return;
            try { _ = _view.HideAsync(); }
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
