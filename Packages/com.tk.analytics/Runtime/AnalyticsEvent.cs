using System.Collections.Generic;

namespace TK.Analytics
{
    /// <summary>An event name plus its parameters — the unit that is buffered and dispatched.</summary>
    public readonly struct AnalyticsEvent
    {
        public string Name { get; }
        public IReadOnlyList<AnalyticsParam> Parameters { get; }   // never null; empty when none

        public AnalyticsEvent(string name, IReadOnlyList<AnalyticsParam> parameters = null)
        {
            Name = name;
            Parameters = parameters ?? System.Array.Empty<AnalyticsParam>();
        }
    }
}
