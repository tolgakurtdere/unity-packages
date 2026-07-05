namespace TK.Notification
{
    /// <summary>The notification that launched/resumed the app (from a tap).</summary>
    public readonly struct NotificationResponse
    {
        public string ChannelId { get; }
        public string Data { get; }

        public NotificationResponse(string channelId, string data)
        {
            ChannelId = channelId;
            Data = data;
        }
    }
}
