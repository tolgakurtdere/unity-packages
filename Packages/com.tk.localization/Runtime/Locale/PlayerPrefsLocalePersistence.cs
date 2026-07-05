using System;
using UnityEngine;

namespace TK.Localization
{
    /// <summary>ILocalePersistence backed by PlayerPrefs under a game-provided key.</summary>
    public sealed class PlayerPrefsLocalePersistence : ILocalePersistence
    {
        private readonly string _key;

        public PlayerPrefsLocalePersistence(string playerPrefsKey)
        {
            if (string.IsNullOrEmpty(playerPrefsKey))
                throw new ArgumentException("A non-empty PlayerPrefs key is required.", nameof(playerPrefsKey));
            _key = playerPrefsKey;
        }

        public string Load()
        {
            var value = PlayerPrefs.GetString(_key, null);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public void Save(string localeCode) => PlayerPrefs.SetString(_key, localeCode ?? string.Empty);
    }
}
