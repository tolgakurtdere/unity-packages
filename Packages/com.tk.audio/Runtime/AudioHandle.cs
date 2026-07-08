namespace TK.Audio
{
    /// <summary>
    /// A control handle for a looping SFX (from <see cref="AudioService.PlaySfxLoop(string, float)"/>).
    /// Default-safe: <c>default(AudioHandle)</c> and a handle to an already-stopped or recycled
    /// voice are silent no-ops, so callers never need null/liveness checks before calling.
    /// </summary>
    public readonly struct AudioHandle
    {
        // SfxPlayer is internal; a private field of an internal type on a public struct is legal
        // and does not leak the type to consumers (they see only the members below).
        private readonly SfxPlayer _owner;
        private readonly int _id;

        internal AudioHandle(SfxPlayer owner, int id)
        {
            _owner = owner;
            _id = id;
        }

        /// <summary>True while the loop voice is still active (audible or temporarily muted).</summary>
        public bool IsPlaying => _owner != null && _owner.IsLoopPlaying(_id);

        /// <summary>Stops the loop immediately and recycles its voice. Safe to call more than once.</summary>
        public void Stop() => _owner?.StopLoop(_id);

        /// <summary>Fades the loop to silent over <paramref name="seconds"/> (unscaled), then stops it. 0 = stop now.</summary>
        public void FadeOutAndStop(float seconds) => _owner?.FadeLoop(_id, seconds);
    }
}
