using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.IAP.UI
{
    /// <summary>
    /// Companion component for <see cref="IapPurchaseButton"/>.
    /// Displays wallet item amounts and manages entitlement-aware visual switching.
    /// When the configured entitlement is granted, switches the button to an alternative
    /// product variant and updates visuals accordingly.
    /// </summary>
    [RequireComponent(typeof(IapPurchaseButton))]
    public class IapButtonContentSetter : MonoBehaviour
    {
        [Serializable]
        private class AmountDisplay
        {
            [field: SerializeField] public string ItemType { get; private set; }
            [field: SerializeField] public TextMeshProUGUI AmountText { get; private set; }
            [field: SerializeField] public string Prefix { get; private set; } = string.Empty;
        }

        [Header("Entitlement Variant")]
        [SerializeField] private string entitlementKey;
        [SerializeField] private string productIdWhenEntitled;

        [Header("Entitlement Visuals")]
        [SerializeField] private GameObject bundleWithEntitlement;
        [SerializeField] private GameObject bundleWithoutEntitlement;
        [SerializeField] private Image itemImage;
        [SerializeField] private Sprite spriteWithEntitlement;
        [SerializeField] private Sprite spriteWithoutEntitlement;

        [Header("Content")]
        [SerializeField] private List<AmountDisplay> amountDisplays;

        private IapPurchaseButton _button;
        private bool _subscribed;

        private void Awake()
        {
            _button = GetComponent<IapPurchaseButton>();

            if (!EnsureService()) return;

            IapService.Instance.Initialized += OnInitialized;

            if (!string.IsNullOrEmpty(entitlementKey))
            {
                IapService.Instance.Entitlements.Subscribe(entitlementKey, OnEntitled);
                _subscribed = true;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (IapService.Instance == null) return;

            IapService.Instance.Initialized -= OnInitialized;

            if (_subscribed)
                IapService.Instance.Entitlements.Unsubscribe(entitlementKey, OnEntitled);
        }

        private void OnInitialized() => Refresh();

        private void OnEntitled()
        {
            // Only swap the button's product when a replacement id is actually configured —
            // an empty productIdWhenEntitled must not clobber the button's current product id.
            // Visuals (bundle/sprite toggling) still update either way via Refresh().
            if (!string.IsNullOrEmpty(productIdWhenEntitled))
                _button.UpdateProduct(productIdWhenEntitled);

            Refresh();
        }

        public void Refresh()
        {
            if (!EnsureService()) return;
            if (!IapService.Instance.Catalog.TryGet(_button.ProductId, out var productDef)) return;

            UpdateAmountDisplays();
            UpdateEntitlementVisuals(productDef);
        }

        private void UpdateAmountDisplays()
        {
            foreach (var display in amountDisplays)
            {
                if (string.IsNullOrEmpty(display.ItemType) || !display.AmountText) continue;
                var amount = IapService.Instance.GetItemAmount(_button.ProductId, display.ItemType);
                display.AmountText.text = $"{display.Prefix}{amount}";
            }
        }

        private void UpdateEntitlementVisuals(IapCatalog.Entry productDef)
        {
            // IsNonConsumable (bool) is used instead of comparing productType directly against
            // UnityEngine.Purchasing.ProductType — TK.IAP.UI is intentionally scoped to
            // ["TK.IAP", "UnityEngine.UI", "Unity.TextMeshPro"] and does not reference Unity.Purchasing.
            var containsEntitlement = productDef.IsNonConsumable
                                       || productDef.items.Exists(item => item.type == IapService.EntitlementItemType);

            if (bundleWithEntitlement) bundleWithEntitlement.SetActive(containsEntitlement);
            if (bundleWithoutEntitlement) bundleWithoutEntitlement.SetActive(!containsEntitlement);

            if (!itemImage) return;

            if (containsEntitlement && spriteWithEntitlement)
                itemImage.sprite = spriteWithEntitlement;
            else if (!containsEntitlement && spriteWithoutEntitlement)
                itemImage.sprite = spriteWithoutEntitlement;
        }

        private static bool _hasLoggedMissingService;

        private static bool EnsureService()
        {
            if (IapService.Instance != null) return true;

            if (!_hasLoggedMissingService)
            {
                _hasLoggedMissingService = true;
                Debug.LogError("[IapButtonContentSetter] IapService not created yet — construct it before UI loads.");
            }

            return false;
        }
    }
}
