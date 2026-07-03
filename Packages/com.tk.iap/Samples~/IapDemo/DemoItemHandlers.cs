using System.Collections.Generic;
using TK.Core.Save;
using TK.IAP;
using UnityEngine;
using UnityEngine.Purchasing;

namespace TK.IAP.Samples.IapDemo
{
    /// <summary>
    /// Runtime demo: builds a 3-entry catalog in code (no ScriptableObject asset needed), registers
    /// logging item handlers, and drives the full init → purchase → confirm flow against whatever
    /// store backend is available. In the Editor this is Unity IAP's built-in fake store, so every
    /// purchase auto-succeeds — see this sample's README for how to try it.
    /// </summary>
    public class DemoItemHandlers : MonoBehaviour
    {
        private IapService _iap;

        private async void Start()
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>
            {
                // Plain consumable: grants 300 "coins" via the handler below.
                new()
                {
                    id = "pack1",
                    storeId = "com.example.pack1",
                    productType = ProductType.Consumable,
                    items = new List<IapCatalog.Item>
                    {
                        new() { type = "coins", amount = 300 }
                    }
                },
                // Permanent entitlement: NonConsumable products auto-grant an entitlement keyed by
                // their catalog id (see IapService.MarkNonConsumableOwned) — no item entry needed.
                new()
                {
                    id = "remove_ads",
                    storeId = "com.example.removeads",
                    productType = ProductType.NonConsumable,
                    items = new List<IapCatalog.Item>()
                },
                // Bundle: a consumable that ALSO grants the "remove_ads" entitlement via an
                // "entitlement" item. Caveat: entitlements inside consumables do NOT survive
                // reinstall — store purchase history only restores NonConsumables on Google/Apple,
                // so a reinstalling player would get their coins re-granted (if re-purchased) but
                // never automatically get "remove_ads" back this way. IapCatalog.OnValidate warns
                // about this in the Editor; prefer a NonConsumable for anything meant to be permanent.
                new()
                {
                    id = "pack10",
                    storeId = "com.example.pack10",
                    productType = ProductType.Consumable,
                    items = new List<IapCatalog.Item>
                    {
                        new() { type = "coins", amount = 300 },
                        new() { type = IapService.EntitlementItemType, value = "remove_ads" }
                    }
                }
            });

            _iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem());

            _iap.RegisterItemHandler("coins", (amount, ctx) =>
                Debug.Log($"[DemoItemHandlers] Granting {amount} coins (product '{ctx.ProductId}', restore: {ctx.IsRestore})."));
            _iap.RegisterItemHandler("life", (amount, ctx) =>
                Debug.Log($"[DemoItemHandlers] Granting {amount} life (product '{ctx.ProductId}', restore: {ctx.IsRestore})."));

            _iap.ProductPurchased += id => Debug.Log($"[DemoItemHandlers] ProductPurchased: {id}");
            _iap.PurchaseFailed += (id, reason) => Debug.Log($"[DemoItemHandlers] PurchaseFailed: {id} ({reason})");
            _iap.RestoreCompleted += success => Debug.Log($"[DemoItemHandlers] RestoreCompleted: {success}");

            try
            {
                await _iap.InitializeAsync();
                Debug.Log($"[DemoItemHandlers] Init finished with state: {_iap.State}");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
