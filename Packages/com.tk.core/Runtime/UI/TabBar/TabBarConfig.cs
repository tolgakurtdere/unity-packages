using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Config for the persistent bottom tab bar. Tabs are pure data:
    /// reordering, adding, and removing tabs is an asset edit, not code.
    /// </summary>
    [CreateAssetMenu(fileName = "TabBarConfig", menuName = "TK/UI Tab Bar Config")]
    public class TabBarConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("UICatalog layout key this tab opens (e.g. 'Home').")]
            public string layoutKey;

            [Tooltip("Text shown by the default tab button presenter.")]
            public string label;
        }

        [SerializeField] private List<Entry> tabs = new();

        [Tooltip("Timing and easing used when switching between ordered tab layouts.")]
        [SerializeField] private TabTransitionSettings transition = new();

        public IReadOnlyList<Entry> Tabs => tabs;
        public TabTransitionSettings Transition => transition ?? TabTransitionSettings.Default;
    }
}
