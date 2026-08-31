using System;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Agent;
using Aigf.Companion.Room;

namespace Aigf.Companion.AI
{
    public sealed class MockLocalLLM : ILocalLLM
    {
        public bool IsReady => true;

        public Task<AgentReply> GenerateAsync(
            string userMessage,
            AgentContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = Normalize(userMessage);
            var russian = ContainsCyrillic(text);
            AgentReply reply;

            if (ContainsAny(text, "останов", "стоп", "stop", "halt"))
            {
                reply = Reply(russian, "Хорошо, остановилась.", "Okay, stopping.", new AgentAction(AgentActionTypes.Stop));
            }
            else if (ContainsAny(text, "встан", "stand up", "get up"))
            {
                reply = Reply(russian, "Хорошо, встаю.", "Okay, standing up.", new AgentAction(AgentActionTypes.Stand));
            }
            else if (ContainsAny(text, "за мной", "следуй", "follow me", "follow"))
            {
                reply = Reply(russian, "Хорошо, иду за тобой.", "Okay, I'll follow you.", new AgentAction(AgentActionTypes.FollowUser));
            }
            else if (ContainsAny(text, "сяд", "садис", "sit"))
            {
                var seat = FindSeat(text, context != null ? context.Room : null);
                reply = seat == null
                    ? Reply(russian, "Я не вижу подходящего места.", "I cannot find a safe seat.")
                    : Reply(
                        russian,
                        "Хорошо, сейчас сяду.",
                        "Okay, I'll sit there.",
                        new AgentAction(AgentActionTypes.WalkTo, seat.Id),
                        new AgentAction(AgentActionTypes.Sit, seat.Id));
            }
            else if (ContainsAny(text, "помаш", "wave"))
            {
                reply = Reply(russian, "Привет!", "Hi!", new AgentAction(AgentActionTypes.Wave));
            }
            else if (ContainsAny(text, "посмотр", "смотри на меня", "look at me"))
            {
                reply = Reply(russian, "Смотрю на тебя.", "I'm looking at you.", new AgentAction(AgentActionTypes.LookAtUser));
            }
            else if (ContainsAny(text, "иди ко мне", "подойди ко мне", "подойди", "come here", "come to me"))
            {
                reply = Reply(russian, "Хорошо, иду к тебе.", "Okay, I'm coming closer.", new AgentAction(AgentActionTypes.WalkToUser));
            }
            else if (TryFindMentionedNode(text, context != null ? context.Room : null, out var node))
            {
                reply = Reply(
                    russian,
                    "Хорошо, иду туда.",
                    "Okay, I'll go there.",
                    new AgentAction(AgentActionTypes.WalkTo, node.Id));
            }
            else
            {
                reply = Reply(russian, "Я рядом. Скажи, что мне сделать.", "I'm here. Tell me what to do.");
            }

            return Task.FromResult(reply);
        }

        private static AgentReply Reply(bool russian, string ru, string en, params AgentAction[] actions)
        {
            return new AgentReply(russian ? ru : en, "warm", actions);
        }

        private static RoomNode FindSeat(string text, RoomGraph room)
        {
            if (room == null)
            {
                return null;
            }

            if (ContainsAny(text, "chair", "стул", "кресл"))
            {
                return room.FindFirst(RoomNodeType.Chair);
            }

            return room.FindFirst(RoomNodeType.Sofa) ?? room.FindFirst(RoomNodeType.Chair);
        }

        private static bool TryFindMentionedNode(string text, RoomGraph room, out RoomNode node)
        {
            node = null;
            if (room == null)
            {
                return false;
            }

            for (var i = 0; i < room.Nodes.Count; i++)
            {
                var candidate = room.Nodes[i];
                if (candidate != null && text.IndexOf(candidate.Id, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    node = candidate;
                    return true;
                }
            }

            if (ContainsAny(text, "диван", "sofa", "couch")) node = room.FindFirst(RoomNodeType.Sofa);
            else if (ContainsAny(text, "стул", "chair")) node = room.FindFirst(RoomNodeType.Chair);
            else if (ContainsAny(text, "стол", "table")) node = room.FindFirst(RoomNodeType.Table);
            return node != null;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');
        }

        private static bool ContainsAny(string source, params string[] fragments)
        {
            for (var i = 0; i < fragments.Length; i++)
            {
                if (source.IndexOf(fragments[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCyrillic(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] >= '\u0400' && value[i] <= '\u04ff')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
