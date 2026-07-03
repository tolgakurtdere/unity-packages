using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    public abstract class PopupBase : UIBase
    {
        [Header("Animation Settings")]
        [SerializeField] protected RectTransform animationContainer;
        [SerializeField] protected Image backgroundDisabler;

        protected virtual float AnimationDuration => 0.3f;

        private IUITransition _transition;

        /// <summary>Override to plug a custom transition (see PrimeTween/DOTween samples).</summary>
        protected virtual IUITransition CreateTransition() => new DefaultPopupTransition();

        private IUITransition Transition => _transition ??= CreateTransition();

        protected override void Awake()
        {
            base.Awake();

            // Should be assigned in Inspector, but fallback to transform if null
            if (!animationContainer)
                animationContainer = GetComponent<RectTransform>();
        }

        public override void OnBackButtonSignalReceived()
        {
            // Default: Request UIManager to close me
            // This ensures queue logic and stack management are handled centrally.
            _ = UIManager.Instance.ClosePopup(this);
        }

        protected override void OnUIEnter()
        {
            UIManager.Instance.AddToStack(this);
        }

        protected override void OnUIExit()
        {
            UIManager.Instance.RemoveFromStack(this);
        }

        protected override async Awaitable PlayDisplayAnimationAsync()
        {
            if (!animationContainer) return;
            await Transition.PlayShowAsync(new UITransitionContext(CanvasGroup, animationContainer, backgroundDisabler, AnimationDuration));
        }

        protected override async Awaitable PlayHideAnimationAsync()
        {
            if (!animationContainer) return;
            await Transition.PlayHideAsync(new UITransitionContext(CanvasGroup, animationContainer, backgroundDisabler, AnimationDuration));
        }
    }
}
