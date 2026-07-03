using System.Collections.Generic;
using TK.Core.Save;

namespace TK.IAP.Tests
{
    public class FakeSaveSystem : ISaveSystem
    {
        private readonly Dictionary<string, object> _store = new();

        public void Save<T>(string key, T data) => _store[key] = data;

        public T Load<T>(string key, T defaultValue = default)
        {
            return _store.TryGetValue(key, out var value) ? (T)value : defaultValue;
        }

        public bool HasKey(string key) => _store.ContainsKey(key);
        public void Delete(string key) => _store.Remove(key);
        public void DeleteAll() => _store.Clear();
    }
}
