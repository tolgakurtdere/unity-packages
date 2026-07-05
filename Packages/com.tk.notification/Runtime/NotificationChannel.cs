namespace TK.Notification
{
    /// <summary>A notification channel (Android). On iOS the id is used only for thread/category grouping.</summary>
    public readonly struct NotificationChannel
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public NotificationImportance Importance { get; }

        public NotificationChannel(string id, string name, string description = null,
            NotificationImportance importance = NotificationImportance.Default)
        {
            Id = id;
            Name = name;
            Description = description;
            Importance = importance;
        }
    }
}
