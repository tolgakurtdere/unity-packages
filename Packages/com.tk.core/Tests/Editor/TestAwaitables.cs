using UnityEngine;

namespace TK.Core.Tests
{
    /// <summary>Synchronously-completed Awaitables for driving async flows in EditMode tests.</summary>
    internal static class TestAwaitables
    {
        public static Awaitable Completed()
        {
            var source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }

        public static Awaitable<bool> Completed(bool result)
        {
            var source = new AwaitableCompletionSource<bool>();
            source.SetResult(result);
            return source.Awaitable;
        }
    }
}
