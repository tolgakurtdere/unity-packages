using NUnit.Framework;
using TK.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TK.Core.Tests
{
    [TestFixture]
    public class TabBarViewTests
    {
        private sealed class TestTabBarView : TabBarView
        {
            public void InvokeAwake() => Awake();
        }

        private GameObject _rootGo;
        private TestTabBarView _view;
        private RectTransform _buttonContainer;
        private TabBarConfig _config;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("TabBar", typeof(RectTransform));
            _view = _rootGo.AddComponent<TestTabBarView>();

            var containerGo = new GameObject("Buttons", typeof(RectTransform));
            containerGo.transform.SetParent(_rootGo.transform, worldPositionStays: false);
            _buttonContainer = (RectTransform)containerGo.transform;

            var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Button));
            templateGo.transform.SetParent(_buttonContainer, worldPositionStays: false);

            _config = ScriptableObject.CreateInstance<TabBarConfig>();
            var configSo = new SerializedObject(_config);
            var tabs = configSo.FindProperty("tabs");
            tabs.arraySize = 3;
            SetEntry(tabs, 0, "Home", "HOME");
            SetEntry(tabs, 1, "Shop", "SHOP");
            SetEntry(tabs, 2, "Daily", "DAILY");
            configSo.ApplyModifiedPropertiesWithoutUndo();

            var viewSo = new SerializedObject(_view);
            viewSo.FindProperty("config").objectReferenceValue = _config;
            viewSo.FindProperty("buttonContainer").objectReferenceValue = _buttonContainer;
            viewSo.FindProperty("buttonTemplate").objectReferenceValue = templateGo.GetComponent<Button>();
            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEntry(SerializedProperty tabs, int index, string key, string label)
        {
            var entry = tabs.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("layoutKey").stringValue = key;
            entry.FindPropertyRelative("label").stringValue = label;
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootGo) Object.DestroyImmediate(_rootGo);
            if (_config) Object.DestroyImmediate(_config);
            _rootGo = null;
            _config = null;
        }

        [Test]
        public void Awake_BuildsOrderedButtonsFromConfig()
        {
            _view.InvokeAwake();

            Assert.AreEqual(3, _view.TabCount);
            Assert.AreEqual(0, _view.GetTabIndex("Home"));
            Assert.AreEqual(1, _view.GetTabIndex("Shop"));
            Assert.AreEqual(2, _view.GetTabIndex("Daily"));
            Assert.AreEqual(-1, _view.GetTabIndex("Nope"), "Unknown keys report -1.");

            Assert.IsTrue(_view.TryGetTabKey(1, out var key));
            Assert.AreEqual("Shop", key);
            Assert.IsFalse(_view.TryGetTabKey(3, out _));
            Assert.IsFalse(_view.TryGetTabKey(-1, out _));

            Assert.IsNotNull(_buttonContainer.Find("Tab_Home"));
            Assert.IsFalse(_buttonContainer.Find("Template").gameObject.activeSelf, "Template stays inactive.");
        }

        [Test]
        public void Click_RaisesTabSelectedWithTheLayoutKey()
        {
            _view.InvokeAwake();
            string selected = null;
            _view.TabSelected += key => selected = key;

            _buttonContainer.Find("Tab_Shop").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual("Shop", selected);
        }

        [Test]
        public void Click_OnTheSelectedTab_DoesNotRaise()
        {
            _view.InvokeAwake();
            _view.SetSelected("Shop");
            var raised = false;
            _view.TabSelected += _ => raised = true;

            _buttonContainer.Find("Tab_Shop").GetComponent<Button>().onClick.Invoke();

            Assert.IsFalse(raised, "Re-tapping the active tab must not fire navigation.");
        }

        [Test]
        public void SetSelected_UnknownKey_LogsWarning()
        {
            _view.InvokeAwake();

            LogAssert.Expect(LogType.Warning, "[TabBarView] Unknown tab key 'Nope' — all tabs deselected.");
            _view.SetSelected("Nope");
        }

        [Test]
        public void SetSelected_KnownKey_DoesNotWarn()
        {
            _view.InvokeAwake();

            _view.SetSelected("Home");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SetVisible_TogglesTheGameObject()
        {
            _view.InvokeAwake();

            _view.SetVisible(false);
            Assert.IsFalse(_rootGo.activeSelf);

            _view.SetVisible(true);
            Assert.IsTrue(_rootGo.activeSelf);
        }

        [Test]
        public void Awake_WithMissingReferences_LogsErrorAndBuildsNothing()
        {
            var bareGo = new GameObject("BareTabBar", typeof(RectTransform));
            try
            {
                var bare = bareGo.AddComponent<TestTabBarView>();

                LogAssert.Expect(LogType.Error, "[TabBarView] Config, container and template must be assigned.");
                bare.InvokeAwake();

                Assert.AreEqual(0, bare.TabCount);
                Assert.AreSame(TabTransitionSettings.Default, bare.TransitionSettings,
                    "Without a config the shared default settings are used.");
            }
            finally
            {
                Object.DestroyImmediate(bareGo);
            }
        }

        [Test]
        public void TransitionSettings_ComeFromTheConfig()
        {
            _view.InvokeAwake();

            Assert.AreSame(_config.Transition, _view.TransitionSettings);
        }

        [Test]
        public void Awake_SkipsDuplicateKeysWithAnError()
        {
            var configSo = new SerializedObject(_config);
            var tabs = configSo.FindProperty("tabs");
            tabs.arraySize = 4;
            SetEntry(tabs, 3, "Home", "HOME AGAIN"); // duplicate of entry 0
            configSo.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, "[TabBarView] Duplicate tab key 'Home' in config — skipped.");
            _view.InvokeAwake();

            Assert.AreEqual(3, _view.TabCount, "The duplicate entry must not produce a button.");
            Assert.AreEqual(0, _view.GetTabIndex("Home"), "The FIRST occurrence keeps its slot.");
        }

        [Test]
        public void Awake_SkipsEmptyKeysWithAnError()
        {
            var configSo = new SerializedObject(_config);
            var tabs = configSo.FindProperty("tabs");
            tabs.arraySize = 4;
            SetEntry(tabs, 3, "", "BLANK");
            configSo.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, "[TabBarView] Config entry with an empty layoutKey — skipped.");
            _view.InvokeAwake();

            Assert.AreEqual(3, _view.TabCount, "The empty-key entry must not produce a button.");
        }
    }
}
