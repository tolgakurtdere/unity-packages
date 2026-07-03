namespace TK.Core.Utilities
{
    /// <summary>
    /// Controls whether a resource is available or should be considered locked.
    /// Locked while one or more operations that require exclusivity are in progress.
    /// </summary>
    public interface IRefCountLock
    {
        bool IsLocked { get; }
        void Lock();
        void Unlock();
    }
}
