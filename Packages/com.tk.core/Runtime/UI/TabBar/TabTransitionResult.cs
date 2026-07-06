namespace TK.Core.UI
{
    public readonly struct TabTransitionResult
    {
        private TabTransitionResult(bool completed, float position)
        {
            Completed = completed;
            Position = position;
        }

        public bool Completed { get; }
        public float Position { get; }

        public static TabTransitionResult CompletedAt(float position) => new(true, position);
        public static TabTransitionResult InterruptedAt(float position) => new(false, position);
    }
}
