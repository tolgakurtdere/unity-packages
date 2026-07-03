namespace TK.Core.Save
{
    /// <summary>
    /// Interface for save/load operations.
    /// Allows swapping implementations (PlayerPrefs, File, Cloud) without changing game code.
    /// </summary>
    public interface ISaveSystem
    {
        /// <summary>
        /// Saves data with the given key.
        /// </summary>
        void Save<T>(string key, T data);

        /// <summary>
        /// Loads data for the given key.
        /// </summary>
        /// <returns>Loaded data or defaultValue if key not found.</returns>
        T Load<T>(string key, T defaultValue = default);

        /// <summary>
        /// Checks if key exists.
        /// </summary>
        bool HasKey(string key);

        /// <summary>
        /// Deletes the data for the given key.
        /// </summary>
        void Delete(string key);

        /// <summary>
        /// Deletes all saved data.
        /// </summary>
        void DeleteAll();
    }
}