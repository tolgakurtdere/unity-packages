using System;
using System.Collections;
using System.Collections.Generic;
using TK.Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    public class TaskOverlayPopup : PopupBase
    {
        [Header("Overlay Settings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform progressBarRectTransform;
        [SerializeField] private Image progressBarImage;

        private SharedBool<CallerInfo> _sharedBool;
        private readonly Dictionary<Guid, CallerInfo> _callerInfoDict = new();
        private Coroutine _handleBusyAnimationCoroutine;

        private const float BUSY_ANIMATION_SHOW_DELAY = 1f;

        protected override void Awake()
        {
            base.Awake();

            _sharedBool = new SharedBool<CallerInfo>(
                defaultValue: false,
                stateToProtect: true,
                setAction: (isEnabled, callerInfo) =>
                {
                    if (isEnabled) ShowInternal(callerInfo);
                    else HideInternal();
                });
        }

        public void Show(out Guid callerId, bool isTransparent = false, bool forceShowBackgroundIfItIsNotTransparent = false)
        {
            callerId = Guid.NewGuid();
            var callerInfo = new CallerInfo(callerId, isTransparent, forceShowBackgroundIfItIsNotTransparent);

            _callerInfoDict.Add(callerId, callerInfo);
            _sharedBool.SetState(true, callerInfo);
        }

        public void Hide(Guid callerId)
        {
            if (!_callerInfoDict.TryGetValue(callerId, out var callerInfo)) return;

            _sharedBool.SetState(false, callerInfo);
            _callerInfoDict.Remove(callerId);
        }

        public void UpdateProgress(float progress)
        {
            if (progressBarImage && progressBarRectTransform)
            {
                if (progress > 0f)
                {
                    var progressBarAnchorMax = progressBarRectTransform.anchorMax;
                    progressBarAnchorMax.x = 0.1f + progress * 0.9f;
                    progressBarRectTransform.anchorMax = progressBarAnchorMax;
                    progressBarImage.enabled = true;
                }
                else
                {
                    progressBarImage.enabled = false;
                }
            }
        }

        private async void ShowInternal(CallerInfo callerInfo)
        {
            await SetActivationStateAsync(true);

            HandleBusyAnimationState(callerInfo.IsTransparent, callerInfo.ForceShowTheBackgroundIfItIsNotTransparent);
            UpdateProgress(0);
        }

        private void HideInternal()
        {
            StopHandleBusyAnimationCoroutine();
            if (canvasGroup) canvasGroup.alpha = 0f;
            _ = SetActivationStateAsync(false);
        }

        private void HandleBusyAnimationState(bool isTransparent, bool forceShowTheBackgroundIfItIsNotTransparent)
        {
            if (!canvasGroup) return;

            if (!isTransparent)
            {
                if (!forceShowTheBackgroundIfItIsNotTransparent)
                {
                    _handleBusyAnimationCoroutine ??= StartCoroutine(HandleBusyAnimationCoroutine());
                }
                else
                {
                    canvasGroup.alpha = 1f;
                }
            }
            else
            {
                StopHandleBusyAnimationCoroutine();
                canvasGroup.alpha = 0f;
            }
        }

        private void StopHandleBusyAnimationCoroutine()
        {
            if (_handleBusyAnimationCoroutine == null) return;

            StopCoroutine(_handleBusyAnimationCoroutine);
            _handleBusyAnimationCoroutine = null;
        }

        private IEnumerator HandleBusyAnimationCoroutine()
        {
            canvasGroup.alpha = 0f;
            yield return new WaitForSecondsRealtime(BUSY_ANIMATION_SHOW_DELAY);
            canvasGroup.alpha = 1f;
            _handleBusyAnimationCoroutine = null;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Awaitable PlayDisplayAnimationAsync()
        {
            // Instant display
        }

        protected override async Awaitable PlayHideAnimationAsync()
        {
            // Instant hide
        }
#pragma warning restore CS1998

        public override void OnBackButtonSignalReceived()
        {
            // Cannot be closed via Back button
        }

        public struct CallerInfo
        {
            public Guid CallerId { get; }
            public bool IsTransparent { get; }
            public bool ForceShowTheBackgroundIfItIsNotTransparent { get; }

            public CallerInfo(Guid callerId, bool isTransparent, bool forceShowTheBackgroundIfItIsNotTransparent)
            {
                CallerId = callerId;
                IsTransparent = isTransparent;
                ForceShowTheBackgroundIfItIsNotTransparent = forceShowTheBackgroundIfItIsNotTransparent;
            }

            public override int GetHashCode() => CallerId.GetHashCode();
            public override bool Equals(object obj) => obj is CallerInfo other && CallerId == other.CallerId;
        }
    }
}
