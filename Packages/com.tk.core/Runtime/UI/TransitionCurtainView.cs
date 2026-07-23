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
    }

    /// <summary>
    /// MonoBehaviour base for curtain prefabs registered under the "TransitionCurtain" catalog
    /// key. Subclass only for custom animation behavior (Animator, tweens, wipes); for a custom
    /// VISUAL with a plain fade, put <see cref="FadeCurtainView"/> on your own prefab instead —
    /// no code needed.
    /// </summary>
    public abstract class TransitionCurtainView : MonoBehaviour, ITransitionCurtainView
    {
        public abstract Awaitable ShowAsync();
        public abstract Awaitable HideAsync();
    }
}
