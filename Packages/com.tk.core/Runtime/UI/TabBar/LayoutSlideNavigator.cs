using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Slides between tab layouts: the current layout moves out while the target moves in
    /// from the opposite edge, both visible during the motion. Layout instances are loaded
    /// once, registered here, and re-positioned instead of re-instantiated.
    /// Interrupted ordered slides keep their current visual position so the next slide can
    /// retarget without jumping; <see cref="Current"/> only ever points at a layout that
    /// fully arrived, never at an abandoned slide target.
    /// </summary>
    public sealed class LayoutSlideNavigator
    {
        private readonly Dictionary<string, LayoutBase> _layouts = new();
        private readonly IOrderedTabTransition _defaultOrderedTransition = new DefaultOrderedTabTransition();
        private float _visualPosition;
        private bool _hasVisualPosition;

        public LayoutBase Current { get; private set; }
        public IOrderedTabTransition OrderedTransition { get; set; }
        public bool HasVisualPosition => _hasVisualPosition;
        public float VisualPosition => _visualPosition;

        /// <summary>The container all registered layouts live under (set by the first registration).</summary>
        public RectTransform Container { get; private set; }

        public void Register(string key, LayoutBase layout)
        {
            if (string.IsNullOrEmpty(key) || !layout) return;

            _layouts[key] = layout;
            if (!Container)
            {
                Container = layout.transform.parent as RectTransform;
            }
            else if (layout.transform.parent != Container)
            {
                Debug.LogWarning($"[LayoutSlideNavigator] Layout '{layout.name}' is parented under " +
                                 $"'{(layout.transform.parent ? layout.transform.parent.name : "<root>")}' but the " +
                                 $"slide container is '{Container.name}' — slide math assumes one shared container.");
            }
        }

        public bool TryGet(string key, out LayoutBase layout) => _layouts.TryGetValue(key, out layout);

        public bool IsRegistered(LayoutBase layout)
        {
            if (!layout) return false;

            foreach (var registered in _layouts.Values)
            {
                if (registered == layout)
                    return true;
            }

            return false;
        }

        /// <summary>Adopts a layout shown through another path (e.g. UIManager) as the current one.</summary>
        public void SetCurrent(LayoutBase layout)
        {
            Current = layout;
            _hasVisualPosition = false;
        }

        public void SetCurrent(LayoutBase layout, int orderedIndex)
        {
            Current = layout;
            _visualPosition = orderedIndex;
            _hasVisualPosition = orderedIndex >= 0;
        }

        /// <summary>
        /// Blocks or unblocks pointer input on every registered layout (via
        /// <see cref="UIBase.SetRaycastsBlocked"/>), toggled around a navigation sequence:
        /// while layouts are sliding their content buttons (Play, Settings, in-screen
        /// shortcuts) must not be tappable, otherwise a tap mid-motion could start a second
        /// transition over a half-slid screen. The persistent tab bar lives outside these
        /// layouts, so it stays live and can still retarget the slide. The polled back input
        /// (Escape / Android back) is suppressed for the same window via
        /// <see cref="UIManager.BackInputEnabled"/> — raycast blocking cannot cover key input,
        /// and a back press mid-slide would route to a half-slid layout in the stack.
        /// </summary>
        public void SetLayoutsInteractable(bool interactable)
        {
            foreach (var layout in _layouts.Values)
            {
                if (layout) layout.SetRaycastsBlocked(!interactable);
            }

            var manager = UIManager.Instance;
            if (manager) manager.BackInputEnabled = interactable;
        }

        /// <summary>
        /// Recovers from an abandoned navigation (e.g. a retarget whose layout failed to load):
        /// re-centres the last fully shown layout and hides everything else, so no half-slid
        /// layouts stay on screen when no follow-up slide will arrive to clean them up.
        /// </summary>
        public async Awaitable SettleAsync()
        {
            _hasVisualPosition = false;
            await HideAllExceptAsync(Current);
        }

        /// <summary>
        /// Slides through every layout between the (possibly fractional) start position and the
        /// target index in one continuous motion. Returns false when interrupted or invalid;
        /// interrupted layouts keep their offsets so the next call can retarget seamlessly.
        /// </summary>
        public async Awaitable<bool> SlideThroughAsync(IReadOnlyList<LayoutBase> orderedLayouts, float startPosition,
            int targetIndex, TabTransitionSettings settings = null, Func<bool> shouldInterrupt = null)
        {
            if (orderedLayouts == null || targetIndex < 0 || targetIndex >= orderedLayouts.Count)
            {
                return false;
            }

            var target = orderedLayouts[targetIndex];
            if (!target)
            {
                return false;
            }

            settings ??= TabTransitionSettings.Default;
            _visualPosition = Mathf.Clamp(startPosition, 0f, orderedLayouts.Count - 1f);
            _hasVisualPosition = true;

            if (Mathf.Approximately(_visualPosition, targetIndex))
            {
                await target.SetActivationStateAsync(true, skipAnimations: true);
                await ArriveAtAsync(target, targetIndex);
                return true;
            }

            var firstIndex = Mathf.Min(Mathf.FloorToInt(_visualPosition), targetIndex);
            var lastIndex = Mathf.Max(Mathf.CeilToInt(_visualPosition), targetIndex);
            var width = Container ? Container.rect.width : ((RectTransform)target.transform).rect.width;
            if (width <= 0f) width = Screen.width;

            for (var index = firstIndex; index <= lastIndex; index++)
            {
                var layout = orderedLayouts[index];
                if (!layout) return false;

                var rect = (RectTransform)layout.transform;
                rect.anchoredPosition = new Vector2((index - _visualPosition) * width, 0f);

                await layout.SetActivationStateAsync(true, skipAnimations: true);
            }

            var transition = OrderedTransition ?? _defaultOrderedTransition;
            var result = await transition.PlayAsync(orderedLayouts, _visualPosition, targetIndex, Container, settings,
                shouldInterrupt);

            if (!result.Completed)
            {
                _visualPosition = result.Position;
                _hasVisualPosition = true;
                return false;
            }

            await ArriveAtAsync(target, targetIndex);
            return true;
        }

        /// <summary>Records a completed arrival and collapses the screen to the target.</summary>
        private async Awaitable ArriveAtAsync(LayoutBase target, int targetIndex)
        {
            Current = target;
            _visualPosition = targetIndex;
            _hasVisualPosition = true;
            await HideAllExceptAsync(target);
        }

        private async Awaitable HideAllExceptAsync(LayoutBase keep)
        {
            foreach (var layout in _layouts.Values)
            {
                if (!layout || layout == keep) continue;

                await layout.SetActivationStateAsync(false, skipAnimations: true);
                ((RectTransform)layout.transform).anchoredPosition = Vector2.zero;
            }

            if (keep) ((RectTransform)keep.transform).anchoredPosition = Vector2.zero;
        }
    }
}
