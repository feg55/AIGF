using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aigf.Companion.AI;

namespace Aigf.Companion.Agent
{
    public static class ActionSequenceRunner
    {
        public static async Task<ActionResult> RunAsync(
            IReadOnlyList<AgentAction> actions,
            Func<AgentAction, CancellationToken, Task<ActionResult>> execute,
            CancellationToken cancellationToken)
        {
            if (actions == null || actions.Count == 0)
            {
                return ActionResult.Success();
            }

            if (execute == null)
            {
                return ActionResult.Failure("Action handler is missing.");
            }

            for (var i = 0; i < actions.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await execute(actions[i], cancellationToken);
                if (result == null || !result.Succeeded)
                {
                    return result ?? ActionResult.Failure("Action handler returned no result.");
                }
            }

            return ActionResult.Success();
        }
    }
}
