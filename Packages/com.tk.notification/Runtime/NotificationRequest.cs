using System;

namespace TK.Notification
{
    /// <summary>A local notification the game asks to schedule. DeliveryTime is absolute (device-local wall clock);
    /// the service applies quiet-hours before scheduling. Data is an opaque payload surfaced on launch.</summary>
    public readonly struct NotificationRequest
    {
        public string ChannelId { get; }
        public string Title { get; }
        public string Body { get; }
        public DateTime DeliveryTime { get; }
        public string Data { get; }
        public string SmallIcon { get; }
        public string LargeIcon { get; }

        public NotificationRequest(string channelId, string title, string body, DateTime deliveryTime,
            string data = null, string smallIcon = null, string largeIcon = null)
        {
            ChannelId = channelId;
            Title = title;
            Body = body;
            DeliveryTime = deliveryTime;
            Data = data;
            SmallIcon = smallIcon;
            LargeIcon = largeIcon;
        }
    }
}
