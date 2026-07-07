using NUnit.Framework;
using TK.Core.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TK.Core.Tests
{
    [TestFixture]
    public class UIManagerTests
    {
        private GameObject _go;
        private UIManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("UIManager", typeof(UIManager));
            _manager = _go.GetComponent<UIManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void BackInput_IsActiveByDefault()
        {
            Assert.IsTrue(_manager.BackInputEnabled);
            Assert.IsTrue(_manager.IsBackInputActive);
        }

        [Test]
        public void BackInputSuppressions_AreRefCounted_SoOverlappingFlowsCompose()
        {
            _manager.PushBackInputSuppression();
            _manager.PushBackInputSuppression(); // a second, overlapping flow

            _manager.PopBackInputSuppression();
            Assert.IsFalse(_manager.IsBackInputActive, "The other flow's suppression must still hold.");

            _manager.PopBackInputSuppression();
            Assert.IsTrue(_manager.IsBackInputActive);
        }

        [Test]
        public void MasterSwitch_AndSuppressions_AreIndependent()
        {
            _manager.BackInputEnabled = false;
            _manager.PushBackInputSuppression();
            _manager.PopBackInputSuppression();

            Assert.IsFalse(_manager.BackInputEnabled, "Suppression cycles must not write the master switch.");
            Assert.IsFalse(_manager.IsBackInputActive);

            _manager.BackInputEnabled = true;
            Assert.IsTrue(_manager.IsBackInputActive);
        }

        [Test]
        public void UnbalancedPop_ThrowsLoudly()
        {
            Assert.That(() => _manager.PopBackInputSuppression(),
                Throws.InvalidOperationException, "Unbalanced pops are a caller bug and must not pass silently.");
        }
    }
}
