using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aigf.Companion.Agent;
using Aigf.Companion.Room;
using UnityEngine;

namespace Aigf.Companion.AI
{
    public static class AgentJson
    {
        public const int DefaultMaxActions = 5;
        private const int MaxJsonCharacters = 16384;
        private static readonly Regex PropertyRegex = new Regex(
            "\\\"(?<key>(?:\\\\.|[^\\\"\\\\])*)\\\"\\s*:",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> AllowedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "speech", "emotion", "actions", "type", "target", "distance", "animation"
        };

        [Serializable]
        private sealed class ReplyDto
        {
            public string speech;
            public string emotion;
            public ActionDto[] actions;
        }

        [Serializable]
        private sealed class ActionDto
        {
            public string type;
            public string target;
            public float distance;
            public string animation;
        }

        public static bool TryParse(
            string json,
            RoomGraph room,
            out AgentReply reply,
            out string error,
            int maxActions = DefaultMaxActions)
        {
            reply = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Model returned empty JSON.";
                return false;
            }

            json = json.Trim();
            if (json.Length > MaxJsonCharacters || json[0] != '{' || json[json.Length - 1] != '}')
            {
                error = "Model output is not a bounded JSON object.";
                return false;
            }

            foreach (Match match in PropertyRegex.Matches(json))
            {
                if (!AllowedKeys.Contains(match.Groups["key"].Value))
                {
                    error = $"Unknown JSON property '{match.Groups["key"].Value}'.";
                    return false;
                }
            }

            ReplyDto dto;
            try
            {
                dto = JsonUtility.FromJson<ReplyDto>(json);
            }
            catch (Exception exception)
            {
                error = $"Malformed action JSON: {exception.Message}";
                return false;
            }

            if (dto == null)
            {
                error = "Malformed action JSON.";
                return false;
            }

            var actionDtos = dto.actions ?? Array.Empty<ActionDto>();
            maxActions = Mathf.Clamp(maxActions, 1, DefaultMaxActions);
            if (actionDtos.Length > maxActions)
            {
                error = $"Reply contains {actionDtos.Length} actions; maximum is {maxActions}.";
                return false;
            }

            var parsed = new AgentReply
            {
                Speech = (dto.speech ?? string.Empty).Trim(),
                Emotion = string.IsNullOrWhiteSpace(dto.emotion) ? "neutral" : dto.emotion.Trim(),
                Actions = new List<AgentAction>(actionDtos.Length)
            };

            if (parsed.Speech.Length > 512 || parsed.Emotion.Length > 32)
            {
                error = "Reply text exceeds the safe length limit.";
                return false;
            }

            for (var i = 0; i < actionDtos.Length; i++)
            {
                var source = actionDtos[i];
                if (source == null)
                {
                    error = $"Action {i} is null.";
                    return false;
                }

                var action = new AgentAction(source.type, source.target, source.distance, source.animation);
                if (!AgentSafety.ValidateAction(action, room, out error))
                {
                    error = $"Action {i}: {error}";
                    return false;
                }

                parsed.Actions.Add(action);
            }

            reply = parsed;
            return true;
        }

        public static bool ValidateReply(
            AgentReply reply,
            RoomGraph room,
            out string error,
            int maxActions = DefaultMaxActions)
        {
            if (reply == null)
            {
                error = "Reply is null.";
                return false;
            }

            reply.Speech = (reply.Speech ?? string.Empty).Trim();
            reply.Emotion = string.IsNullOrWhiteSpace(reply.Emotion) ? "neutral" : reply.Emotion.Trim();
            reply.Actions = reply.Actions ?? new List<AgentAction>();

            if (reply.Actions.Count > Mathf.Clamp(maxActions, 1, DefaultMaxActions))
            {
                error = "Reply exceeds the action limit.";
                return false;
            }

            for (var i = 0; i < reply.Actions.Count; i++)
            {
                if (!AgentSafety.ValidateAction(reply.Actions[i], room, out error))
                {
                    error = $"Action {i}: {error}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static string Serialize(AgentReply reply, bool pretty = false)
        {
            if (reply == null)
            {
                return string.Empty;
            }

            var actions = reply.Actions ?? new List<AgentAction>();
            var dto = new ReplyDto
            {
                speech = reply.Speech ?? string.Empty,
                emotion = reply.Emotion ?? "neutral",
                actions = new ActionDto[actions.Count]
            };

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i] ?? new AgentAction();
                dto.actions[i] = new ActionDto
                {
                    type = action.Type ?? string.Empty,
                    target = action.Target ?? string.Empty,
                    distance = action.Distance,
                    animation = action.Animation ?? string.Empty
                };
            }

            return JsonUtility.ToJson(dto, pretty);
        }
    }
}
