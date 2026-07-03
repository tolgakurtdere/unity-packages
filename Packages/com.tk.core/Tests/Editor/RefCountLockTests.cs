using TK.Core.Utilities;
using NUnit.Framework;

namespace TK.Core.Tests
{
    [TestFixture]
    public class RefCountLockTests
    {
        private RefCountLock _lock;

        [SetUp]
        public void SetUp() => _lock = new RefCountLock();

        // ── Initial state ────────────────────────────────────────────

        [Test]
        public void InitialState_IsNotLocked()
        {
            Assert.IsFalse(_lock.IsLocked);
        }

        // ── Lock ─────────────────────────────────────────────────────

        [Test]
        public void AfterOneLock_IsLocked()
        {
            _lock.Lock();
            Assert.IsTrue(_lock.IsLocked);
        }

        [Test]
        public void AfterMultipleLocks_IsLocked()
        {
            _lock.Lock();
            _lock.Lock();
            _lock.Lock();
            Assert.IsTrue(_lock.IsLocked);
        }

        // ── Unlock ────────────────────────────────────────────────────

        [Test]
        public void LockThenUnlock_IsNotLocked()
        {
            _lock.Lock();
            _lock.Unlock();
            Assert.IsFalse(_lock.IsLocked);
        }

        [Test]
        public void ThreeLocksThreeUnlocks_IsNotLocked()
        {
            _lock.Lock();
            _lock.Lock();
            _lock.Lock();
            _lock.Unlock();
            _lock.Unlock();
            _lock.Unlock();
            Assert.IsFalse(_lock.IsLocked);
        }

        [Test]
        public void TwoLocksOneUnlock_StillLocked()
        {
            _lock.Lock();
            _lock.Lock();
            _lock.Unlock();
            Assert.IsTrue(_lock.IsLocked, "Should remain locked after one unlock when locked twice.");
        }

        // ── Reference-count guard ────────────────────────────────────

        [Test]
        public void UnlockWithoutLock_ThrowsInvalidOperation()
        {
            Assert.Throws<System.InvalidOperationException>(() => _lock.Unlock());
        }

        [Test]
        public void ExtraUnlockAfterBalanced_ThrowsInvalidOperation()
        {
            _lock.Lock();
            _lock.Unlock();
            Assert.Throws<System.InvalidOperationException>(() => _lock.Unlock());
        }

        // ── Interface contract ────────────────────────────────────────

        [Test]
        public void ImplementsIRefCountLock()
        {
            Assert.IsInstanceOf<IRefCountLock>(_lock);
        }

        [Test]
        public void IRefCountLock_LockAndUnlock_WorksViaInterface()
        {
            IRefCountLock iLock = _lock;
            iLock.Lock();
            Assert.IsTrue(iLock.IsLocked);
            iLock.Unlock();
            Assert.IsFalse(iLock.IsLocked);
        }
    }
}
