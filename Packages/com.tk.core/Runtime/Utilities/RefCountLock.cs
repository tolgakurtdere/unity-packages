using System;

namespace TK.Core.Utilities
{
    /// <summary>
    /// Reference-counted lock.
    /// Each Lock() call increments the counter; each Unlock() decrements it.
    /// IsLocked is true as long as the counter is above zero.
    /// This allows nested locks (e.g. two independent operations requiring exclusivity simultaneously,
    /// such as input locking during animations or evaluations)
    /// without accidentally unlocking prematurely.
    /// </summary>
    public class RefCountLock : IRefCountLock
    {
        private int _lockCount;

        public bool IsLocked => _lockCount > 0;

        public void Lock() => _lockCount++;

        public void Unlock()
        {
            if (_lockCount <= 0)
                throw new InvalidOperationException("Unlock called more times than Lock.");

            _lockCount--;
        }
    }
}