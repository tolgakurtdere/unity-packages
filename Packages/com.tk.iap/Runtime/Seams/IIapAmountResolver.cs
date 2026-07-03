namespace TK.IAP
{
    /// <summary>
    /// Optional seam for overriding catalog item amounts at read time (e.g. from remote config).
    /// Called wherever amounts are read (purchase application AND UI display), so late-arriving
    /// config is picked up automatically. Return defaultAmount to keep the catalog value.
    /// </summary>
    public interface IIapAmountResolver
    {
        int Resolve(string productId, string itemType, int defaultAmount);
    }
}
