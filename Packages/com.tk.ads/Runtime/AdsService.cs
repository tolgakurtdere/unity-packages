using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Ads
{
    /// <summary>
    /// Main ads facade. Owns policy (pacing, intent-based banner, reward latching, retry)
    /// behind IAdsGateway; MAX specifics live in MaxAdsGateway. Main-thread only.
    /// One service per gateway instance; to retry a Failed init construct both fresh.
    /// Rewarded is never gated by policy delegates (remove-ads keeps rewarded).
    /// </summary>
    public sealed class AdsService
    {
        public static AdsService Instance { get; private set; }

        public const string InterstitialIntervalKey = AdsPacingKeys.InterstitialInterval;
        public const string CooldownAfterRewardedKey = AdsPacingKeys.CooldownAfterRewarded;

        public AdsInitState State { get; private set; } = AdsInitState.NotInitialized;
        public event Action Initialized;
        public event Action InitFailed;

        public bool IsBannerVisible => _bannerIntent.IsVisible;
        public event Action BannerClicked;

        public bool IsInterstitialReady => State == AdsInitState.Initialized && _gateway.IsInterstitialReady;
        public event Action InterstitialClosed;

        public bool IsRewardedReady => State == AdsInitState.Initialized && _gateway.IsRewardedReady;
        public event Action RewardedReadyChanged;

        private readonly AdsSettings _settings;
        private readonly AdsOptions _options;
        private readonly IAdsGateway _gateway;
        private readonly Func<float> _clock;
        private readonly InterstitialPacer _pacer;
        private readonly BannerIntent _bannerIntent = new();
        private readonly LoadRetryPolicy _bannerRetry = new();
        private readonly LoadRetryPolicy _interstitialRetry = new();
        private readonly LoadRetryPolicy _rewardedRetry = new();

        private Task _initTask;
        private bool _fullscreenAdInProgress;
        private bool _rewardLatched;
        private TaskCompletionSource<bool> _interstitialTcs;
        private TaskCompletionSource<RewardedResult> _rewardedTcs;
        private bool _destroyed;
        private bool _bannerDestroyed;

        public AdsService(AdsSettings settings, AdsOptions options = null)
        {
            _settings = settings ? settings : throw new ArgumentNullException(nameof(settings));
            _options = options ?? new AdsOptions();
            _gateway = _options.Gateway ?? new MaxAdsGateway();
            _clock = _options.Clock ?? (() => Time.realtimeSinceStartup);
            _pacer = new InterstitialPacer(_clock, _options.PacingResolver,
                _settings.interstitialMinIntervalSeconds, _settings.cooldownAfterRewardedSeconds);

            Instance = this;
        }

        public async Task InitializeAsync()
        {
            if (_initTask != null)
            {
                await _initTask;
                return;
            }

            _initTask = InitializeInternalAsync();
            await _initTask;
        }

        private async Task InitializeInternalAsync()
        {
            State = AdsInitState.Initializing;

            _gateway.Initialized += OnGatewayInitialized;
            _gateway.InitializeFailed += OnGatewayInitializeFailed;
            _gateway.BannerLoaded += OnBannerLoaded;
            _gateway.BannerLoadFailed += OnBannerLoadFailed;
            _gateway.BannerClicked += () => BannerClicked?.Invoke();
            _gateway.InterstitialLoaded += OnInterstitialLoaded;
            _gateway.InterstitialLoadFailed += OnInterstitialLoadFailed;
            _gateway.InterstitialDisplayFailed += OnInterstitialDisplayFailed;
            _gateway.InterstitialHidden += OnInterstitialHidden;
            _gateway.RewardedLoaded += OnRewardedLoaded;
            _gateway.RewardedLoadFailed += OnRewardedLoadFailed;
            _gateway.RewardedDisplayFailed += OnRewardedDisplayFailed;
            _gateway.RewardedHidden += OnRewardedHidden;
            _gateway.RewardReceived += OnRewardReceived;
            _gateway.RevenuePaid += OnRevenuePaid;

            try
            {
                await _gateway.InitializeAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FailInit("gateway init threw");
            }
        }

        private void OnGatewayInitialized()
        {
            if (State != AdsInitState.Initializing) return;

            State = AdsInitState.Initialized;
            Initialized?.Invoke();

            if (Allowed(_options.ShouldLoadBanner) && !string.IsNullOrEmpty(_settings.BannerAdUnitId))
                _gateway.CreateBanner(_settings.BannerAdUnitId, _settings.bannerPosition, _settings.bannerBackgroundColor);

            if (Allowed(_options.ShouldLoadInterstitial) && !string.IsNullOrEmpty(_settings.InterstitialAdUnitId))
                _gateway.LoadInterstitial(_settings.InterstitialAdUnitId);

            if (!string.IsNullOrEmpty(_settings.RewardedAdUnitId))
                _gateway.LoadRewarded(_settings.RewardedAdUnitId); // never policy-gated
        }

        private void OnGatewayInitializeFailed(string message)
        {
            if (State != AdsInitState.Initializing) return;
            FailInit(message);
        }

        private void FailInit(string message)
        {
            Debug.LogError($"[AdsService] Initialization failed: {message}");
            State = AdsInitState.Failed;
            InitFailed?.Invoke();
        }

        // ── Banner ──

        public void ShowBanner()
        {
            if (State != AdsInitState.Initialized) return;
            if (!Allowed(_options.ShouldShowBanner)) return;

            if (_bannerIntent.RequestShow())
                _gateway.ShowBanner();
        }

        public void HideBanner()
        {
            _bannerIntent.RequestHide();
            if (State == AdsInitState.Initialized) _gateway.HideBanner();
        }

        public void DestroyBanner()
        {
            _bannerIntent.Reset();
            _bannerRetry.OnSucceeded(); // counter bookkeeping; actual retry cancelation is the _bannerDestroyed check below
            _bannerDestroyed = true;    // pending OnBannerLoadFailed retry loops check this and bail
            if (State == AdsInitState.Initialized) _gateway.DestroyBanner();
        }

        private void OnBannerLoaded()
        {
            _bannerRetry.OnSucceeded();
            if (!Allowed(_options.ShouldShowBanner)) return;

            if (_bannerIntent.OnLoaded())
                _gateway.ShowBanner();
        }

        private async void OnBannerLoadFailed(string message)
        {
            if (_bannerDestroyed) return;
            var delay = _bannerRetry.OnFailed();
            Debug.LogWarning($"[AdsService] Banner load failed ({message}); retrying in {delay}s");
            await DelayScaled(delay);
            if (_bannerDestroyed || _destroyed || State != AdsInitState.Initialized) return;
            _gateway.CreateBanner(_settings.BannerAdUnitId, _settings.bannerPosition, _settings.bannerBackgroundColor);
        }

        // ── Interstitial ──

        public Task<bool> ShowInterstitialAsync(string placement = null)
        {
            if (State != AdsInitState.Initialized) return Task.FromResult(false);
            if (_fullscreenAdInProgress) return Task.FromResult(false);
            if (!Allowed(_options.ShouldLoadInterstitial) || !Allowed(_options.ShouldShowInterstitial)) return Task.FromResult(false);
            if (!_pacer.CanShow()) return Task.FromResult(false);
            if (!_gateway.IsInterstitialReady) return Task.FromResult(false);

            _fullscreenAdInProgress = true;
            _interstitialTcs = new TaskCompletionSource<bool>();
            SetMuted(true);
            _gateway.ShowInterstitial(placement);
            return _interstitialTcs.Task;
        }

        private void OnInterstitialHidden()
        {
            _pacer.NotifyInterstitialClosed();
            FinishFullscreen();
            InterstitialClosed?.Invoke();
            ReloadInterstitial();
            _interstitialTcs?.TrySetResult(true);
            _interstitialTcs = null;
        }

        private void OnInterstitialDisplayFailed(string message)
        {
            Debug.LogWarning($"[AdsService] Interstitial display failed: {message}");
            FinishFullscreen();
            ReloadInterstitial();
            _interstitialTcs?.TrySetResult(false);
            _interstitialTcs = null;
        }

        private void OnInterstitialLoaded() => _interstitialRetry.OnSucceeded();

        private async void OnInterstitialLoadFailed(string message)
        {
            var delay = _interstitialRetry.OnFailed();
            await DelayScaled(delay);
            if (_destroyed || State != AdsInitState.Initialized) return;
            if (Allowed(_options.ShouldLoadInterstitial)) ReloadInterstitial();
        }

        private void ReloadInterstitial()
        {
            if (!string.IsNullOrEmpty(_settings.InterstitialAdUnitId))
                _gateway.LoadInterstitial(_settings.InterstitialAdUnitId);
        }

        // ── Rewarded (never policy-gated) ──

        public Task<RewardedResult> ShowRewardedAsync(string placement = null)
        {
            if (State != AdsInitState.Initialized) return Task.FromResult(RewardedResult.NotInitialized);
            if (_fullscreenAdInProgress) return Task.FromResult(RewardedResult.NotReady);
            if (!_gateway.IsRewardedReady) return Task.FromResult(RewardedResult.NotReady);

            _fullscreenAdInProgress = true;
            _rewardLatched = false;
            _rewardedTcs = new TaskCompletionSource<RewardedResult>();
            SetMuted(true);
            _gateway.ShowRewarded(placement);
            return _rewardedTcs.Task;
        }

        private void OnRewardReceived() => _rewardLatched = true;

        private void OnRewardedHidden()
        {
            var rewarded = _rewardLatched;
            _rewardLatched = false;
            if (rewarded) _pacer.NotifyRewardedCompleted();

            FinishFullscreen();
            ReloadRewarded();
            RewardedReadyChanged?.Invoke();
            _rewardedTcs?.TrySetResult(rewarded ? RewardedResult.Rewarded : RewardedResult.Cancelled);
            _rewardedTcs = null;
        }

        private void OnRewardedDisplayFailed(string message)
        {
            Debug.LogWarning($"[AdsService] Rewarded display failed: {message}");
            _rewardLatched = false;
            FinishFullscreen();
            ReloadRewarded();
            _rewardedTcs?.TrySetResult(RewardedResult.FailedToShow);
            _rewardedTcs = null;
        }

        private void OnRewardedLoaded()
        {
            _rewardedRetry.OnSucceeded();
            RewardedReadyChanged?.Invoke();
        }

        private async void OnRewardedLoadFailed(string message)
        {
            var delay = _rewardedRetry.OnFailed();
            await DelayScaled(delay);
            if (_destroyed || State != AdsInitState.Initialized) return;
            ReloadRewarded();
        }

        private void ReloadRewarded()
        {
            if (!string.IsNullOrEmpty(_settings.RewardedAdUnitId))
                _gateway.LoadRewarded(_settings.RewardedAdUnitId);
        }

        // ── Cross-cutting ──

        public Task<bool> ShowConsentDialogAsync() => _gateway.ShowConsentDialogAsync();

        /// <summary>Detach from gateway timing loops (tests / teardown). Not required in normal app lifetime.</summary>
        public void Teardown() => _destroyed = true;

        private void OnRevenuePaid(AdRevenueInfo info)
        {
            if (_options.RevenueReporter == null) return;
            try { _options.RevenueReporter.OnAdRevenue(info); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void FinishFullscreen()
        {
            _fullscreenAdInProgress = false;
            SetMuted(false);
        }

        private void SetMuted(bool muted)
        {
            try { _options.AudioMuteSetter?.Invoke(muted); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static bool Allowed(Func<bool> policy) => policy == null || policy();

        private async Task DelayScaled(float seconds)
        {
            var scaled = seconds * Math.Max(0f, _options.RetryDelayScale);
            if (scaled <= 0f) { await Task.Yield(); return; }
            await Task.Delay(TimeSpan.FromSeconds(scaled));
        }
    }
}
