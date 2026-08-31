using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.Agent;

namespace Aigf.Companion.AI
{
    public interface ILocalLLM
    {
        bool IsReady { get; }

        Task<AgentReply> GenerateAsync(
            string userMessage,
            AgentContext context,
            CancellationToken cancellationToken = default);
    }
}
