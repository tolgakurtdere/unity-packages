using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TK.Audio.Tests
{
    [TestFixture]
    public class AudioServiceTests
    {
        private AudioService _service;
        private AudioCatalog _catalog;
        private readonly List<AudioClip> _clips = new();

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            if (_catalog) Object.DestroyImmediate(_catalog);
            _catalog = null;
            foreach (var clip in _clips)
            {
                if (clip) Object.DestroyImmediate(clip);
            }

            _clips.Clear();
        }

        private AudioClip CreateClip(string name)
        {
            var clip = AudioClip.Create(name, 44100, 1, 44100, false); // 1 s — longer than any advance lead
            _clips.Add(clip);
            return clip;
        }

        private void BuildCatalog(params (string key, AudioChannel channel, string[] playlist)[] playlists)
        {
            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            var so = new SerializedObject(_catalog);
            var entries = so.FindProperty("entries");
            var defs = new (string key, AudioChannel channel)[]
            {
                ("click", AudioChannel.Sfx),
                ("music_a", AudioChannel.Music),
                ("music_b", AudioChannel.Music)
            };
            entries.arraySize = defs.Length;
            for (var i = 0; i < defs.Length; i++)
            {
                // Grown serialized elements are ZEROED (initializers don't run through the
                // serializer) — set everything the tests rely on explicitly.
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = defs[i].key;
                entry.FindPropertyRelative("channel").enumValueIndex = (int)defs[i].channel;
                entry.FindPropertyRelative("volumeScale").floatValue = 1f;
                entry.FindPropertyRelative("minRetriggerInterval").floatValue = 0.05f;
                var clips = entry.FindPropertyRelative("clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = CreateClip(defs[i].key);
            }

            var lists = so.FindProperty("playlists");
            lists.arraySize = playlists.Length;
            for (var i = 0; i < playlists.Length; i++)
            {
                var list = lists.GetArrayElementAtIndex(i);
                list.FindPropertyRelative("key").stringValue = playlists[i].key;
                list.FindPropertyRelative("loop").boolValue = true;
                var keys = list.FindPropertyRelative("entryKeys");
                keys.arraySize = playlists[i].playlist.Length;
                for (var k = 0; k < playlists[i].playlist.Length; k++)
                    keys.GetArrayElementAtIndex(k).stringValue = playlists[i].playlist[k];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private int ActiveSfxCount()
        {
            var host = _service.Host;
            var count = 0;
            foreach (Transform child in host)
            {
                if (child.name.StartsWith("Sfx Template") && child.gameObject.activeSelf) count++;
            }

            return count;
        }

        private AudioSource MusicSourceWithClip()
        {
            foreach (Transform child in _service.Host)
            {
                if (child.name.StartsWith("Music"))
                {
                    var source = child.GetComponent<AudioSource>();
                    if (source && source.clip) return source;
                }
            }

            return null;
        }

        // ---------- state / mute / persistence ----------

        [Test]
        public void EffectiveVolumes_CombineSettingsAndMuteStack()
        {
            _service = new AudioService();

            Assert.AreEqual(1f, _service.EffectiveMusicVolume);
            Assert.AreEqual(1f, _service.EffectiveSfxVolume);

            _service.MusicVolume = 0.4f;
            Assert.AreEqual(0.4f, _service.EffectiveMusicVolume, 1e-4f);

            _service.MusicEnabled = false;
            Assert.AreEqual(0f, _service.EffectiveMusicVolume);
            Assert.AreEqual(1f, _service.EffectiveSfxVolume, "Channels are independent.");

            _service.MusicEnabled = true;
            _service.PushMute();
            Assert.IsTrue(_service.IsMuted);
            Assert.AreEqual(0f, _service.EffectiveMusicVolume);
            Assert.AreEqual(0f, _service.EffectiveSfxVolume);

            _service.PushMute();
            _service.PopMute();
            Assert.IsTrue(_service.IsMuted, "Ref-counted: the other holder still suppresses.");

            _service.PopMute();
            Assert.IsFalse(_service.IsMuted);
            Assert.AreEqual(0.4f, _service.EffectiveMusicVolume, 1e-4f, "Durable settings survive the mute cycle.");
        }

        [Test]
        public void UnbalancedPopMute_ThrowsLoudly()
        {
            _service = new AudioService();

            Assert.That(() => _service.PopMute(), Throws.InvalidOperationException);
        }

        [Test]
        public void Settings_PersistThroughTheSaveSystem()
        {
            var save = new FakeSaveSystem();
            _service = new AudioService(saveSystem: save);
            _service.MusicEnabled = false;
            _service.SfxVolume = 0.25f;
            _service.Dispose();

            _service = new AudioService(saveSystem: save);

            Assert.IsFalse(_service.MusicEnabled);
            Assert.AreEqual(0.25f, _service.SfxVolume, 1e-4f);
        }

        [Test]
        public void WithoutASaveSystem_ServiceIsRuntimeOnlyWithDefaults()
        {
            _service = new AudioService();

            Assert.IsTrue(_service.MusicEnabled);
            Assert.IsTrue(_service.SfxEnabled);
            Assert.DoesNotThrow(() => _service.MusicVolume = 0.5f);
        }

        [Test]
        public void Dispose_DestroysTheHost()
        {
            _service = new AudioService();
            Assert.IsNotNull(_service.Host);

            _service.Dispose();

            Assert.IsNull(_service.Host);
            Assert.IsNull(GameObject.Find("[TK.Audio]"));
        }

        // ---------- sfx ----------

        [Test]
        public void PlaySfx_SpawnsAPooledShot_AndThrottlesRapidReplays()
        {
            BuildCatalog();
            _service = new AudioService(_catalog);

            _service.PlaySfx("click");
            _service.PlaySfx("click"); // inside the 0.05 s retrigger window — dropped

            Assert.AreEqual(1, ActiveSfxCount(), "The second play inside the throttle window must be dropped.");
        }

        [Test]
        public void PlaySfx_SpawnsNothing_WhenDisabledOrMuted()
        {
            BuildCatalog();
            _service = new AudioService(_catalog);

            _service.SfxEnabled = false;
            _service.PlaySfx("click");
            Assert.AreEqual(0, ActiveSfxCount());

            _service.SfxEnabled = true;
            _service.PushMute();
            _service.PlaySfx(CreateClip("direct")); // direct overload — no throttle, still gated
            Assert.AreEqual(0, ActiveSfxCount());
            _service.PopMute();
        }

        [Test]
        public void PlaySfx_DirectOverload_BypassesTheThrottle()
        {
            _service = new AudioService();
            var clip = CreateClip("direct");

            _service.PlaySfx(clip);
            _service.PlaySfx(clip);

            Assert.AreEqual(2, ActiveSfxCount(), "Direct-clip shots carry no catalog throttle.");
        }

        [Test]
        public void PushMute_SilencesInFlightShotsImmediately()
        {
            _service = new AudioService();
            _service.PlaySfx(CreateClip("direct"), volumeScale: 0.8f);
            var source = _service.Host.GetComponentsInChildren<AudioSource>(false)[0];

            foreach (Transform child in _service.Host)
            {
                if (child.name.StartsWith("Sfx Template") && child.gameObject.activeSelf)
                    source = child.GetComponent<AudioSource>();
            }

            Assert.Greater(source.volume, 0f);

            _service.PushMute();

            Assert.AreEqual(0f, source.volume, "Ad-mute must silence already-playing shots.");
            _service.PopMute();
        }

        [Test]
        public void PlaySfx_UnknownKey_LogsError()
        {
            BuildCatalog();
            _service = new AudioService(_catalog);

            LogAssert.Expect(LogType.Error, "[AudioService] Unknown audio key 'nope'.");
            _service.PlaySfx("nope");
        }

        [Test]
        public void StringKeyCalls_WithoutACatalog_ErrorOnce()
        {
            _service = new AudioService();

            LogAssert.Expect(LogType.Error,
                "[AudioService] No AudioCatalog was provided — string-key call 'click' ignored (use the direct-clip overloads, or construct the service with a catalog).");
            _service.PlaySfx("click");
            _service.PlaySfx("click"); // second call must stay silent (warn-once)

            LogAssert.NoUnexpectedReceived();
        }

        // ---------- music / playlists ----------

        [Test]
        public void PlayMusic_ByKey_SetsActiveKeyAndAssignsTheClip()
        {
            BuildCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };

            _service.PlayMusic("music_a");

            Assert.AreEqual("music_a", _service.ActiveMusicKey);
            Assert.IsNull(_service.ActivePlaylistKey);
            var source = MusicSourceWithClip();
            Assert.IsNotNull(source);
            Assert.IsTrue(source.loop, "Single tracks loop by default.");
        }

        [Test]
        public void PlayMusic_SameKeyTwice_IsIdempotent()
        {
            BuildCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };

            _service.PlayMusic("music_a");
            var source = MusicSourceWithClip();
            var clip = source.clip;

            _service.PlayMusic("music_a");

            Assert.AreSame(clip, MusicSourceWithClip().clip, "Re-requesting the active track must not restart it.");
        }

        [Test]
        public void PlayPlaylist_SetsPlaylistStateAndStartsATrack()
        {
            BuildCatalog(("menu", AudioChannel.Music, new[] { "music_a", "music_b" }));
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };

            _service.PlayPlaylist("menu");

            Assert.AreEqual("menu", _service.ActivePlaylistKey);
            Assert.AreEqual("music_a", _service.ActiveMusicKey, "Non-shuffled playlists start at the first entry.");
            var source = MusicSourceWithClip();
            Assert.IsFalse(source.loop, "Playlist tracks must not loop individually (the list advances).");

            _service.PlayPlaylist("menu");
            Assert.AreEqual("menu", _service.ActivePlaylistKey, "Re-requesting the running playlist is a no-op.");
        }

        [Test]
        public void PlayMusic_ReplacesTheRunningPlaylist()
        {
            BuildCatalog(("menu", AudioChannel.Music, new[] { "music_a", "music_b" }));
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };
            _service.PlayPlaylist("menu");

            _service.PlayMusic("music_b");

            Assert.IsNull(_service.ActivePlaylistKey);
            Assert.AreEqual("music_b", _service.ActiveMusicKey);
        }

        [Test]
        public void StopMusic_ClearsTheActiveState()
        {
            BuildCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };
            _service.PlayMusic("music_a");

            _service.StopMusic();

            Assert.IsNull(_service.ActiveMusicKey);
            Assert.IsNull(_service.ActivePlaylistKey);
        }

        [Test]
        public void PlayPlaylist_UnknownKey_LogsError()
        {
            BuildCatalog();
            _service = new AudioService(_catalog);

            LogAssert.Expect(LogType.Error, "[AudioService] Unknown playlist key 'nope'.");
            _service.PlayPlaylist("nope");

            Assert.IsNull(_service.ActivePlaylistKey);
        }

        [Test]
        public void PlayPlaylist_WithNonMusicEntries_SkipsThemWithErrors()
        {
            BuildCatalog(("mixed", AudioChannel.Music, new[] { "click", "music_a" }));
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };

            LogAssert.Expect(LogType.Error,
                "[AudioService] Playlist entry 'click' is missing or not a Music entry — skipped.");
            _service.PlayPlaylist("mixed");

            Assert.AreEqual("music_a", _service.ActiveMusicKey, "Playable entries must survive the filtering.");
        }
    }
}
