using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TK.Audio.Tests
{
    [TestFixture]
    public class AudioStaticFacadeTests
    {
        private AudioService _service;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            Audio.Unbind(Audio.Service); // static hygiene between tests
        }

        [TearDown]
        public void TearDown()
        {
            Audio.Unbind(Audio.Service);
            _service?.Dispose();
            _service = null;
            if (_clip) Object.DestroyImmediate(_clip);
            _clip = null;
        }

        [Test]
        public void Bind_RoutesCallsToTheService()
        {
            _service = new AudioService();
            _clip = AudioClip.Create("clip", 44100, 1, 44100, false);
            Audio.Bind(_service);

            Audio.SfxVolume = 0.3f;
            Audio.PlaySfx(_clip);

            Assert.AreEqual(0.3f, _service.SfxVolume, 1e-4f);
            var activeShots = 0;
            foreach (Transform child in _service.Host)
            {
                if (child.name.StartsWith("Sfx Template") && child.gameObject.activeSelf) activeShots++;
            }

            Assert.AreEqual(1, activeShots);
        }

        [Test]
        public void UnboundCalls_WarnOnceAndNoOp()
        {
            LogAssert.Expect(LogType.Warning,
                "[Audio] No AudioService bound — call Audio.Bind(service) at your composition root.");
            Audio.StopMusic();
            Audio.PushMute(); // second unbound call must stay silent

            Assert.IsTrue(Audio.MusicEnabled, "Unbound getters fall back to defaults.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Unbind_OnlyDetachesTheBoundInstance()
        {
            _service = new AudioService();
            Audio.Bind(_service);

            using var other = new DisposableService();
            Audio.Unbind(other.Service);
            Assert.AreSame(_service, Audio.Service, "Unbinding a different instance must be a no-op.");

            Audio.Unbind(_service);
            Assert.IsNull(Audio.Service);
        }

        private sealed class DisposableService : System.IDisposable
        {
            public AudioService Service { get; } = new();
            public void Dispose() => Service.Dispose();
        }
    }
}
