using Microsoft.Extensions.Logging;
using ARC.Agents.Context;
using ARC.Agents.Exceptions;
using ARC.Tools.Exceptions;

namespace ARC.Agents.Common;

internal static class AgentRunGuard
{
    public static async Task<T> ExecuteAsync<T>(
        string agentName,
        ILogger logger,
        AgentContext? context,
        Func<Task<T>> action)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var result = await action();
            logger.LogInformation(
                "Agent {Agent} cycle {CycleId} correlation {CorrelationId} dealer {DealerUrn} succeeded durationMs {DurationMs}",
                agentName, context?.CycleId, context?.CorrelationId, context?.DealerUrn,
                (DateTimeOffset.UtcNow - started).TotalMilliseconds);
            return result;
        }
        catch (ToolException ex)
        {
            logger.LogError(ex, "Agent {Agent} tool {Tool} failed cycle {CycleId} correlation {CorrelationId} dealer {DealerUrn} durationMs {DurationMs}",
                agentName, ex.ToolName, context?.CycleId, context?.CorrelationId, context?.DealerUrn,
                (DateTimeOffset.UtcNow - started).TotalMilliseconds);
            throw new AgentException(agentName, ex.Message, ex);
        }
    }
}
