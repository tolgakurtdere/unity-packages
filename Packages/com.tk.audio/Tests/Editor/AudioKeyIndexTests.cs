using System.Collections.Generic;
using NUnit.Framework;
using TK.Audio.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TK.Audio.Tests
{
    [TestFixture]
    public class AudioKeyIndexTests
    {
        private readonly List<AudioCatalog> _catalogs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var catalog in _catalogs)
            {
                if (catalog) Object.DestroyImmediate(catalog);
            }

            _catalogs.Clear();
        }

        private AudioCatalog BuildCatalog(string[] entryKeys, string[] playlistKeys = null)
        {
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            _catalogs.Add(catalog);

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = entryKeys.Length;
            for (var i = 0; i < entryKeys.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = entryKeys[i];
                entry.FindPropertyRelative("channel").enumValueIndex = (int)AudioChannel.Sfx;
            }

            if (playlistKeys != null)
            {
                var playlists = so.FindProperty("playlists");
                playlists.arraySize = playlistKeys.Length;
                for (var i = 0; i < playlistKeys.Length; i++)
                    playlists.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue = playlistKeys[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        [Test]
        public void CollectKeys_UnionsDedupesAndSortsAcrossCatalogs()
        {
            var a = BuildCatalog(new[] { "click", "whoosh" });
            var b = BuildCatalog(new[] { "whoosh", "coin" }); // 'whoosh' duplicated across catalogs

            var keys = AudioCatalogKeyIndex.CollectKeys(new[] { a, b });

            CollectionAssert.AreEqual(new[] { "click", "coin", "whoosh" }, keys, "Deduped and ordinal-sorted.");
        }

        [Test]
        public void CollectKeys_SkipsEmptyKeys()
        {
            var a = BuildCatalog(new[] { "click", "", "coin" });

            var keys = AudioCatalogKeyIndex.CollectKeys(new[] { a });

            CollectionAssert.AreEqual(new[] { "click", "coin" }, keys);
        }

        [Test]
        public void CollectPlaylistKeys_UnionsAcrossCatalogs()
        {
            var a = BuildCatalog(new[] { "m1" }, new[] { "menu" });
            var b = BuildCatalog(new[] { "m2" }, new[] { "gameplay", "menu" });

            var keys = AudioCatalogKeyIndex.CollectPlaylistKeys(new[] { a, b });

            CollectionAssert.AreEqual(new[] { "gameplay", "menu" }, keys);
        }

        [Test]
        public void Collect_EmptyOrNullInput_ReturnsEmpty()
        {
            Assert.IsEmpty(AudioCatalogKeyIndex.CollectKeys(new AudioCatalog[0]));
            Assert.IsEmpty(AudioCatalogKeyIndex.CollectKeys(null));
            Assert.IsEmpty(AudioCatalogKeyIndex.CollectPlaylistKeys(null));
        }
    }
}
