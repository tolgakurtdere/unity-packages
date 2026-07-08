using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TK.Audio.Tests
{
    [TestFixture]
    public class AudioCatalogTests
    {
        private AudioCatalog _catalog;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            _clip = AudioClip.Create("clip", 44100, 1, 44100, false);

            // NOTE: growing a serialized list creates ZEROED elements (field initializers do not
            // run through the serializer — same as the inspector's + button on an empty list),
            // so fixtures must set every value they assert.
            var so = new SerializedObject(_catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            var click = entries.GetArrayElementAtIndex(0);
            click.FindPropertyRelative("key").stringValue = "click";
            click.FindPropertyRelative("channel").enumValueIndex = (int)AudioChannel.Sfx;
            click.FindPropertyRelative("volumeScale").floatValue = 1f;
            click.FindPropertyRelative("minRetriggerInterval").floatValue = 0.05f;
            var clips = click.FindPropertyRelative("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = _clip;

            var music = entries.GetArrayElementAtIndex(1);
            music.FindPropertyRelative("key").stringValue = "music_menu";
            music.FindPropertyRelative("channel").enumValueIndex = (int)AudioChannel.Music;
            music.FindPropertyRelative("clips").arraySize = 0;

            var playlists = so.FindProperty("playlists");
            playlists.arraySize = 1;
            var playlist = playlists.GetArrayElementAtIndex(0);
            playlist.FindPropertyRelative("key").stringValue = "menu";
            playlist.FindPropertyRelative("loop").boolValue = true;
            var keys = playlist.FindPropertyRelative("entryKeys");
            keys.arraySize = 1;
            keys.GetArrayElementAtIndex(0).stringValue = "music_menu";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void TryGetEntry_ResolvesTheConfiguredValues()
        {
            Assert.IsTrue(_catalog.TryGetEntry("click", out var entry));
            Assert.AreEqual(AudioChannel.Sfx, entry.Channel);
            Assert.IsTrue(entry.HasDirectClips);
            Assert.AreEqual(1f, entry.VolumeScale);
            Assert.AreEqual(0.05f, entry.MinRetriggerInterval, 1e-4f);
        }

        [Test]
        public void TryGetEntry_UnknownOrEmptyKeys_ReturnFalse()
        {
            Assert.IsFalse(_catalog.TryGetEntry("nope", out _));
            Assert.IsFalse(_catalog.TryGetEntry(null, out _));
            Assert.IsFalse(_catalog.TryGetEntry("", out _));
        }

        [Test]
        public void TryGetPlaylist_ResolvesByKey()
        {
            Assert.IsTrue(_catalog.TryGetPlaylist("menu", out var playlist));
            Assert.IsTrue(playlist.Loop, "Loop must round-trip through serialization.");
            Assert.AreEqual(1, playlist.EntryKeys.Count);
            Assert.IsFalse(_catalog.TryGetPlaylist("nope", out _));
        }

        [Test]
        public void HasDirectClips_IsFalseForAddressableOnlyEntries()
        {
            Assert.IsTrue(_catalog.TryGetEntry("music_menu", out var entry));
            Assert.IsFalse(entry.HasDirectClips);
        }

        [Test]
        public void OnValidate_WarnsWhenAnEntryHasBothClipSources()
        {
            var so = new SerializedObject(_catalog);
            var click = so.FindProperty("entries").GetArrayElementAtIndex(0); // already has a direct clip
            click.FindPropertyRelative("addressableClip").FindPropertyRelative("m_AssetGUID").stringValue =
                "0123456789abcdef0123456789abcdef"; // any well-formed GUID makes RuntimeKeyIsValid true

            // ApplyModifiedProperties triggers OnValidate on the target.
            LogAssert.Expect(LogType.Warning, new Regex("entry 'click' has BOTH direct clips and an"));
            so.ApplyModifiedProperties();
        }
    }
}
