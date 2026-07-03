using System;
using NUnit.Framework;
using TK.Core.App;
using AppContext = TK.Core.App.AppContext;

namespace TK.Core.Tests
{
    [TestFixture]
    public class AppContextTests
    {
        private AppContext _context;

        [SetUp]
        public void SetUp() => _context = new AppContext(new FakeSaveSystem());

        private class DummyService
        {
        }

        [Test]
        public void Constructor_NullSaveSystem_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new AppContext(null));
        }

        [Test]
        public void SaveSystem_IsExposedTyped()
        {
            Assert.IsNotNull(_context.SaveSystem);
        }

        [Test]
        public void RegisterAndGet_RoundTrips()
        {
            var service = new DummyService();
            _context.Register(service);

            Assert.AreSame(service, _context.Get<DummyService>());
        }

        [Test]
        public void Get_Unregistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _context.Get<DummyService>());
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalse()
        {
            Assert.IsFalse(_context.TryGet<DummyService>(out _));
        }

        [Test]
        public void Register_Duplicate_Throws()
        {
            _context.Register(new DummyService());
            Assert.Throws<InvalidOperationException>(() => _context.Register(new DummyService()));
        }

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _context.Register<DummyService>(null));
        }

        [Test]
        public void RaiseGameEnded_NotifiesSubscribers()
        {
            GameEndResult? received = null;
            _context.OnGameEnded += r => received = r;

            _context.RaiseGameEnded(GameEndResult.Win);

            Assert.AreEqual(GameEndResult.Win, received);
        }
    }
}
