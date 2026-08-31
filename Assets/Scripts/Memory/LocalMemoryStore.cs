using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Memory
{
    public sealed class LocalMemoryStore : IMemoryStore
    {
        [Serializable]
        private sealed class MemoryCollection
        {
            public List<MemoryItem> items = new List<MemoryItem>();
        }

        private readonly string path;
        private readonly object gate = new object();
        private MemoryCollection collection;

        public LocalMemoryStore(string explicitPath = null)
        {
            path = string.IsNullOrWhiteSpace(explicitPath)
                ? Path.Combine(Application.persistentDataPath, "memory.json")
                : explicitPath;
        }

        public Task AddAsync(MemoryItem item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item == null || string.IsNullOrWhiteSpace(item.Text))
            {
                return Task.CompletedTask;
            }

            lock (gate)
            {
                EnsureLoaded();
                collection.items.Add(item);
                if (collection.items.Count > 200)
                {
                    collection.items.RemoveRange(0, collection.items.Count - 200);
                }

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(collection, true));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                EnsureLoaded();
                return Task.FromResult<IReadOnlyList<MemoryItem>>(new List<MemoryItem>(collection.items));
            }
        }

        private void EnsureLoaded()
        {
            if (collection != null)
            {
                return;
            }

            collection = new MemoryCollection();
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var loaded = JsonUtility.FromJson<MemoryCollection>(File.ReadAllText(path));
                if (loaded != null && loaded.items != null)
                {
                    collection = loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AI] Local memory could not be read: {exception.Message}");
            }
        }
    }
}
