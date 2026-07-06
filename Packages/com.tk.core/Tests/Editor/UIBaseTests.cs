using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using TestLayout = TK.Core.Tests.TestUi.TestLayout;

namespace TK.Core.Tests
{
    [TestFixture]
    public class UIBaseTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void SetRaycastsBlocked_TogglesBlocksRaycastsOnTheAwakeCreatedGroup()
        {
            _go = new GameObject("TestLayout", typeof(Canvas));
            var ui = _go.AddComponent<TestLayout>();

            ui.InvokeAwake();

            Assert.IsNotNull(ui.ExposedCanvasGroup, "Awake must guarantee a CanvasGroup on the Canvas.");
            Assert.IsTrue(ui.ExposedCanvasGroup.blocksRaycasts);

            ui.SetRaycastsBlocked(true);
            Assert.IsFalse(ui.ExposedCanvasGroup.blocksRaycasts, "Blocked UI must not receive raycasts.");

            ui.SetRaycastsBlocked(false);
            Assert.IsTrue(ui.ExposedCanvasGroup.blocksRaycasts, "Unblocking must restore raycasts.");
        }

        [Test]
        public void SetRaycastsBlocked_DoesNotChangeVisibility()
        {
            _go = new GameObject("TestLayout", typeof(Canvas));
            var ui = _go.AddComponent<TestLayout>();
            ui.InvokeAwake();

            ui.SetRaycastsBlocked(true);

            Assert.IsFalse(ui.IsShown, "Raycast blocking is orthogonal to show state.");
            Assert.AreEqual(1f, ui.ExposedCanvasGroup.alpha, "Alpha must stay under package control.");
        }

        [Test]
        public void SetRaycastsBlocked_WithoutCanvas_IsSafe()
        {
            _go = new GameObject("NoCanvas");
            var ui = _go.AddComponent<TestLayout>();
            LogAssert.Expect(LogType.Error, "[UIBase] NoCanvas -> No Canvas found in hierarchy!");
            ui.InvokeAwake();

            Assert.DoesNotThrow(() => ui.SetRaycastsBlocked(true));
        }
    }
}
