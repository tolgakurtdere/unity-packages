using Newtonsoft.Json;
using UnityEngine;

namespace TK.Core.Save
{
    /// <summary>
    /// ISaveSystem implementation using PlayerPrefs with JSON serialization.
    /// Suitable for local development; can be swapped for cloud storage later.
    /// </summary>
    public class PlayerPrefsJsonSaveSystem : ISaveSystem
    {
        public void Save<T>(string key, T data)
        {
            var json = JsonConvert.SerializeObject(data);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public T Load<T>(string key, T defaultValue = default)
        {
            if (!PlayerPrefs.HasKey(key))
                return defaultValue;

            var json = PlayerPrefs.GetString(key);
            try
            {
                var result = JsonConvert.DeserializeObject<T>(json);
                return result ?? defaultValue;
            }
            catch
            {
                Debug.LogWarning($"[SaveSystem] Failed to deserialize key '{key}'. Returning default.");
                return defaultValue;
            }
        }

        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}