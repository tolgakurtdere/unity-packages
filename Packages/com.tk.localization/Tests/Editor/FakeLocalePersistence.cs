using TK.Localization;

namespace TK.Localization.Tests
{
    /// <summary>In-memory ILocalePersistence for tests: holds the code in a field and counts Save calls.</summary>
    public sealed class FakeLocalePersistence : ILocalePersistence
    {
        private string _stored;

        public int SaveCount { get; private set; }

        public FakeLocalePersistence(string initial = null)
        {
            _stored = initial;
        }

        public string Load() => _stored;

        public void Save(string localeCode)
        {
            _stored = localeCode;
            SaveCount++;
        }
    }
}
