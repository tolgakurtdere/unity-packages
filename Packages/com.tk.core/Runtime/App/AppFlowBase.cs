using System;
using TK.Core.Save;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Base composition root for the app flow. Owns AppContext, LevelProgressService and the
    /// transition lock; the game implements what "show the menu" and "start level N" mean.
    /// On Start: resumes a saved session if the game reports one, otherwise shows the menu.
    /// </summary>
    public abstract class AppFlowBase : MonoBehaviour
    {
        protected AppContext Context { get; private set; }
        protected LevelProgressService LevelProgress { get; private set; }

        /// <summary>True while a navigation transition is running. UI can bind buttons to CanNavigate.</summary>
        public bool IsTransitioning { get; private set; }

        public bool CanNavigate => !IsTransitioning;

        /// <summary>How many levels the game has (used by LevelProgressService).</summary>
        protected abstract int LevelCount { get; }

        /// <summary>Show the game's menu UI. Runs inside the transition lock.</summary>
        protected abstract Awaitable ShowMenuAsync();

        /// <summary>Load and start the given level. Runs inside the transition lock.</summary>
        protected abstract Awaitable StartLevelAsync(int levelIndex);

        /// <summary>Swap the save backend if needed (cloud, file, ...).</summary>
        protected virtual ISaveSystem CreateSaveSystem() => new PlayerPrefsJsonSaveSystem();

        /// <summary>Register game services into the context (called once, end of Awake).</summary>
        protected virtual void RegisterServices(AppContext context) { }

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

        /// <summary>Game-end hook (show result popup, advance flow, ...). Fired via AppContext.RaiseGameEnded.</summary>
        protected virtual void OnGameEnded(GameEndResult result) { }

        protected virtual void Awake()
        {
            Context = new AppContext(CreateSaveSystem());
            LevelProgress = new LevelProgressService(Context.SaveSystem, LevelCount);
            Context.OnGameEnded += HandleGameEnded;
            gameObject.AddComponent<AppLifecycleRelay>().Initialize(Context);
            RegisterServices(Context);
        }

        protected virtual async void Start()
        {
            try
            {
                if (TryGetResumeState(out var resumeIndex))
                {
                    await RunTransitionAsync(() =>
                    {
                        LevelProgress.SetLevelIndex(resumeIndex);
                        return ResumeAsync(LevelProgress.CurrentLevelIndex);
                    });
                }
                else
                {
                    await RunTransitionAsync(ShowMenuAsync);
                }
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

        /// <summary>Starts a specific level (e.g. from level select).</summary>
        public async Awaitable PlayLevelAsync(int levelIndex)
        {
            await RunTransitionAsync(() =>
            {
                LevelProgress.SetLevelIndex(levelIndex);
                return StartLevelAsync(LevelProgress.CurrentLevelIndex);
            });
        }

        /// <summary>Starts the current level (menu Play button — current starts at the frontier).</summary>
        public async Awaitable PlayCurrentLevelAsync()
        {
            await RunTransitionAsync(() => StartLevelAsync(LevelProgress.CurrentLevelIndex));
        }

        /// <summary>Advances progression and starts the next level (win flow).</summary>
        public async Awaitable PlayNextLevelAsync()
        {
            await RunTransitionAsync(() => StartLevelAsync(LevelProgress.AdvanceToNextLevel()));
        }

        /// <summary>Restarts the current level (lose/retry flow).</summary>
        public async Awaitable RetryLevelAsync()
        {
            await RunTransitionAsync(() => StartLevelAsync(LevelProgress.CurrentLevelIndex));
        }

        /// <summary>Leaves gameplay and shows the menu.</summary>
        public async Awaitable ReturnToMenuAsync()
        {
            await RunTransitionAsync(ShowMenuAsync);
        }

        /// <summary>
        /// Runs a navigation operation under the transition lock. Re-entrant calls while a
        /// transition is running are DROPPED (spam-safe) — bind button interactability to
        /// CanNavigate for feedback. The operation body only evaluates once the lock is held.
        /// </summary>
        protected async Awaitable RunTransitionAsync(Func<Awaitable> operation)
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

        private void HandleGameEnded(GameEndResult result) => OnGameEnded(result);
    }
}
