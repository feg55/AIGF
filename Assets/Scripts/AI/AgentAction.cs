using System;

namespace Aigf.Companion.AI
{
    [Serializable]
    public sealed class AgentAction
    {
        public string Type;
        public string Target;
        public float Distance;
        public string Animation;

        public AgentAction()
        {
        }

        public AgentAction(string type, string target = "", float distance = 0f, string animation = "")
        {
            Type = type ?? string.Empty;
            Target = target ?? string.Empty;
            Distance = distance;
            Animation = animation ?? string.Empty;
        }
    }

    public static class AgentActionTypes
    {
        public const string WalkToUser = "walk_to_user";
        public const string WalkTo = "walk_to";
        public const string Sit = "sit";
        public const string Stand = "stand";
        public const string FollowUser = "follow_user";
        public const string Stop = "stop";
        public const string LookAtUser = "look_at_user";
        public const string LookAt = "look_at";
        public const string Wave = "wave";
        public const string PlayAnimation = "play_animation";

        public static readonly string[] All =
        {
            WalkToUser, WalkTo, Sit, Stand, FollowUser,
            Stop, LookAtUser, LookAt, Wave, PlayAnimation
        };
    }
}
