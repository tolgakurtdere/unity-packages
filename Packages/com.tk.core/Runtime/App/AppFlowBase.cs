using System;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Level-based composition root: AppRootBase plus level progression. The game implements what
    /// "show the menu" and "start level N" mean; this base adds LevelProgress, the
    /// Play/Retry/Return verbs, the level lifecycle hooks (OnBeforeLevelStartAsync /
    /// OnAfterLevelEndAsync), and a resume-or-menu boot policy (override OnBootAsync to change
    /// it). Not level-based? Subclass AppRootBase instead — same wiring, no level API.
    /// </summary>
    public abstract class AppFlowBase : AppRootBase
    {
        protected LevelProgressService LevelProgress { get; private set; }

        /// <summary>How many levels the game has (used by LevelProgressService).</summary>
        protected abstract int LevelCount { get; }

        /// <summary>
        /// Enter the game's menu state. A semantic state hook, not a UI call — TK.Core.App never
        /// references TK.Core.UI; implement it with your own UI (or none). Runs inside the
        /// transition lock.
        /// </summary>
        protected abstract Awaitable ShowMenuAsync();

        /// <summary>Load and start the given level. Runs inside the transition lock.</summary>
        protected abstract Awaitable StartLevelAsync(int levelIndex);

        /// <summary>
        /// Report a resumable mid-run session (e.g. an unfinished level save). Return true and the
        /// level index to resume into; default = no resume, boot to menu.
        /// </summary>
        protected virtual bool TryGetResumeState(out int levelIndex)
        {
            levelIndex = 0;
            return false;
        }

        /// <summary>How to resume a reported session. Default: start that level normally.</summary>
        protected virtual Awaitable ResumeAsync(int levelIndex) => StartLevelAsync(levelIndex);

        /// <summary>
        /// Runs inside the transition lock before every level entry this base initiates (the
        /// Play/Retry verbs and boot-resume). Return false to veto the start — the natural seam
        /// for a lives/energy gate or a pre-level interstitial, without overriding the public
        /// verbs. Note PlayNextLevelAsync advances progression before this runs: a veto keeps the
        /// advanced frontier but starts nothing.
        /// </summary>
        protected virtual Awaitable<bool> OnBeforeLevelStartAsync(int levelIndex) => CompletedAwaitable(true);

        /// <summary>
        /// Runs after the synchronous OnGameEnded hook on every game end (fire-and-forget;
        /// exceptions are logged) — the seam for post-level flows like an interstitial, reward
        /// grant, or autosave.
        /// </summary>
        protected virtual Awaitable OnAfterLevelEndAsync(GameEndResult result) => CompletedAwaitable();

        /// <summary>
        /// Boot policy: resumes a reported session (TryGetResumeState) or shows the menu. Override
        /// to boot elsewhere — straight into a level, a consent/tutorial flow, ... The public
        /// verbs (PlayCurrentLevelAsync, etc.) are safe to call from an override; they take the
        /// transition lock themselves.
        /// </summary>
        protected override async Awaitable OnBootAsync()
        {
            if (TryGetResumeState(out var resumeIndex))
            {
                await RunTransitionAsync(() =>
                {
                    LevelProgress.SetLevelIndex(resumeIndex);
                    return ResumeWithHookAsync(LevelProgress.CurrentLevelIndex);
                });
            }
            else
            {
                await RunTransitionAsync(ShowMenuAsync);
            }
        }

        protected override void OnContextCreated()
        {
            base.OnContextCreated();
            LevelProgress = new LevelProgressService(Context.SaveSystem, LevelCount);
        }

        /// <summary>Starts a specific level (e.g. from level select).</summary>
        public async Awaitable PlayLevelAsync(int levelIndex)
        {
            await RunTransitionAsync(() =>
            {
                LevelProgress.SetLevelIndex(levelIndex);
                return StartLevelWithHookAsync(LevelProgress.CurrentLevelIndex);
            });
        }

        /// <summary>Starts the current level (menu Play button — current starts at the frontier).</summary>
        public async Awaitable PlayCurrentLevelAsync()
        {
            await RunTransitionAsync(() => StartLevelWithHookAsync(LevelProgress.CurrentLevelIndex));
        }

        /// <summary>Advances progression and starts the next level (win flow).</summary>
        public async Awaitable PlayNextLevelAsync()
        {
            await RunTransitionAsync(() => StartLevelWithHookAsync(LevelProgress.AdvanceToNextLevel()));
        }

        /// <summary>Restarts the current level (lose/retry flow).</summary>
        public async Awaitable RetryLevelAsync()
        {
            await RunTransitionAsync(() => StartLevelWithHookAsync(LevelProgress.CurrentLevelIndex));
        }

        /// <summary>Leaves gameplay and shows the menu.</summary>
        public async Awaitable ReturnToMenuAsync()
        {
            await RunTransitionAsync(ShowMenuAsync);
        }

        private protected override void HandleGameEnded(GameEndResult result)
        {
            base.HandleGameEnded(result); // the sync OnGameEnded hook, exactly as before
            DispatchAfterLevelEnd(result);
        }

        private async void DispatchAfterLevelEnd(GameEndResult result)
        {
            try
            {
                await OnAfterLevelEndAsync(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Awaitable StartLevelWithHookAsync(int levelIndex)
        {
            if (!await OnBeforeLevelStartAsync(levelIndex)) return;
            await StartLevelAsync(levelIndex);
        }

        private async Awaitable ResumeWithHookAsync(int levelIndex)
        {
            if (!await OnBeforeLevelStartAsync(levelIndex)) return;
            await ResumeAsync(levelIndex);
        }
    }
}
