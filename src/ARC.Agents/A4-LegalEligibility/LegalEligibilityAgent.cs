using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ARC.Agents.Common;
using ARC.Agents.Models;
using ARC.Agents.Prompts;
using ARC.Tools.Legal;

namespace ARC.Agents.A4LegalEligibility;

/// <summary>A4 — Section 138 eligibility and limitation clock. Does not approve G3.</summary>
public sealed class LegalEligibilityAgent
{
    public const string Name = "A4-LegalEligibility";

    private readonly LegalEligibilityTool _tool;
    private readonly ILogger<LegalEligibilityAgent> _logger;

    public AIAgent Agent { get; }

    public LegalEligibilityAgent(
        IChatClient chatClient,
        LegalEligibilityTool tool,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        _tool = tool;
        _logger = loggerFactory.CreateLogger<LegalEligibilityAgent>();
        Agent = ArcAgentFactory.Create(
            chatClient,
            Name,
            "Evaluates Section 138 eligibility and statutory clock via tools. Does not approve G3.",
            AgentPrompts.A4,
            [
                AIFunctionFactory.Create(
                    _tool.CheckSection138EligibilityAsync,
                    new AIFunctionFactoryOptions
                    {
                        Name = LegalEligibilityTool.Name,
                        Description = "Authoritative Section 138 eligibility from rules R2/R5. The model must not change Eligible."
                    }),
                AIFunctionFactory.Create(
                    _tool.GetLimitationClockAsync,
                    new AIFunctionFactoryOptions
                    {
                        Name = LegalEligibilityTool.ClockToolName,
                        Description = "Authoritative statutory dates and remaining days. The model must not calculate dates."
                    })
            ],
            loggerFactory,
            services);
    }

    public Task<LegalEligibilityAgentResult> RunAsync(LegalEligibilityAgentRequest request, CancellationToken cancellationToken)
        => AgentRunGuard.ExecuteAsync(Name, _logger, request.Context, async () =>
        {
            var facts = await _tool.CheckSection138EligibilityAsync(
                new LegalEligibilityRequest(
                    request.DealerUrn,
                    request.Context.AsOf,
                    request.Exposure,
                    request.DemandNotice,
                    request.Context.CycleId,
                    request.Context.CorrelationId),
                cancellationToken);

            var explanation = await AgentNarration.ExplainAsync(
                Agent,
                Name,
                new
                {
                    request.DealerUrn,
                    eligible = facts.Eligibility.Eligible,
                    facts.Eligibility.BlockReason,
                    clockStatus = facts.Clock?.Status.ToString(),
                    facts.Clock?.NoticeByDate,
                    facts.Clock?.CureWindowEnds,
                    facts.Clock?.FileByDate,
                    facts.Clock?.DaysRemaining,
                    alerts = facts.Alerts.Select(a => new { kind = a.Kind.ToString(), a.DaysRemaining, a.Deadline }),
                    selectedCheque = facts.SelectedCheque?.ChequeNumber,
                    humanGate = "G3 Legal progression — agent cannot approve"
                },
                _logger,
                cancellationToken);

            return new LegalEligibilityAgentResult(facts, explanation);
        });
}
