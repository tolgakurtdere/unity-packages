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

        /// <summary>Catalog of Sfx-only entries with explicit throttle + voice cap (each gets a fresh 1 s clip).</summary>
        private void BuildSfxCatalog(params (string key, float throttle, int maxVoices)[] sfx)
        {
            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            var so = new SerializedObject(_catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = sfx.Length;
            for (var i = 0; i < sfx.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = sfx[i].key;
                entry.FindPropertyRelative("channel").enumValueIndex = (int)AudioChannel.Sfx;
                entry.FindPropertyRelative("volumeScale").floatValue = 1f;
                entry.FindPropertyRelative("minRetriggerInterval").floatValue = sfx[i].throttle;
                entry.FindPropertyRelative("maxConcurrentVoices").intValue = sfx[i].maxVoices;
                var clips = entry.FindPropertyRelative("clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = CreateClip(sfx[i].key);
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

        [Test]
        public void MaxConcurrentVoices_CullsTheOldestAtTheCap()
        {
            BuildSfxCatalog(("capped", throttle: 0f, maxVoices: 2));
            _service = new AudioService(_catalog);

            _service.PlaySfx("capped");
            _service.PlaySfx("capped");
            _service.PlaySfx("capped"); // exceeds the cap → oldest culled before this one plays

            Assert.AreEqual(2, ActiveSfxCount(), "A capped key must never exceed its voice count.");
        }

        [Test]
        public void MaxConcurrentVoices_Zero_IsUnlimited()
        {
            BuildSfxCatalog(("free", throttle: 0f, maxVoices: 0));
            _service = new AudioService(_catalog);

            _service.PlaySfx("free");
            _service.PlaySfx("free");
            _service.PlaySfx("free");

            Assert.AreEqual(3, ActiveSfxCount(), "Cap 0 means no limit.");
        }

        [Test]
        public void PlaySfx_WithDelay_DoesNotSpawnOnTheSameFrame()
        {
            BuildSfxCatalog(("click", throttle: 0f, maxVoices: 0));
            _service = new AudioService(_catalog);

            _service.PlaySfx("click", 1f, 0.2f);

            Assert.AreEqual(0, ActiveSfxCount(), "A delayed shot must not spawn synchronously.");
        }

        [Test]
        public void StopSfx_StopsOnlyTheGivenKey()
        {
            BuildSfxCatalog(("a", throttle: 0f, maxVoices: 0), ("b", throttle: 0f, maxVoices: 0));
            _service = new AudioService(_catalog);
            _service.PlaySfx("a");
            _service.PlaySfx("b");
            Assert.AreEqual(2, ActiveSfxCount());

            _service.StopSfx("a");

            Assert.AreEqual(1, ActiveSfxCount(), "Only the 'a' voice must stop.");
        }

        [Test]
        public void StopAllSfx_ClearsEveryOneShot()
        {
            BuildSfxCatalog(("a", throttle: 0f, maxVoices: 0), ("b", throttle: 0f, maxVoices: 0));
            _service = new AudioService(_catalog);
            _service.PlaySfx("a");
            _service.PlaySfx("b");

            _service.StopAllSfx();

            Assert.AreEqual(0, ActiveSfxCount());
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

        private void BuildCliplessCatalog()
        {
            // Entries EXIST and are Music-channel, but have no direct clips and no valid
            // addressable — the exact shape that used to drive OnTrackUnavailable into
            // synchronous infinite recursion on a looping playlist (found by the first
            // consumer's play-mode gate).
            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            var so = new SerializedObject(_catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = 2;
            for (var i = 0; i < 2; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = i == 0 ? "silent_a" : "silent_b";
                entry.FindPropertyRelative("channel").enumValueIndex = (int)AudioChannel.Music;
                entry.FindPropertyRelative("volumeScale").floatValue = 1f;
                entry.FindPropertyRelative("clips").arraySize = 0;
            }

            var lists = so.FindProperty("playlists");
            lists.arraySize = 1;
            var list = lists.GetArrayElementAtIndex(0);
            list.FindPropertyRelative("key").stringValue = "menu";
            list.FindPropertyRelative("loop").boolValue = true;
            var keys = list.FindPropertyRelative("entryKeys");
            keys.arraySize = 2;
            keys.GetArrayElementAtIndex(0).stringValue = "silent_a";
            keys.GetArrayElementAtIndex(1).stringValue = "silent_b";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void PlayPlaylist_WithOnlyUnplayableTracks_FailsSafeWithoutSynchronousRecursion()
        {
            BuildCliplessCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };

            // Pre-fix this call froze the main thread and crashed with a stack overflow
            // (loop=true playlist + per-track failure chained without a frame yield).
            LogAssert.Expect(LogType.Error,
                "[AudioService] Music entry 'silent_a' has no direct clip and no valid addressable.");
            _service.PlayPlaylist("menu");

            // Returning at all proves the fix (the failure path parks on a frame yield instead
            // of recursing); exactly ONE track failed inside the synchronous window. The
            // full lap-counter stop is frame-driven and play-mode verified.
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayMusic_UnplayableEntry_ClearsStateInsteadOfLooping()
        {
            BuildCliplessCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };

            LogAssert.Expect(LogType.Error,
                "[AudioService] Music entry 'silent_a' has no direct clip and no valid addressable.");
            _service.PlayMusic("silent_a");

            Assert.IsNull(_service.ActiveMusicKey, "A failed single track must clear the active state.");
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

        // ---------- mute / pause (v0.3) ----------

        [Test]
        public void PushMute_PausesMusicWithoutClearingIt()
        {
            BuildCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };
            _service.PlayMusic("music_a");
            Assert.IsNotNull(MusicSourceWithClip(), "sanity: music is playing.");

            _service.PushMute();

            Assert.IsTrue(_service.IsMusicPaused);
            Assert.IsNotNull(MusicSourceWithClip(), "Pause must keep the clip — freeze, not stop.");
            Assert.AreEqual("music_a", _service.ActiveMusicKey, "The active track is unchanged by a pause.");

            _service.PopMute();
            Assert.IsFalse(_service.IsMusicPaused);
            Assert.IsNotNull(MusicSourceWithClip());
        }

        [Test]
        public void MusicPause_ComposesAdsMuteAndManualPause()
        {
            _service = new AudioService();
            Assert.IsFalse(_service.IsMusicPaused);

            _service.PushMute();
            Assert.IsTrue(_service.IsMusicPaused, "Ad mute pauses music.");

            _service.PauseMusic();
            _service.PopMute();
            Assert.IsTrue(_service.IsMusicPaused, "Still paused: the manual pause outlives the ad mute.");

            _service.ResumeMusic();
            Assert.IsFalse(_service.IsMusicPaused, "Resumes only once BOTH the mute and the manual pause clear.");
        }

        [Test]
        public void PauseResumeMusic_AreIdempotent()
        {
            _service = new AudioService();

            _service.PauseMusic();
            _service.PauseMusic(); // must not stack
            _service.ResumeMusic();

            Assert.IsFalse(_service.IsMusicPaused, "One resume clears the manual pause.");
            Assert.DoesNotThrow(() => _service.ResumeMusic(), "An extra resume is a no-op.");
        }

        [Test]
        public void SfxMute_StaysAVolumeGate_NotAffectedByMusicPause()
        {
            _service = new AudioService();

            _service.PauseMusic(); // music-only pause
            Assert.AreEqual(1f, _service.EffectiveSfxVolume, "PauseMusic must not touch the SFX channel.");

            _service.PushMute();
            Assert.AreEqual(0f, _service.EffectiveSfxVolume, "Ad mute still volume-gates SFX.");
        }

        // ---------- FadeChannelVolume + Changed event (v0.3) ----------

        [Test]
        public void Changed_FiresOnRealChange_NotOnNoOp()
        {
            _service = new AudioService();
            var count = 0;
            _service.Changed += () => count++;

            _service.MusicVolume = 0.5f;
            Assert.AreEqual(1, count, "A real change fires Changed once.");

            _service.MusicVolume = 0.5f; // no-op
            Assert.AreEqual(1, count, "A no-op write must not fire Changed.");

            _service.SfxEnabled = false;
            Assert.AreEqual(2, count);
        }

        [Test]
        public void FadeChannelVolume_ZeroSeconds_SetsMusicMultiplierInstantly_WithoutTouchingTheSetting()
        {
            _service = new AudioService();
            var changed = 0;
            _service.Changed += () => changed++;

            _service.FadeChannelVolume(AudioChannel.Music, 0.3f, 0f);

            Assert.AreEqual(0.3f, _service.EffectiveMusicVolume, 1e-4f, "The transient fade scales the audible volume.");
            Assert.AreEqual(1f, _service.MusicVolume, "The durable setting is untouched.");
            Assert.AreEqual(0, changed, "A transient fade must not raise Changed.");
        }

        [Test]
        public void FadeChannelVolume_ZeroSeconds_ScalesTheSfxChannel()
        {
            _service = new AudioService();

            _service.FadeChannelVolume(AudioChannel.Sfx, 0.5f, 0f);

            Assert.AreEqual(0.5f, _service.EffectiveSfxVolume, 1e-4f);
            Assert.AreEqual(1f, _service.SfxVolume, "The durable setting is untouched.");
        }

        [Test]
        public void FadeChannelVolume_ClampsTarget()
        {
            _service = new AudioService();

            _service.FadeChannelVolume(AudioChannel.Music, 5f, 0f);
            Assert.AreEqual(1f, _service.EffectiveMusicVolume, 1e-4f);

            _service.FadeChannelVolume(AudioChannel.Music, -1f, 0f);
            Assert.AreEqual(0f, _service.EffectiveMusicVolume, 1e-4f);
        }

        // ---------- settings-mute = stop + remembered request (v0.3) ----------

        [Test]
        public void DisablingMusic_StopsIt_AndReenablingReplaysTheRequest()
        {
            BuildCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };
            _service.PlayMusic("music_a");
            Assert.AreEqual("music_a", _service.ActiveMusicKey);

            _service.MusicEnabled = false;
            Assert.IsNull(_service.ActiveMusicKey, "Disabling music stops it.");

            _service.MusicEnabled = true;
            Assert.AreEqual("music_a", _service.ActiveMusicKey, "Re-enabling replays the remembered request from the top.");
        }

        [Test]
        public void PlayMusicWhileDisabled_RecordsButDoesNotStartUntilEnabled()
        {
            BuildCatalog();
            var save = new FakeSaveSystem();
            _service = new AudioService(_catalog, save) { MusicCrossfadeSeconds = 0f };
            _service.MusicEnabled = false;
            _service.Dispose();

            _service = new AudioService(_catalog, save) { MusicCrossfadeSeconds = 0f }; // boots muted
            Assert.IsFalse(_service.MusicEnabled);

            _service.PlayMusic("music_a");
            Assert.IsNull(_service.ActiveMusicKey, "A request made while disabled starts nothing.");

            _service.MusicEnabled = true;
            Assert.AreEqual("music_a", _service.ActiveMusicKey, "Enabling starts the remembered request.");
        }

        [Test]
        public void StopMusic_ClearsTheRequest_SoReenablingPlaysNothing()
        {
            BuildCatalog();
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };
            _service.PlayMusic("music_a");

            _service.StopMusic();
            Assert.IsNull(_service.ActiveMusicKey);

            _service.MusicEnabled = false;
            _service.MusicEnabled = true;
            Assert.IsNull(_service.ActiveMusicKey, "StopMusic forgot the request — there is nothing to replay.");
        }

        [Test]
        public void DisablingMusic_KeepsAPlaylistRequest()
        {
            BuildCatalog(("menu", AudioChannel.Music, new[] { "music_a", "music_b" }));
            _service = new AudioService(_catalog) { MusicCrossfadeSeconds = 0f };
            _service.PlayPlaylist("menu");
            Assert.AreEqual("menu", _service.ActivePlaylistKey);

            _service.MusicEnabled = false;
            Assert.IsNull(_service.ActivePlaylistKey);

            _service.MusicEnabled = true;
            Assert.AreEqual("menu", _service.ActivePlaylistKey, "The playlist request is replayed on re-enable.");
        }
    }
}
