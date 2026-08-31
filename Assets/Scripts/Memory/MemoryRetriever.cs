using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aigf.Companion.Memory
{
    public sealed class MemoryRetriever
    {
        private readonly IMemoryStore store;

        public MemoryRetriever(IMemoryStore memoryStore)
        {
            store = memoryStore;
        }

        public async Task<IReadOnlyList<MemoryItem>> RetrieveAsync(
            string query,
            int maxItems = 4,
            CancellationToken cancellationToken = default)
        {
            if (store == null || maxItems <= 0)
            {
                return Array.Empty<MemoryItem>();
            }

            var items = await store.GetAllAsync(cancellationToken);
            var tokens = Tokenize(query);
            var scored = new List<ScoredMemory>(items.Count);
            var now = DateTime.UtcNow;

            for (var i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Text)) continue;

                var text = item.Text.ToLowerInvariant();
                var keywordScore = 0f;
                foreach (var token in tokens)
                {
                    if (text.IndexOf(token, StringComparison.Ordinal) >= 0) keywordScore += 1f;
                }

                var ageDays = Math.Max(0d, (now - item.CreatedUtc).TotalDays);
                var recency = (float)(1d / (1d + ageDays / 14d));
                var score = keywordScore * 2f + item.Importance + recency * 0.5f;
                if (keywordScore > 0f || tokens.Count == 0)
                {
                    scored.Add(new ScoredMemory(item, score));
                }
            }

            scored.Sort((left, right) => right.Score.CompareTo(left.Score));
            var count = Math.Min(Math.Min(maxItems, 8), scored.Count);
            var result = new List<MemoryItem>(count);
            for (var i = 0; i < count; i++) result.Add(scored[i].Item);
            return result;
        }

        private static HashSet<string> Tokenize(string text)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var parts = (text ?? string.Empty).ToLowerInvariant().Split(
                new[] { ' ', '\t', '\r', '\n', '.', ',', '!', '?', ':', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length >= 3) result.Add(parts[i]);
            }

            return result;
        }

        private readonly struct ScoredMemory
        {
            public MemoryItem Item { get; }
            public float Score { get; }

            public ScoredMemory(MemoryItem item, float score)
            {
                Item = item;
                Score = score;
            }
        }
    }
}
