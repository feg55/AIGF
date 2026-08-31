using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aigf.Companion.Room
{
    public sealed class ManualRoomProvider : MonoBehaviour, IRoomProvider
    {
        [SerializeField] private RoomNode[] nodes;
        [SerializeField] private bool includeInactive = true;

        public bool IsReady { get; private set; }
        public RoomGraph Current { get; private set; }

        public Task<RoomGraph> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nodes == null || nodes.Length == 0)
            {
                nodes = GetComponentsInChildren<RoomNode>(includeInactive);
            }

            Current = new RoomGraph();
            for (var i = 0; i < nodes.Length; i++)
            {
                if (!Current.AddOrReplace(nodes[i], out var error))
                {
                    Debug.LogWarning($"[ROOM] Ignored manual node: {error}", this);
                }
            }

            IsReady = true;
            Debug.Log($"[ROOM] Manual room loaded with {Current.Count} semantic nodes.", this);
            return Task.FromResult(Current);
        }

        public void SetNodes(RoomNode[] roomNodes)
        {
            nodes = roomNodes;
            IsReady = false;
        }
    }
}
