using System.Collections.Generic;
using NUnit.Framework;
using TK.Audio.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TK.Audio.Tests
{
    [TestFixture]
    public class AudioCatalogPopulatorTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj) Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 44100, 1, 44100, false);
            clip.name = name;
            _created.Add(clip);
            return clip;
        }

        private AudioCatalog Catalog()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            _created.Add(catalog);
            return catalog;
        }

        [Test]
        public void NewKeysFor_DerivesNames_SkipsExisting_AndDedupesWithinSelection()
        {
            var clips = new[] { Clip("click"), Clip("coin"), Clip("click"), null, Clip("whoosh") };

            var keys = AudioCatalogPopulator.NewKeysFor(clips, new[] { "coin" });

            CollectionAssert.AreEqual(new[] { "click", "whoosh" }, keys,
                "'coin' already exists; the second 'click' is a within-selection dup; null is skipped.");
        }

        [Test]
        public void NewKeysFor_NullClips_ReturnsEmpty()
        {
            Assert.IsEmpty(AudioCatalogPopulator.NewKeysFor(null, null));
        }

        [Test]
        public void AppendEntries_AddsEntriesWithSaneDefaults_NotTheZeroedTrap()
        {
            var catalog = Catalog();

            var added = AudioCatalogPopulator.AppendEntries(catalog, new[] { Clip("click") }, AudioChannel.Sfx);

            Assert.AreEqual(1, added);
            Assert.IsTrue(catalog.TryGetEntry("click", out var entry));
            Assert.AreEqual(AudioChannel.Sfx, entry.Channel);
            Assert.AreEqual(1f, entry.VolumeScale, "Auto-added entries must not be silent (volumeScale 0).");
            Assert.AreEqual(0.05f, entry.MinRetriggerInterval, 1e-4f);
            Assert.IsTrue(entry.HasDirectClips, "The clip must be assigned.");
        }

        [Test]
        public void AppendEntries_SkipsKeysAlreadyInTheCatalog()
        {
            var catalog = Catalog();
            AudioCatalogPopulator.AppendEntries(catalog, new[] { Clip("click") }, AudioChannel.Sfx);

            var added = AudioCatalogPopulator.AppendEntries(catalog, new[] { Clip("click"), Clip("coin") },
                AudioChannel.Sfx);

            Assert.AreEqual(1, added, "'click' already exists — only 'coin' is added.");
            Assert.IsTrue(catalog.TryGetEntry("coin", out _));
        }
    }
}
