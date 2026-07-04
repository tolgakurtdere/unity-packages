namespace TK.Ads.Tests
{
    public sealed class FakeClock
    {
        public float Now;
        public float Read() => Now;
        public void Advance(float seconds) => Now += seconds;
    }
}
