using System;
using System.Collections.Generic;
using TK.Core.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TK.Core.UI
{
    public class UIManager : MonoBehaviour
    {
        private static UIManager s_instance;

        /// <summary>Scene singleton, resolved lazily by type. Null when no UIManager exists in the loaded scenes.</summary>
        public static UIManager Instance
        {
            get
            {
                if (!s_instance) s_instance = FindAnyObjectByType<UIManager>();
                return s_instance;
            }
        }

        // With Enter Play Mode's domain reload disabled, statics survive between play sessions —
        // drop the previous session's (destroyed) instance instead of carrying its stale shell.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_instance = null;

        [Header("Containers")]
        [SerializeField] private RectTransform layoutContainer;
        [SerializeField] private RectTransform popupContainer;
        [SerializeField] private RectTransform taskOverlayContainer;

        [Header("Catalog")]
        [SerializeField] private UICatalog catalog;

        /// <summary>
        /// The currently shown full-screen layout; null when none. Well-defined because layouts
        /// are exclusive (showing one hides the previous) — popups have no equivalent property
        /// since several can be open at once (nested stack + queue); ask per-popup via
        /// <see cref="IsFocused"/> instead. Kept in sync by the navigation stack.
        /// </summary>
        public LayoutBase ActiveLayout { get; private set; }

        /// <summary>
        /// Master switch for the polled back input (Escape / gamepad East / Android back).
        /// Game-facing and durable: nothing in the package writes it, so a game that disables
        /// back handling entirely stays disabled. Temporary flow suppressions stack on top via
        /// <see cref="PushBackInputSuppression"/>/<see cref="PopBackInputSuppression"/>.
        /// </summary>
        public bool BackInputEnabled { get; set; } = true;

        // Temporary suppressions from flows that must not be interrupted (e.g. a tab slide —
        // the raycast lock cannot cover key input). Ref-counted so overlapping flows compose.
        private readonly RefCountLock _backInputSuppressions = new();

        /// <summary>True when a back press would be handled right now: the master switch is on AND no flow is suppressing.</summary>
        public bool IsBackInputActive => BackInputEnabled && !_backInputSuppressions.IsLocked;

        /// <summary>
        /// Temporarily suppresses back input without touching <see cref="BackInputEnabled"/>.
        /// Ref-counted — every push must be paired with one <see cref="PopBackInputSuppression"/>.
        /// <see cref="LayoutSlideNavigator.SetLayoutsInteractable"/> uses this around tab navigation.
        /// </summary>
        public void PushBackInputSuppression() => _backInputSuppressions.Lock();

        /// <summary>Releases one suppression acquired by <see cref="PushBackInputSuppression"/>. Throws when unbalanced.</summary>
        public void PopBackInputSuppression() => _backInputSuppressions.Unlock();

        /// <summary>The catalog string-key APIs resolve against. Assign in inspector or set at bootstrap.</summary>
        public UICatalog Catalog { get => catalog; set => catalog = value; }

        // Navigation Stack for nested popups (Back button handling)
        private readonly List<IBackButtonSignalReceiver> _navigationStack = new();

        // Queue for sequential popups (e.g. offers shown one after another)
        private readonly Queue<PopupBase> _popupQueue = new();

        // Track the currently showing popup from the queue
        private PopupBase _activeQueuePopup;

        // Addressable instance cache
        private readonly Dictionary<AssetReferenceGameObject, UIBase> _loadedInstances = new();

        private void Awake()
        {
            if (s_instance && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        private void Update()
        {
            // Global Back Button Handler
            var backPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame ||
                              Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (backPressed && IsBackInputActive)
            {
                HandleBackInput();
            }
        }

        /// <summary>
        /// Shows a full-screen layout. Hides the previous one.
        /// </summary>
        /// <param name="layout">The layout to show.</param>
        /// <param name="recordHistory">If true, sets layout.PreviousLayout to current ActiveLayout for Back navigation.</param>
        public async Awaitable ShowLayout(LayoutBase layout, bool recordHistory = true)
        {
            if (ActiveLayout == layout) return;

            // Close all popups first
            await CloseAllPopups();

            // Record history for Back button navigation
            if (recordHistory && layout && ActiveLayout)
            {
                layout.PreviousLayout = ActiveLayout;
            }

            if (ActiveLayout)
            {
                await ActiveLayout.SetActivationStateAsync(false);
            }

            // ActiveLayout is updated via AddToStack called by layout.OnUIEnter
            if (layout)
            {
                await layout.SetActivationStateAsync(true);
            }
        }

        /// <summary>
        /// Shows a popup based on the specified mode.
        /// </summary>
        /// <param name="popup">The popup to show.</param>
        /// <param name="mode">The display mode (Queue, Immediate, Optional).</param>
        public async Awaitable ShowPopup(PopupBase popup, PopupShowMode mode = PopupShowMode.Queue)
        {
            switch (mode)
            {
                case PopupShowMode.Queue:
                    _popupQueue.Enqueue(popup);
                    await TryShowNextQueuePopup();
                    break;

                case PopupShowMode.Optional:
                    // Discard if any popup is active or queue is busy
                    if (IsAnyPopupActive() || _activeQueuePopup || _popupQueue.Count > 0)
                    {
                        Debug.Log($"[UIManager] Optional popup '{popup.name}' discarded because UI is busy.");

                        // Only release if NOT in use elsewhere (Queue, Active, Stack)
                        // Because GetOrLoadAsync returns cached instances, we might be holding a shared instance that is already queued.
                        var isUsed = _activeQueuePopup == popup || _popupQueue.Contains(popup) || _navigationStack.Contains(popup);
                        if (!isUsed) Addressables.ReleaseInstance(popup.gameObject);

                        return;
                    }

                    // Otherwise treat as Immediate
                    await popup.SetActivationStateAsync(true);
                    break;

                case PopupShowMode.Immediate:
                default:
                    await popup.SetActivationStateAsync(true);
                    break;
            }
        }

        private bool IsAnyPopupActive()
        {
            // Check if any active item in stack is a popup
            for (var i = _navigationStack.Count - 1; i >= 0; i--)
            {
                if (_navigationStack[i] is PopupBase) return true;
            }

            return false;
        }

        /// <summary>
        /// Loads a popup via Addressables (cached), parents it under PopupContainer, and shows it.
        /// </summary>
        public async Awaitable<T> ShowPopupAsync<T>(AssetReferenceGameObject reference, PopupShowMode mode = PopupShowMode.Queue)
            where T : PopupBase
        {
            var popup = await GetOrLoadAsync<T>(reference, popupContainer);
            if (!popup) return null;
            await ShowPopup(popup, mode);
            return popup;
        }

        /// <summary>
        /// Loads a layout via Addressables (cached), parents it under LayoutContainer, and shows it.
        /// </summary>
        public async Awaitable<T> ShowLayoutAsync<T>(AssetReferenceGameObject reference, bool recordHistory = true)
            where T : LayoutBase
        {
            var layout = await GetOrLoadAsync<T>(reference, layoutContainer);
            if (!layout) return null;
            await ShowLayout(layout, recordHistory);
            return layout;
        }

        /// <summary>
        /// Returns a previously loaded UI instance from cache, or null if not loaded.
        /// </summary>
        public T GetLoadedInstance<T>(AssetReferenceGameObject reference) where T : UIBase
        {
            if (reference != null && _loadedInstances.TryGetValue(reference, out var cached) && cached)
                return cached as T;
            return null;
        }

        /// <summary>
        /// Loads a popup via Addressables (cached) without showing it.
        /// Use this when you need to initialize/setup the popup before display.
        /// </summary>
        public async Awaitable<T> LoadPopupAsync<T>(AssetReferenceGameObject reference)
            where T : PopupBase
        {
            return await GetOrLoadAsync<T>(reference, popupContainer);
        }

        /// <summary>
        /// Resolves a layout by catalog key, then loads and shows it.
        /// </summary>
        public async Awaitable<T> ShowLayoutAsync<T>(string key, bool recordHistory = true) where T : LayoutBase
            => catalog && catalog.TryGet(key, out var reference) ? await ShowLayoutAsync<T>(reference, recordHistory) : LogMissing<T>(key);

        /// <summary>
        /// Resolves a popup by catalog key, then loads and shows it.
        /// </summary>
        public async Awaitable<T> ShowPopupAsync<T>(string key, PopupShowMode mode = PopupShowMode.Queue) where T : PopupBase
            => catalog && catalog.TryGet(key, out var reference) ? await ShowPopupAsync<T>(reference, mode) : LogMissing<T>(key);

        /// <summary>
        /// Resolves a popup by catalog key and loads it without showing it.
        /// </summary>
        public async Awaitable<T> LoadPopupAsync<T>(string key) where T : PopupBase
            => catalog && catalog.TryGet(key, out var reference) ? await LoadPopupAsync<T>(reference) : LogMissing<T>(key);

        private T LogMissing<T>(string key) where T : UIBase
        {
            Debug.LogError($"[UIManager] Cannot resolve UI key '{key}' (catalog: {(catalog ? catalog.name : "none assigned")}).");
            return null;
        }

        /// <summary>
        /// Returns a cached instance or loads it from Addressables.
        /// </summary>
        private async Awaitable<T> GetOrLoadAsync<T>(AssetReferenceGameObject reference, Transform parent)
            where T : UIBase
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogError($"[UIManager] Asset reference for {typeof(T).Name} is invalid!");
                return null;
            }

            if (_loadedInstances.TryGetValue(reference, out var cached) && cached)
                return (T)cached;

            var go = await Addressables.InstantiateAsync(reference, parent).Task;
            var instance = go.GetComponent<T>();

            if (!instance)
            {
                Debug.LogError($"[UIManager] Prefab '{go.name}' does not have component {typeof(T).Name}! Releasing instance.");
                Addressables.ReleaseInstance(go);
                return null;
            }

            _loadedInstances[reference] = instance;

            // Start hidden
            go.SetActive(false);
            return instance;
        }

        /// <summary>
        /// Releases an Addressable UI instance and removes it from cache.
        /// </summary>
        public void ReleaseUI(AssetReferenceGameObject reference)
        {
            if (!_loadedInstances.TryGetValue(reference, out var instance)) return;
            if (instance) Addressables.ReleaseInstance(instance.gameObject);
            _loadedInstances.Remove(reference);
        }

        /// <summary>
        /// Central method to close a popup. Handles stack removal and queue progression.
        /// </summary>
        public async Awaitable ClosePopup(PopupBase popup)
        {
            // 1. Deactivate the popup
            await popup.SetActivationStateAsync(false);

            // 2. Check if this was the active queue popup
            if (_activeQueuePopup == popup)
            {
                _activeQueuePopup = null;
                _ = TryShowNextQueuePopup();
            }
        }

        /// <summary>
        /// Closes all currently open popups (navigation stack + queue).
        /// Use before showing a result screen to prevent visual conflicts.
        /// </summary>
        public async Awaitable CloseActivePopupsAsync()
        {
            await CloseAllPopups();
        }

        #region Task Overlay API

        /// <summary>Catalog key the busy/task overlay prefab is resolved from.</summary>
        public const string TaskOverlayKey = "TaskOverlay";

        private TaskOverlayPopup _taskOverlayInstance;

        /// <summary>
        /// Shows the busy/task overlay and returns this caller's id (pass it to <see cref="HideTask"/>).
        /// Holds are ref-counted per caller: the overlay stays up until every caller has released.
        /// Returns Guid.Empty when the overlay can't be resolved from the catalog.
        /// </summary>
        public async Awaitable<Guid> ShowTask(bool isTransparent = false, bool forceShowBackgroundIfItIsNotTransparent = false)
        {
            if (!_taskOverlayInstance)
            {
                if (!catalog || !catalog.TryGet(TaskOverlayKey, out var reference))
                {
                    Debug.LogWarning($"[UIManager] Cannot show task overlay: no '{TaskOverlayKey}' entry (catalog: {(catalog ? catalog.name : "none assigned")}).");
                    return Guid.Empty;
                }

                _taskOverlayInstance = await GetOrLoadAsync<TaskOverlayPopup>(reference, taskOverlayContainer);
            }

            if (_taskOverlayInstance)
            {
                _taskOverlayInstance.Show(out var callerId, isTransparent, forceShowBackgroundIfItIsNotTransparent);
                return callerId;
            }

            return Guid.Empty;
        }

        /// <summary>Releases one <see cref="ShowTask"/> hold; the overlay hides once the last caller releases.</summary>
        public void HideTask(Guid taskId)
        {
            if (_taskOverlayInstance)
            {
                _taskOverlayInstance.Hide(taskId);
            }
        }

        /// <summary>Drives the overlay's progress bar (0–1); zero or less hides the bar (indeterminate busy).</summary>
        public void UpdateTaskProgress(float progress)
        {
            if (_taskOverlayInstance)
            {
                _taskOverlayInstance.UpdateProgress(progress);
            }
        }

        #endregion

        #region Transition Curtain API

        /// <summary>
        /// Catalog key the transition curtain prefab is resolved from. Optional — unlike
        /// <see cref="TaskOverlayKey"/>, a missing entry is a legitimate default: a plain black
        /// fade curtain is generated in code instead (no warning).
        /// </summary>
        public const string TransitionCurtainKey = "TransitionCurtain";

        private TransitionCurtainController _curtain;

        private TransitionCurtainController Curtain => _curtain ??= new TransitionCurtainController(
            ResolveCurtainViewAsync,
            onCoverBegin: PushBackInputSuppression,
            onOpenEnd: PopBackInputSuppression);

        /// <summary>
        /// Closes the transition curtain, runs the work, reopens — the visual mask for scene/state
        /// swaps. Fully covers before the work starts (no swap frame can leak) and ALWAYS reopens,
        /// even when the work throws (the exception still propagates). minCoverSeconds keeps the
        /// curtain closed at least that long (unscaled time) when the work finishes early.
        /// </summary>
        public Awaitable RunUnderCurtainAsync(Func<Awaitable> work, float minCoverSeconds = 0f)
            => Curtain.RunAsync(work, minCoverSeconds);

        /// <summary>
        /// Manual curtain control, ref-counted: returns once fully covered; every successful call
        /// must pair with one <see cref="HideCurtainAsync"/>. Prefer
        /// <see cref="RunUnderCurtainAsync"/>, which guarantees the pairing.
        /// </summary>
        public Awaitable ShowCurtainAsync() => Curtain.ShowAsync();

        /// <summary>Releases one curtain hold; reopens when the last holder releases.</summary>
        public Awaitable HideCurtainAsync() => Curtain.HideAsync();

        private async Awaitable<ITransitionCurtainView> ResolveCurtainViewAsync()
        {
            var parent = taskOverlayContainer ? taskOverlayContainer : (RectTransform)transform;

            if (catalog && catalog.TryGet(TransitionCurtainKey, out var reference) && reference != null && reference.RuntimeKeyIsValid())
            {
                var go = await Addressables.InstantiateAsync(reference, parent).Task;
                var view = go.GetComponent<TransitionCurtainView>();
                if (view)
                {
                    // Below the TaskOverlay: its delayed busy spinner stays visible above the curtain.
                    go.transform.SetSiblingIndex(0);
                    return view;
                }

                Debug.LogError($"[UIManager] '{TransitionCurtainKey}' prefab has no TransitionCurtainView component — using the built-in fade curtain instead.");
                Addressables.ReleaseInstance(go);
            }

            return CreateFallbackCurtain(parent);
        }

        /// <summary>
        /// Builds the zero-setup default curtain: a full-stretch black image with a
        /// <see cref="FadeCurtainView"/>, starting open. Exposed for tests and for games that
        /// want the stock curtain outside the catalog flow.
        /// </summary>
        public static FadeCurtainView CreateFallbackCurtain(Transform parent)
        {
            var go = new GameObject("TransitionCurtain (Default)",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(FadeCurtainView));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(0);

            go.GetComponent<Image>().color = Color.black;

            // EditMode (and pre-Awake) safety: the factory itself produces the open state.
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            return go.GetComponent<FadeCurtainView>();
        }

        #endregion

        /// <summary>
        /// Registers an opened UI on the back-button stack: moves it to the top, syncs sibling
        /// render order, and makes it <see cref="ActiveLayout"/> when it's a layout. Called by
        /// <c>UIBase.OnUIEnter</c> — game code rarely needs this directly.
        /// </summary>
        public void AddToStack(IBackButtonSignalReceiver item)
        {
            _navigationStack.Remove(item);
            _navigationStack.Add(item);

            // Keep render order in sync with stack order.
            if (item is UIBase ui && ui) ui.transform.SetAsLastSibling();

            if (item is LayoutBase layout) ActiveLayout = layout;
        }

        /// <summary>
        /// Removes a closed UI from the back-button stack and re-derives
        /// <see cref="ActiveLayout"/> from the top-most remaining layout. Called by
        /// <c>UIBase.OnUIExit</c> — game code rarely needs this directly.
        /// </summary>
        public void RemoveFromStack(IBackButtonSignalReceiver item)
        {
            if (!_navigationStack.Contains(item)) return;

            _navigationStack.Remove(item);

            // Re-evaluate ActiveLayout/Popup if needed
            if (_navigationStack.Count == 0)
            {
                ActiveLayout = null;
                return;
            }

            // Find valid ActiveLayout from top down
            LayoutBase foundLayout = null;
            for (var i = _navigationStack.Count - 1; i >= 0; i--)
            {
                if (_navigationStack[i] is LayoutBase layout)
                {
                    foundLayout = layout;
                    break;
                }
            }
            ActiveLayout = foundLayout;
        }

        /// <summary>True when this UI is at the top of the back-button stack — the one a back press would route to.</summary>
        public bool IsFocused(UIBase ui)
        {
            return _navigationStack.Count != 0 && ReferenceEquals(_navigationStack[^1], ui);
        }

        private void HandleBackInput()
        {
            if (_navigationStack.Count == 0) return;

            var top = _navigationStack[^1];
            top.OnBackButtonSignalReceived();
        }

        private async Awaitable TryShowNextQueuePopup()
        {
            // If already showing a queue popup, wait.
            if (_activeQueuePopup) return;

            // If nothing in queue, stop.
            if (_popupQueue.Count == 0) return;

            _activeQueuePopup = _popupQueue.Dequeue();

            try
            {
                // Show it directly. The Queue logic is managed here, not recursively via ShowPopup.
                await _activeQueuePopup.SetActivationStateAsync(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIManager] Failed to show queued popup '{_activeQueuePopup.name}': {e.Message}");

                _activeQueuePopup = null; // If showing failed, clear the active flag so queue isn't blocked forever
                _ = TryShowNextQueuePopup(); // Try next one immediately
            }
        }

        private async Awaitable CloseAllPopups()
        {
            // Clear the queue BEFORE closing: ClosePopup on the active queue popup
            // fire-and-forgets TryShowNextQueuePopup, which must find an empty queue
            // or it would start showing a popup mid-close and orphan it.
            _popupQueue.Clear();

            // Create a copy to iterate safely
            var stackCopy = new List<IBackButtonSignalReceiver>(_navigationStack);

            // Reverse order to close top-first
            stackCopy.Reverse();

            foreach (var receiver in stackCopy)
            {
                if (receiver is PopupBase popup)
                {
                    // Use ClosePopup to ensure queue logic is respected if mixed (unlikely but safe)
                    await ClosePopup(popup);
                }
            }

            _navigationStack.RemoveAll(item => item is PopupBase);
            _activeQueuePopup = null;
        }
    }
}
