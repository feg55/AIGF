using System;
using System.Collections.Generic;
using Aigf.Companion.AI;
using Aigf.Companion.Room;
using UnityEngine;

namespace Aigf.Companion.Agent
{
    public static class AgentSafety
    {
        private static readonly HashSet<string> SupportedActions =
            new HashSet<string>(AgentActionTypes.All, StringComparer.Ordinal);

        public static bool ValidateAction(AgentAction action, RoomGraph room, out string error)
        {
            if (action == null)
            {
                error = "Action is null.";
                return false;
            }

            action.Type = (action.Type ?? string.Empty).Trim().ToLowerInvariant();
            action.Target = (action.Target ?? string.Empty).Trim();
            action.Animation = (action.Animation ?? string.Empty).Trim();

            if (!SupportedActions.Contains(action.Type))
            {
                error = $"Unsupported action type '{action.Type}'.";
                return false;
            }

            if (float.IsNaN(action.Distance) || float.IsInfinity(action.Distance))
            {
                error = "Action distance is not finite.";
                return false;
            }

            if (action.Distance != 0f)
            {
                action.Distance = Mathf.Clamp(action.Distance, 0.6f, 2f);
            }

            if (RequiresRoomTarget(action.Type))
            {
                if (string.IsNullOrWhiteSpace(action.Target))
                {
                    error = $"Action '{action.Type}' requires a room target ID.";
                    return false;
                }

                if (room == null || !room.TryGetNode(action.Target, out _))
                {
                    error = $"Unknown room target '{action.Target}'.";
                    return false;
                }
            }

            if (action.Type == AgentActionTypes.PlayAnimation && string.IsNullOrWhiteSpace(action.Animation))
            {
                error = "play_animation requires an animation ID.";
                return false;
            }

            if (action.Animation.Length > 64 || action.Target.Length > 64)
            {
                error = "Action identifier exceeds the maximum length.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool RequiresRoomTarget(string actionType)
        {
            return actionType == AgentActionTypes.WalkTo ||
                   actionType == AgentActionTypes.Sit ||
                   actionType == AgentActionTypes.LookAt;
        }
    }
}
