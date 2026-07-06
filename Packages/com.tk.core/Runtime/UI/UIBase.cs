using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    // Removed RequireComponent to allow flexible hierarchy (Canvas as child)
    public abstract class UIBase : MonoBehaviour, IBackButtonSignalReceiver
    {
        public bool IsFocused => UIManager.Instance.IsFocused(this);

        public bool IsShown { get; private set; }

        protected Canvas Canvas { get; private set; }
        protected CanvasGroup CanvasGroup { get; private set; }
        protected Image ForegroundDisabler { get; private set; }

        protected static readonly Color TransparentColor = Color.clear;

        protected virtual void Awake()
        {
            // Look for Canvas in children (inclusive of self)
            Canvas = GetComponentInChildren<Canvas>(true);

            if (!Canvas)
            {
                Debug.LogError($"[UIBase] {gameObject.name} -> No Canvas found in hierarchy!");
                return;
            }

            // Ensure CanvasGroup exists on the Canvas object
            CanvasGroup = Canvas.GetComponent<CanvasGroup>();
            if (!CanvasGroup)
            {
                CanvasGroup = Canvas.gameObject.AddComponent<CanvasGroup>();
            }

            // Create Foreground Disabler to block clicks during animations
            CreateForegroundDisabler();

            // Initial state
            gameObject.SetActive(false);
            Canvas.enabled = false;
        }

        private void CreateForegroundDisabler()
        {
            var disablerObj = new GameObject($"{nameof(ForegroundDisabler)} (Built-in)");
            // Parent it to the Canvas so it renders correctly
            disablerObj.transform.SetParent(Canvas.transform);
            disablerObj.transform.localScale = Vector3.one;
            disablerObj.transform.localPosition = Vector3.zero;

            ForegroundDisabler = disablerObj.AddComponent<Image>();
            ForegroundDisabler.color = TransparentColor;
            ForegroundDisabler.raycastTarget = true; // Blocks clicks

            // Stretch to fill
            var rect = ForegroundDisabler.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            ForegroundDisabler.enabled = false;
        }

        public virtual async Awaitable SetActivationStateAsync(bool isActive, bool skipAnimations = false)
        {
            if (IsShown == isActive) return; // idempotent: double-show / double-hide is a no-op

            // Awake failed to find a Canvas (already logged) — a misconfigured prefab cannot be shown.
            if (!Canvas || !ForegroundDisabler) return;

            IsShown = isActive;

            ForegroundDisabler.enabled = true; // Block interactions

            if (isActive)
            {
                gameObject.SetActive(true);
                Canvas.enabled = true;

                OnUIEnter();

                if (!skipAnimations)
                {
                    await PlayDisplayAnimationAsync();
                }
            }
            else
            {
                if (!skipAnimations)
                {
                    await PlayHideAnimationAsync();
                }

                OnUIExit();

                gameObject.SetActive(false);
                Canvas.enabled = false;
            }

            if (ForegroundDisabler)
                ForegroundDisabler.enabled = false; // Unblock
        }

        /// <summary>Blocks or unblocks pointer input on this UI without changing visibility.</summary>
        public void SetRaycastsBlocked(bool blocked)
        {
            if (CanvasGroup) CanvasGroup.blocksRaycasts = !blocked;
        }

        public abstract void OnBackButtonSignalReceived();

        protected abstract Awaitable PlayDisplayAnimationAsync();
        protected abstract Awaitable PlayHideAnimationAsync();
        protected abstract void OnUIEnter();
        protected abstract void OnUIExit();
    }
}
