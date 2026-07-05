namespace TK.Localization
{
    /// <summary>Persists the user's chosen locale code. Implementations decide the storage + key.</summary>
    public interface ILocalePersistence
    {
        string Load();                 // saved locale code, or null if none
        void Save(string localeCode);
    }
}
