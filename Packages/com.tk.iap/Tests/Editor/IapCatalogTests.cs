using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.TestTools;

namespace TK.IAP.Tests
{
    [TestFixture]
    public sealed class IapCatalogTests
    {
        [Test]
        public void TryGet_FindsById()
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>
            {
                new() { id = "pack1", storeId = "coin_pack_1", productType = ProductType.Consumable }
            });

            var found = catalog.TryGet("pack1", out var entry);

            Assert.IsTrue(found);
            Assert.IsNotNull(entry);
            Assert.AreEqual("pack1", entry.id);
            Assert.AreEqual("coin_pack_1", entry.storeId);
        }

        [Test]
        public void Get_MissingId_LogsErrorAndReturnsNull()
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>());

            LogAssert.Expect(LogType.Error, new Regex("No entry for id 'missing'"));
            var entry = catalog.Get("missing");

            Assert.IsNull(entry);
        }

        [Test]
        public void DuplicateId_LogsError_FirstEntryWins()
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>
            {
                new() { id = "pack1", storeId = "coin_pack_1", productType = ProductType.Consumable },
                new() { id = "pack1", storeId = "coin_pack_1_dupe", productType = ProductType.Consumable }
            });

            LogAssert.Expect(LogType.Error, new Regex("Duplicate id 'pack1'"));
            var found = catalog.TryGet("pack1", out var entry);

            Assert.IsTrue(found);
            Assert.AreEqual("coin_pack_1", entry.storeId);
        }

        [Test]
        public void BuildDefinitions_FiltersByPlatform()
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>
            {
                new() { id = "both", storeId = "store_both", productType = ProductType.Consumable, platforms = StorePlatform.BothStores },
                new() { id = "apple", storeId = "store_apple", productType = ProductType.Consumable, platforms = StorePlatform.AppleAppStore },
                new() { id = "google", storeId = "store_google", productType = ProductType.Consumable, platforms = StorePlatform.GooglePlayStore }
            });

            var definitions = catalog.BuildDefinitions(true);

            Assert.AreEqual(2, definitions.Count);
            Assert.AreEqual("store_both", definitions[0].StoreId);
            Assert.AreEqual("store_apple", definitions[1].StoreId);
        }

        [Test]
        public void SetEntries_InvalidatesLookup()
        {
            var catalog = ScriptableObject.CreateInstance<IapCatalog>();
            catalog.SetEntries(new List<IapCatalog.Entry>
            {
                new() { id = "pack1", storeId = "coin_pack_1", productType = ProductType.Consumable }
            });

            // Force the lookup to build and cache.
            Assert.IsTrue(catalog.TryGet("pack1", out _));

            catalog.SetEntries(new List<IapCatalog.Entry>
            {
                new() { id = "pack2", storeId = "coin_pack_2", productType = ProductType.Consumable }
            });

            Assert.IsFalse(catalog.TryGet("pack1", out _));
            Assert.IsTrue(catalog.TryGet("pack2", out var entry));
            Assert.AreEqual("coin_pack_2", entry.storeId);
        }
    }
}
