using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ARC.Agents.Common;
using ARC.Agents.Models;
using ARC.Agents.Prompts;
using ARC.Tools.Risk;

namespace ARC.Agents.A2RiskPrioritisation;

/// <summary>A2 — ranking and recovery tier. The tool score/tier cannot be overridden by the model.</summary>
public sealed class RiskPrioritisationAgent
{
    public const string Name = "A2-RiskPrioritisation";

    private readonly RiskPrioritisationTool _tool;
    private readonly ILogger<RiskPrioritisationAgent> _logger;

    public AIAgent Agent { get; }

    public RiskPrioritisationAgent(
        IChatClient chatClient,
        RiskPrioritisationTool tool,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        _tool = tool;
        _logger = loggerFactory.CreateLogger<RiskPrioritisationAgent>();
        Agent = ArcAgentFactory.Create(
            chatClient,
            Name,
            "Ranks dealers and assigns recovery tier. Does not invent risk scores.",
            AgentPrompts.A2,
            [
                AIFunctionFactory.Create(
                    _tool.Prioritise,
                    new AIFunctionFactoryOptions
                    {
                        Name = RiskPrioritisationTool.Name,
                        Description = "Authoritative recovery tier and ranking score. The model must not change the returned tier or score."
                    })
            ],
            loggerFactory,
            services);
    }

    public Task<RiskPrioritisationAgentResult> RunAsync(RiskPrioritisationAgentRequest request, CancellationToken cancellationToken)
        => AgentRunGuard.ExecuteAsync(Name, _logger, request.Context, async () =>
        {
            var assessment = _tool.Prioritise(new RiskPrioritisationRequest(
                request.Exposure,
                request.HasBouncedSecurityCheque,
                request.DaysSinceDemandNotice,
                request.Context.CorrelationId));

            var explanation = await AgentNarration.ExplainAsync(
                Agent,
                Name,
                new
                {
                    dealerUrn = request.Exposure.DealerUrn.Value,
                    netRecoverableExposure = request.Exposure.NetRecoverableExposure.Amount,
                    tier = assessment.Tier.ToString(),
                    score = assessment.Score,
                    request.HasBouncedSecurityCheque,
                    request.DaysSinceDemandNotice,
                    tsiRemarks = request.TsiRemarks
                },
                _logger,
                cancellationToken);

            return new RiskPrioritisationAgentResult(assessment, explanation);
        });
}
