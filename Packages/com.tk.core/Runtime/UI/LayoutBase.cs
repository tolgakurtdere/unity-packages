using UnityEngine;

namespace TK.Core.UI
{
    public abstract class LayoutBase : UIBase
    {
        public LayoutBase PreviousLayout { get; set; }

        public override void OnBackButtonSignalReceived()
        {
            if (PreviousLayout)
            {
                // recordHistory: false because we're going BACK, not forward
                // We don't want PreviousLayout.PreviousLayout = this
                _ = UIManager.Instance.ShowLayout(PreviousLayout, recordHistory: false);
            }
            else
            {
                Debug.LogWarning($"[LayoutBase] Back pressed on {name}, but no PreviousLayout defined.");
            }
        }

        protected override void OnUIEnter()
        {
            UIManager.Instance.AddToStack(this);
        }

        protected override void OnUIExit()
        {
            UIManager.Instance.RemoveFromStack(this);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Awaitable PlayDisplayAnimationAsync()
        {
            // Instant display
        }

        protected override async Awaitable PlayHideAnimationAsync()
        {
            // Instant hide
        }
#pragma warning restore CS1998
    }
}
