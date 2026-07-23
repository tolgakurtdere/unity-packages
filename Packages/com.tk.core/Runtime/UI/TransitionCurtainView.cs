using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// A transition curtain visual. The one contract: when <see cref="ShowAsync"/> returns, the
    /// screen is FULLY covered (no swap frame can leak); <see cref="HideAsync"/> animates open.
    /// </summary>
    public interface ITransitionCurtainView
    {
        Awaitable ShowAsync();
        Awaitable HideAsync();

        /// <summary>Snap to FULL cover synchronously — no animation, no frame yield.</summary>
        void ShowInstantly();

        /// <summary>Snap fully open synchronously.</summary>
        void HideInstantly();
    }

    /// <summary>
    /// MonoBehaviour base for curtain prefabs registered under the "TransitionCurtain" catalog
    /// key. Subclass only for custom animation behavior (Animator, tweens, wipes); for a custom
    /// VISUAL with a plain fade, put <see cref="FadeCurtainView"/> on your own prefab instead —
    /// no code needed. Since 0.7.0 the seam also carries the instant pair (ShowInstantly/HideInstantly); implementations snap synchronously, no frame yield.
    /// </summary>
    public abstract class TransitionCurtainView : MonoBehaviour, ITransitionCurtainView
    {
        public abstract Awaitable ShowAsync();
        public abstract Awaitable HideAsync();
        public abstract void ShowInstantly();
        public abstract void HideInstantly();
    }
}
