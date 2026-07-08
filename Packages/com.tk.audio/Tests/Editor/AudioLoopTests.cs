using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TK.Audio.Tests
{
    [TestFixture]
    public class AudioLoopTests
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
            var clip = AudioClip.Create(name, 44100, 1, 44100, false);
            _clips.Add(clip);
            return clip;
        }

        /// <summary>Builds a catalog of Sfx entries (one clip each) for loop tests.</summary>
        private void BuildLoopCatalog(params string[] keys)
        {
            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            var so = new SerializedObject(_catalog);
            var entries = so.FindProperty("entries");
            entries.arraySize = keys.Length;
            for (var i = 0; i < keys.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = keys[i];
                entry.FindPropertyRelative("channel").enumValueIndex = (int)AudioChannel.Sfx;
                entry.FindPropertyRelative("volumeScale").floatValue = 1f;
                var clips = entry.FindPropertyRelative("clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = CreateClip(keys[i]);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private int ActiveLoopCount()
        {
            var count = 0;
            foreach (Transform child in _service.Host)
            {
                if (child.name.StartsWith("Sfx Loop") && child.gameObject.activeSelf) count++;
            }

            return count;
        }

        private int ActiveOneShotCount()
        {
            var count = 0;
            foreach (Transform child in _service.Host)
            {
                if (child.name.StartsWith("Sfx Template") && child.gameObject.activeSelf) count++;
            }

            return count;
        }

        private AudioSource FirstActiveLoopSource()
        {
            foreach (Transform child in _service.Host)
            {
                if (child.name.StartsWith("Sfx Loop") && child.gameObject.activeSelf)
                    return child.GetComponent<AudioSource>();
            }

            return null;
        }

        [Test]
        public void DefaultHandle_IsASafeNoOp()
        {
            var handle = default(AudioHandle);

            Assert.IsFalse(handle.IsPlaying);
            Assert.DoesNotThrow(() => handle.Stop());
            Assert.DoesNotThrow(() => handle.FadeOutAndStop(1f));
        }

        [Test]
        public void PlaySfxLoop_UsesADedicatedSource_NotTheOneShotPool()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);

            var handle = _service.PlaySfxLoop("ambience");

            Assert.IsTrue(handle.IsPlaying);
            Assert.AreEqual(1, ActiveLoopCount(), "The loop must use a dedicated loop source.");
            Assert.AreEqual(0, ActiveOneShotCount(), "A loop must not consume the one-shot pool.");
        }

        [Test]
        public void Stop_RecyclesTheLoopVoice()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);
            var handle = _service.PlaySfxLoop("ambience");

            handle.Stop();

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, ActiveLoopCount());
        }

        [Test]
        public void Stop_Twice_AndStaleHandle_AreNoOps()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);
            var handle = _service.PlaySfxLoop("ambience");

            handle.Stop();

            Assert.DoesNotThrow(() => handle.Stop(), "A second Stop must be a no-op.");
            Assert.IsFalse(handle.IsPlaying);
        }

        [Test]
        public void FadeOutAndStop_ZeroSeconds_StopsImmediately()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);
            var handle = _service.PlaySfxLoop("ambience");

            handle.FadeOutAndStop(0f);

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, ActiveLoopCount());
        }

        [Test]
        public void PushMute_SilencesARunningLoop_AndPopRestoresIt()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);
            _service.PlaySfxLoop("ambience");
            var source = FirstActiveLoopSource();
            Assert.Greater(source.volume, 0f);

            _service.PushMute();
            Assert.AreEqual(0f, source.volume, "Ad-mute must silence a running loop immediately.");

            _service.PopMute();
            Assert.Greater(source.volume, 0f, "The loop returns to its volume after the mute.");
        }

        [Test]
        public void PlaySfxLoop_WhileSfxDisabled_ReturnsANonPlayingHandle()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);
            _service.SfxEnabled = false;

            var handle = _service.PlaySfxLoop("ambience");

            Assert.IsFalse(handle.IsPlaying, "A loop must not start while the channel is silent.");
            Assert.AreEqual(0, ActiveLoopCount());
        }

        [Test]
        public void StopAllSfx_StopsLoopsToo()
        {
            BuildLoopCatalog("ambience");
            _service = new AudioService(_catalog);
            var handle = _service.PlaySfxLoop("ambience");

            _service.StopAllSfx();

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, ActiveLoopCount());
        }

        [Test]
        public void StopSfx_ByKey_StopsMatchingLoopsOnly()
        {
            BuildLoopCatalog("wind", "engine");
            _service = new AudioService(_catalog);
            var wind = _service.PlaySfxLoop("wind");
            var engine = _service.PlaySfxLoop("engine");
            Assert.AreEqual(2, ActiveLoopCount());

            _service.StopSfx("wind");

            Assert.IsFalse(wind.IsPlaying, "The 'wind' loop must stop.");
            Assert.IsTrue(engine.IsPlaying, "The 'engine' loop must keep playing.");
        }
    }
}
