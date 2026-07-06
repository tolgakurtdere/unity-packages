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
                var index = _order.Count;
                var button = Instantiate(buttonTemplate, buttonContainer);
                button.gameObject.SetActive(true);
                button.name = $"Tab_{tab.layoutKey}";

                var key = tab.layoutKey;
                var presenter = FindPresenter(button);
                var data = new TabButtonData(key, tab.label, index);
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
