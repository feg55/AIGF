namespace Aigf.Companion.Agent
{
    public sealed class ConversationTurn
    {
        public string User { get; }
        public string Assistant { get; }

        public ConversationTurn(string user, string assistant)
        {
            User = user ?? string.Empty;
            Assistant = assistant ?? string.Empty;
        }
    }
}
