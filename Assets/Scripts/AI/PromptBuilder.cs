using System.Globalization;
using System.Text;
using Aigf.Companion.Agent;

namespace Aigf.Companion.AI
{
    public sealed class PromptBuilder
    {
        public string Build(string userMessage, AgentContext context)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("You are an embodied companion in the user's room.");
            builder.AppendLine("Return one JSON object only. Do not claim actions are complete.");
            builder.AppendLine("Allowed actions: walk_to_user,walk_to,sit,stand,follow_user,stop,look_at_user,look_at,wave,play_animation.");
            builder.AppendLine("Use only listed room IDs. Max 5 actions. Keep speech short.");
            builder.AppendLine("Schema: {\"speech\":\"\",\"emotion\":\"neutral\",\"actions\":[{\"type\":\"\",\"target\":\"\",\"distance\":0,\"animation\":\"\"}]}");

            if (context != null)
            {
                builder.Append("USER position=").Append(Vector(context.UserPosition))
                    .Append(" forward=").AppendLine(Vector(context.UserForward));
                builder.Append("AVATAR position=").Append(Vector(context.AvatarPosition))
                    .Append(" state=").Append(context.State.ToString().ToLowerInvariant())
                    .Append(" distance=").AppendLine(context.DistanceToUser.ToString("0.00", CultureInfo.InvariantCulture));
                builder.AppendLine("ROOM OBJECTS:");
                builder.Append(context.Room != null ? context.Room.ToPromptSummary() : "(none)\n");

                if (context.Memories != null && context.Memories.Count > 0)
                {
                    builder.AppendLine("RELEVANT MEMORY:");
                    for (var i = 0; i < context.Memories.Count; i++)
                    {
                        builder.Append("- ").AppendLine(context.Memories[i].Text);
                    }
                }
            }

            builder.Append("USER MESSAGE: ").AppendLine((userMessage ?? string.Empty).Trim());
            builder.Append("JSON:");
            return builder.ToString();
        }

        private static string Vector(UnityEngine.Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.00},{1:0.00},{2:0.00})",
                value.x,
                value.y,
                value.z);
        }
    }
}
