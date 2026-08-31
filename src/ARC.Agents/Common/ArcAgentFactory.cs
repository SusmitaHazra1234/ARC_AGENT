using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ARC.Agents.Common;

internal static class ArcAgentFactory
{
    public static AIAgent Create(
        IChatClient chatClient,
        string name,
        string description,
        string instructions,
        IList<AITool> tools,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
        => chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = name,
                Description = description,
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions,
                    Tools = tools
                }
            },
            loggerFactory,
            services);
}
