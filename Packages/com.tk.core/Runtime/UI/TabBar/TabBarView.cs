using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    /// <summary>
    /// Persistent bottom tab bar. Builds buttons from TabBarConfig and raises
    /// TabSelected(layoutKey) outward; switching layouts is the composition layer's job.
    /// Lives outside every layout as a persistent shell in the main scene.
    /// </summary>
    public class TabBarView : MonoBehaviour
    {
        [SerializeField] private TabBarConfig config;
        [SerializeField] private RectTransform buttonContainer;
        [SerializeField] private Button buttonTemplate;

        private readonly Dictionary<string, TabButtonHandle> _buttons = new();
        private readonly List<string> _order = new();
        private string _selectedKey;

        /// <summary>Raised with the tab's layout key when the user taps a tab.</summary>
        public event Action<string> TabSelected;

        private bool _built;

        /// <summary>Builds the buttons from the config. Subclasses overriding this MUST call
        /// <c>base.Awake()</c> — Unity invokes only the most-derived implementation.</summary>
        protected virtual void Awake()
        {
            if (!config || !buttonContainer || !buttonTemplate)
            {
                Debug.LogError("[TabBarView] Config, container and template must be assigned.");
                return;
            }

            buttonTemplate.gameObject.SetActive(false);

            foreach (var tab in config.Tabs)
            {
                var key = tab.layoutKey;
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogError("[TabBarView] Config entry with an empty layoutKey — skipped.");
                    continue;
                }

                if (_buttons.ContainsKey(key))
                {
                    // Unity duplicates the previous element when a list grows in the inspector,
                    // so this is an easy authoring slip; a silent overwrite would orphan the
                    // first button (its presenter would never receive SetSelected again).
                    Debug.LogError($"[TabBarView] Duplicate tab key '{key}' in config — skipped.");
                    continue;
                }

                var index = _order.Count;
                var button = Instantiate(buttonTemplate, buttonContainer);
                button.gameObject.SetActive(true);
                button.name = $"Tab_{key}";

                var presenter = FindPresenter(button);
                var data = new TabButtonData(key, tab.label, index, tab.icon);
                if (presenter != null)
                {
                    presenter.Initialize(data, button);
                }
                else
                {
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label) label.text = tab.label;
                }

                button.onClick.AddListener(() =>
                {
                    if (key != _selectedKey)
                        TabSelected?.Invoke(key);
                });
                _buttons[key] = new TabButtonHandle(presenter);
                _order.Add(key);
            }

            _built = true;
        }

        protected virtual void Start()
        {
            // Unity calls only the most-derived Awake: a subclass override that forgets
            // base.Awake() silently skips button building (no exception, empty tab bar).
            // Fail loudly one frame later instead. Guarded on the refs so a misconfigured
            // prefab doesn't get a second, misleading error on top of the Awake one.
            if (!_built && config && buttonContainer && buttonTemplate)
                Debug.LogError("[TabBarView] Buttons were never built — an Awake override must call base.Awake().");
        }

        /// <summary>Left-to-right position of a tab; -1 when unknown. Drives the slide direction.</summary>
        public int GetTabIndex(string layoutKey) => _order.IndexOf(layoutKey);

        public int TabCount => _order.Count;

        public bool TryGetTabKey(int index, out string layoutKey)
        {
            if (index >= 0 && index < _order.Count)
            {
                layoutKey = _order[index];
                return true;
            }

            layoutKey = null;
            return false;
        }

        /// <summary>Marks a tab as the active one (visual state only; no event).</summary>
        public void SetSelected(string layoutKey)
        {
            if (!string.IsNullOrEmpty(layoutKey) && !_buttons.ContainsKey(layoutKey))
                Debug.LogWarning($"[TabBarView] Unknown tab key '{layoutKey}' — all tabs deselected.");

            var instant = string.IsNullOrEmpty(_selectedKey);
            _selectedKey = layoutKey;
            foreach (var pair in _buttons)
            {
                pair.Value.Presenter?.SetSelected(pair.Key == _selectedKey, instant);
            }
        }

        public void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf != isVisible)
                gameObject.SetActive(isVisible);
        }

        public TabTransitionSettings TransitionSettings => config ? config.Transition : TabTransitionSettings.Default;

        private static ITabButtonPresenter FindPresenter(Button button)
        {
            foreach (var behaviour in button.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is ITabButtonPresenter presenter)
                    return presenter;
            }

            return null;
        }

        private readonly struct TabButtonHandle
        {
            public TabButtonHandle(ITabButtonPresenter presenter)
            {
                Presenter = presenter;
            }

            public ITabButtonPresenter Presenter { get; }
        }
    }
}
