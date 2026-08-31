namespace Aigf.Companion.Agent
{
    public sealed class ActionResult
    {
        public bool Succeeded { get; }
        public string Message { get; }

        private ActionResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public static ActionResult Success(string message = "") => new ActionResult(true, message);
        public static ActionResult Failure(string message) => new ActionResult(false, message);
    }
}
