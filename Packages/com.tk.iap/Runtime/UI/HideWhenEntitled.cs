using UnityEngine;

namespace TK.IAP.UI
{
    /// <summary>Hides this GameObject once the configured entitlement is granted (e.g. "remove_ads").</summary>
    public class HideWhenEntitled : MonoBehaviour
    {
        [SerializeField] private string entitlementKey;

        private void Awake()
        {
            if (IapService.Instance == null)
            {
                Debug.LogError("[HideWhenEntitled] IapService not created yet — construct it before UI loads.");
                return;
            }

            IapService.Instance.Entitlements.Subscribe(entitlementKey, Hide);
        }

        private void OnDestroy()
        {
            IapService.Instance?.Entitlements.Unsubscribe(entitlementKey, Hide);
        }

        private void Hide() => gameObject.SetActive(false);

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(entitlementKey))
                Debug.LogWarning($"[HideWhenEntitled] '{name}': entitlementKey is empty — component will never hide.", this);
        }
    }
}
