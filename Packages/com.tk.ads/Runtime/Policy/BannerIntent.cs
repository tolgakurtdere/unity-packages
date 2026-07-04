namespace TK.Ads
{
    /// <summary>
    /// Single source of truth for banner visibility (the reference scattered this across
    /// three classes — a real defect source). Intent survives load latency: ShowBanner()
    /// before the banner loads means "show it when it arrives".
    /// </summary>
    public sealed class BannerIntent
    {
        public bool WantsVisible { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsVisible { get; private set; }

        /// <summary>Record show intent. Returns true when the gateway should be told to show NOW.</summary>
        public bool RequestShow()
        {
            WantsVisible = true;
            if (!IsLoaded || IsVisible) return false;
            IsVisible = true;
            return true;
        }

        public void RequestHide()
        {
            WantsVisible = false;
            IsVisible = false;
        }

        /// <summary>Banner loaded (or refreshed). Returns true when it should be auto-shown now.</summary>
        public bool OnLoaded()
        {
            IsLoaded = true;
            if (!WantsVisible || IsVisible) return false;
            IsVisible = true;
            return true;
        }

        public void Reset() // destroy
        {
            WantsVisible = false;
            IsLoaded = false;
            IsVisible = false;
        }
    }
}
