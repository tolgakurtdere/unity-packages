using TK.Core.UI;
using UnityEngine;

namespace TK.Core.Tests
{
    /// <summary>
    /// Holder for the shared test layout. The MonoBehaviour is deliberately NESTED: a top-level
    /// class in an Editor folder gets a MonoScript, and Unity refuses AddComponent on editor
    /// scripts ("Can't add script behaviour 'TestLayout' because it is an editor script") —
    /// AddComponent then returns null. Nested classes have no MonoScript, so the check is
    /// skipped (same mechanism the fixture-nested test MonoBehaviours rely on).
    /// Consumers alias it: <c>using TestLayout = TK.Core.Tests.TestUi.TestLayout;</c>
    /// </summary>
    internal static class TestUi
    {
        /// <summary>Concrete LayoutBase (instant animations); Awake is invoked manually in EditMode.</summary>
        internal sealed class TestLayout : LayoutBase
        {
            public void InvokeAwake() => Awake();
            public CanvasGroup ExposedCanvasGroup => CanvasGroup;
        }
    }
}
