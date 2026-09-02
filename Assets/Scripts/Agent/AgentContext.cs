using System.Collections.Generic;
using Aigf.Companion.Memory;
using Aigf.Companion.Room;
using UnityEngine;

namespace Aigf.Companion.Agent
{
    public sealed class AgentContext
    {
        public Vector3 UserPosition { get; set; }
        public Vector3 UserForward { get; set; }
        public Vector3 AvatarPosition { get; set; }
        public AgentState State { get; set; }
        public RoomGraph Room { get; set; }
        public IReadOnlyList<MemoryItem> Memories { get; set; }
        public IReadOnlyList<ConversationTurn> RecentTurns { get; set; }

        public float DistanceToUser => Vector3.Distance(AvatarPosition, UserPosition);
    }
}
