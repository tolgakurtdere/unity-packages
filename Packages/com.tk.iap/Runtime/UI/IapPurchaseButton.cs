using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.IAP.UI
{
    /// <summary>
    /// Drop-in purchase button: shows the localized price for a catalog product, an optional
    /// old-price + discount display, and starts the purchase on click.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class IapPurchaseButton : MonoBehaviour
    {
        [SerializeField] private string productId;
        [SerializeField] private TextMeshProUGUI priceText;

        [SerializeField] private bool hasOldPrice;
        [SerializeField] private string[] oldProductIds;
        [SerializeField] private TextMeshProUGUI oldPriceText;
        [SerializeField] private TextMeshProUGUI discountText;
        [SerializeField] private GameObject discountTextHolder;

        public string ProductId => productId;
        public string[] OldProductIds => oldProductIds;

        public string LocalizedPriceString
        {
            get => priceText ? priceText.text : null;

            set
            {
                if (priceText)
                    priceText.text = value;
            }
        }

        public int DiscountPercentage { get; private set; }

        private Button _iapButton;
        private bool _isInitialized;

        private const string NotInitializedPriceString = "---";

        private void Awake()
        {
            if (discountTextHolder) discountTextHolder.SetActive(false); // Default state
            Init();
        }

        private void Init()
        {
            if (_isInitialized)
                return;

            _iapButton = GetComponent<Button>();
            _iapButton.onClick.AddListener(OnIapButtonTapped);
            LocalizedPriceString = NotInitializedPriceString;

            if (!EnsureService()) { _isInitialized = true; return; }

            if (IapService.Instance.State == IapInitState.Initialized)
            {
                OnIapSystemInitialized();
            }
            else
            {
                IapService.Instance.Initialized += OnIapSystemInitialized;
                IapService.Instance.InitFailed += OnIapSystemInitializationFailed;
            }

            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (_iapButton) _iapButton.onClick.RemoveListener(OnIapButtonTapped);

            if (IapService.Instance == null) return;
            IapService.Instance.Initialized -= OnIapSystemInitialized;
            IapService.Instance.InitFailed -= OnIapSystemInitializationFailed;
        }

        /// <summary>
        /// Switches the product this button represents at runtime, then refreshes all price/discount UI.
        /// Used by the offer system to dynamically assign different products to the buy button.
        /// </summary>
        public void UpdateProduct(string newProductId, params string[] newOldProductIds)
        {
            productId = newProductId;

            if (newOldProductIds is { Length: > 0 })
                oldProductIds = newOldProductIds;

            if (EnsureService() && IapService.Instance.State == IapInitState.Initialized)
                RefreshPriceDisplay();
        }

        private void OnIapSystemInitialized() => RefreshPriceDisplay();

        private void RefreshPriceDisplay()
        {
            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogError("[IapPurchaseButton] RefreshPriceDisplay() --> productId is empty!");
                return;
            }

            if (!IapService.Instance.TryGetPrice(productId, out var price))
            {
                Debug.LogError($"[IapPurchaseButton] RefreshPriceDisplay() --> Store product not found! (ProductId: {productId})");
                return;
            }

            LocalizedPriceString = price.Display;

            if (hasOldPrice && oldProductIds is { Length: > 0 })
            {
                var totalOldPrice = 0m;
                var oldIsoCurrencyCode = price.IsoCurrencyCode;

                foreach (var oldId in oldProductIds)
                {
                    if (string.IsNullOrEmpty(oldId)) continue;
                    if (!IapService.Instance.TryGetPrice(oldId, out var oldPrice)) continue;
                    totalOldPrice += oldPrice.Amount;
                    oldIsoCurrencyCode = oldPrice.IsoCurrencyCode;
                }

                if (totalOldPrice <= 0) return;

                if (oldPriceText) oldPriceText.text = $"{oldIsoCurrencyCode} {totalOldPrice}";

                DiscountPercentage = (int)(100 - price.Amount / totalOldPrice * 100); // Calculate discount percentage anyway, it may be used from outside
                if (discountText)
                {
                    discountText.text = (DiscountPercentage / 100f).ToString("0%", System.Globalization.CultureInfo.InvariantCulture);
                    if (discountTextHolder) discountTextHolder.SetActive(DiscountPercentage > 0);
                }
            }
        }

        private void OnIapSystemInitializationFailed()
        {
            LocalizedPriceString = NotInitializedPriceString;
            if (oldPriceText) oldPriceText.text = NotInitializedPriceString;
            if (discountText) discountText.text = string.Empty;
            if (discountTextHolder) discountTextHolder.SetActive(false);
            DiscountPercentage = 0;
        }

        // Button Event
        private void OnIapButtonTapped()
        {
            if (!EnsureService()) return;

            if (IapService.Instance.State != IapInitState.Initialized)
            {
                Debug.LogError($"[IapPurchaseButton] OnIapButtonTapped() --> In App Purchasing is not initialized! ({productId})");
                return;
            }

            IapService.Instance.Purchase(productId);
        }

        private static bool _hasLoggedMissingService;

        private static bool EnsureService()
        {
            if (IapService.Instance != null) return true;

            if (!_hasLoggedMissingService)
            {
                _hasLoggedMissingService = true;
                Debug.LogError("[IapPurchaseButton] IapService not created yet — construct it before UI loads.");
            }

            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(productId))
                Debug.LogWarning("[IapPurchaseButton] productId is empty — this button will never resolve a price.", this);
        }
    }
}
