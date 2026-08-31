using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ARC.Agents.Common;
using ARC.Agents.Models;
using ARC.Agents.Prompts;
using ARC.Knowledge.Retrieval;
using ARC.Tools.Insights;
using ARC.Tools.Knowledge;

namespace ARC.Agents.A8SupervisoryInsight;

/// <summary>A8 — exception queue and optional NLQ over tool facts. Not a BI platform.</summary>
public sealed class SupervisoryInsightAgent
{
    public const string Name = "A8-SupervisoryInsight";

    private readonly SupervisoryInsightTool _insights;
    private readonly KnowledgeRetrievalTool _knowledge;
    private readonly ILogger<SupervisoryInsightAgent> _logger;

    public AIAgent Agent { get; }

    public SupervisoryInsightAgent(
        IChatClient chatClient,
        SupervisoryInsightTool insights,
        KnowledgeRetrievalTool knowledge,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        _insights = insights;
        _knowledge = knowledge;
        _logger = loggerFactory.CreateLogger<SupervisoryInsightAgent>();
        Agent = ArcAgentFactory.Create(
            chatClient,
            Name,
            "Returns the ARC exception queue and explains it. Does not invent analytics.",
            AgentPrompts.A8,
            [
                AIFunctionFactory.Create(
                    _insights.GetAsync,
                    new AIFunctionFactoryOptions
                    {
                        Name = SupervisoryInsightTool.Name,
                        Description = "Authoritative exception queue from cycle/dealer state. Not a BI cube."
                    }),
                AIFunctionFactory.Create(
                    _knowledge.SearchDocumentsAsync,
                    new AIFunctionFactoryOptions
                    {
                        Name = KnowledgeRetrievalTool.SearchName,
                        Description = "Optional policy/document search with provenance for NLQ context."
                    })
            ],
            loggerFactory,
            services);
    }

    public Task<SupervisoryInsightAgentResult> RunAsync(SupervisoryInsightAgentRequest request, CancellationToken cancellationToken)
        => AgentRunGuard.ExecuteAsync(Name, _logger, request.Context, async () =>
        {
            var insights = await _insights.GetAsync(
                new SupervisoryInsightRequest(
                    request.CycleId,
                    request.Context.AsOf,
                    request.Region,
                    request.DealerUrn,
                    request.Context.CorrelationId,
                    request.PromisesToPay),
                cancellationToken);

            RetrievalResult? retrieval = null;
            if (!string.IsNullOrWhiteSpace(request.NaturalLanguageQuestion))
            {
                retrieval = await _knowledge.SearchDocumentsAsync(
                    new SearchDocumentsRequest(
                        request.NaturalLanguageQuestion,
                        request.DealerUrn,
                        request.Region,
                        DocumentCategory: null,
                        RequiredVersion: null,
                        request.Context.CorrelationId),
                    cancellationToken);
            }

            var explanation = await AgentNarration.ExplainAsync(
                Agent,
                Name,
                new
                {
                    question = request.NaturalLanguageQuestion,
                    exceptions = insights.Exceptions.Select(e => new { kind = e.Kind.ToString(), e.DealerUrn, e.Detail }),
                    dealerCount = insights.Dealers.Count,
                    sources = retrieval?.Sources.Select(s => new { s.Reference.DocumentId, s.Title, s.Reference.SourceSystem })
                },
                _logger,
                cancellationToken);

            return new SupervisoryInsightAgentResult(insights, retrieval, explanation);
        });
}
