using System;

namespace Aigf.Companion.Memory
{
    [Serializable]
    public sealed class MemoryItem
    {
        public string Id;
        public string Text;
        public float Importance;
        public long CreatedUtcTicks;

        public DateTime CreatedUtc => CreatedUtcTicks > 0
            ? new DateTime(CreatedUtcTicks, DateTimeKind.Utc)
            : DateTime.UnixEpoch;

        public MemoryItem()
        {
        }

        public MemoryItem(string text, float importance = 0.5f)
        {
            Id = Guid.NewGuid().ToString("N");
            Text = text ?? string.Empty;
            Importance = Math.Max(0f, Math.Min(1f, importance));
            CreatedUtcTicks = DateTime.UtcNow.Ticks;
        }
    }
}
