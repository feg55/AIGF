using System;
using System.Collections.Generic;

namespace Aigf.Companion.AI
{
    [Serializable]
    public sealed class AgentReply
    {
        public string Speech;
        public string Emotion;
        public List<AgentAction> Actions;

        public AgentReply()
        {
            Speech = string.Empty;
            Emotion = "neutral";
            Actions = new List<AgentAction>();
        }

        public AgentReply(string speech, string emotion, params AgentAction[] actions)
        {
            Speech = speech ?? string.Empty;
            Emotion = string.IsNullOrWhiteSpace(emotion) ? "neutral" : emotion;
            Actions = actions == null ? new List<AgentAction>() : new List<AgentAction>(actions);
        }
    }
}
