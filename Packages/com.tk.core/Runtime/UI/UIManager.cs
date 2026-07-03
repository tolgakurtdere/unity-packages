using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace TK.Core.UI
{
    public class UIManager : MonoBehaviour
    {
        private static UIManager s_instance;
        public static UIManager Instance
        {
            get
            {
                if (!s_instance) s_instance = FindAnyObjectByType<UIManager>();
                return s_instance;
            }
        }

        public LayoutBase ActiveLayout { get; private set; }

        [Header("Containers")]
        [SerializeField] private RectTransform layoutContainer;
        [SerializeField] private RectTransform popupContainer;
        [SerializeField] private RectTransform taskOverlayContainer;

        [Header("Catalog")]
        [SerializeField] private UICatalog catalog;

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
            if (backPressed)
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

        public const string TaskOverlayKey = "TaskOverlay";

        private TaskOverlayPopup _taskOverlayInstance;

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

        public void HideTask(Guid taskId)
        {
            if (_taskOverlayInstance)
            {
                _taskOverlayInstance.Hide(taskId);
            }
        }

        public void UpdateTaskProgress(float progress)
        {
            if (_taskOverlayInstance)
            {
                _taskOverlayInstance.UpdateProgress(progress);
            }
        }

        #endregion

        public void AddToStack(IBackButtonSignalReceiver item)
        {
            _navigationStack.Remove(item);
            _navigationStack.Add(item);

            // Keep render order in sync with stack order.
            if (item is UIBase ui && ui) ui.transform.SetAsLastSibling();

            if (item is LayoutBase layout) ActiveLayout = layout;
        }

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
            for (int i = _navigationStack.Count - 1; i >= 0; i--)
            {
                if (_navigationStack[i] is LayoutBase layout)
                {
                    foundLayout = layout;
                    break;
                }
            }
            ActiveLayout = foundLayout;
        }

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
            _popupQueue.Clear();
            _activeQueuePopup = null;
        }
    }
}
