using System;
using System.Collections.Generic;
using System.Text;

namespace Aigf.Companion.Room
{
    public sealed class RoomGraph
    {
        private readonly List<RoomNode> nodes = new List<RoomNode>();
        private readonly Dictionary<string, RoomNode> byId =
            new Dictionary<string, RoomNode>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RoomNode> Nodes => nodes;
        public int Count => nodes.Count;

        public RoomGraph()
        {
        }

        public RoomGraph(IEnumerable<RoomNode> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var node in source)
            {
                AddOrReplace(node, out _);
            }
        }

        public bool AddOrReplace(RoomNode node, out string error)
        {
            if (node == null)
            {
                error = "Room node is null.";
                return false;
            }

            var id = (node.Id ?? string.Empty).Trim();
            if (!IsSafeId(id))
            {
                error = $"Room node ID '{id}' is invalid. Use letters, digits, '_' or '-'.";
                return false;
            }

            if (byId.TryGetValue(id, out var previous))
            {
                nodes.Remove(previous);
            }

            byId[id] = node;
            nodes.Add(node);
            error = string.Empty;
            return true;
        }

        public void Clear()
        {
            nodes.Clear();
            byId.Clear();
        }

        public bool TryGetNode(string id, out RoomNode node)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                node = null;
                return false;
            }

            return byId.TryGetValue(id.Trim(), out node) && node != null;
        }

        public RoomNode FindFirst(RoomNodeType type)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].Type == type)
                {
                    return nodes[i];
                }
            }

            return null;
        }

        public string ToPromptSummary(int maxNodes = 24)
        {
            var builder = new StringBuilder(256);
            var count = Math.Min(Math.Max(maxNodes, 0), nodes.Count);
            for (var i = 0; i < count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                builder.Append("- id=").Append(node.Id)
                    .Append(" type=").Append(node.Type.ToString().ToLowerInvariant())
                    .Append(" canSit=").Append(node.CanSit ? "true" : "false")
                    .AppendLine();
            }

            return builder.ToString();
        }

        private static bool IsSafeId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!(char.IsLetterOrDigit(character) || character == '_' || character == '-'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
