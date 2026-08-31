using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ARC.Agents.Common;
using ARC.Agents.Models;
using ARC.Agents.Prompts;
using ARC.Domain.ValueObjects;
using ARC.Knowledge.Retrieval;
using ARC.Tools.Knowledge;
using ARC.Tools.Notice;

namespace ARC.Agents.A3NoticeDecisioning;

/// <summary>A3 — notice recommendation. DecideNotice is authoritative. Does not approve G1.</summary>
public sealed class NoticeDecisioningAgent
{
    public const string Name = "A3-NoticeDecisioning";

    private readonly NoticeDecisionTool _notice;
    private readonly KnowledgeRetrievalTool _knowledge;
    private readonly ILogger<NoticeDecisioningAgent> _logger;

    public AIAgent Agent { get; }

    public NoticeDecisioningAgent(
        IChatClient chatClient,
        NoticeDecisionTool notice,
        KnowledgeRetrievalTool knowledge,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        _notice = notice;
        _knowledge = knowledge;
        _logger = loggerFactory.CreateLogger<NoticeDecisioningAgent>();
        Agent = ArcAgentFactory.Create(
            chatClient,
            Name,
            "Recommends Issue, Hold, or Reconcile from deterministic rules. Does not approve G1.",
            AgentPrompts.A3,
            [
                AIFunctionFactory.Create(
                    _notice.Decide,
                    new AIFunctionFactoryOptions
                    {
                        Name = NoticeDecisionTool.Name,
                        Description = "Authoritative notice decision from rules R1/R5/R6. The model must not change Issue, Hold, or Reconcile."
                    }),
                AIFunctionFactory.Create(
                    _knowledge.SearchDocumentsAsync,
                    new AIFunctionFactoryOptions
                    {
                        Name = KnowledgeRetrievalTool.SearchName,
                        Description = "Hybrid document search with provenance. Does not decide notices."
                    }),
                AIFunctionFactory.Create(
                    _knowledge.TraverseGraphAsync,
                    new AIFunctionFactoryOptions
                    {
                        Name = KnowledgeRetrievalTool.GraphName,
                        Description = "Dealer 360 graph traversal with provenance. Does not decide notices."
                    })
            ],
            loggerFactory,
            services);
    }

    public Task<NoticeDecisioningAgentResult> RunAsync(NoticeDecisioningAgentRequest request, CancellationToken cancellationToken)
        => AgentRunGuard.ExecuteAsync(Name, _logger, request.Context, async () =>
        {
            var citations = new List<Citation>();
            RetrievalResult? retrieval = null;

            var nodes = await _knowledge.TraverseGraphAsync(
                new TraverseGraphRequest(request.Dealer.Urn.Value, request.Context.CorrelationId),
                cancellationToken);
            citations.AddRange(nodes.Select(n => new Citation(n.Provenance.DocumentId, n.Label)));

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                retrieval = await _knowledge.SearchDocumentsAsync(
                    new SearchDocumentsRequest(
                        request.SearchText,
                        request.Dealer.Urn.Value,
                        request.Dealer.Region,
                        DocumentCategory: null,
                        RequiredVersion: null,
                        request.Context.CorrelationId),
                    cancellationToken);
                citations.AddRange(retrieval.Sources.Select(s => new Citation(s.Reference.DocumentId, s.Title)));
            }

            var verdict = _notice.Decide(new NoticeDecisionRequest(
                request.Dealer,
                request.Exposure,
                request.Context.AsOf,
                request.OpenDispute,
                request.ActivePromiseToPay,
                citations,
                request.Context.CorrelationId));

            var explanation = await AgentNarration.ExplainAsync(
                Agent,
                Name,
                new
                {
                    dealerUrn = request.Dealer.Urn.Value,
                    decision = verdict.Decision.ToString(),
                    verdict.RequiresDepotManagerGate,
                    netRecoverableExposure = request.Exposure.NetRecoverableExposure.Amount,
                    rules = verdict.RuleResults.Select(r => new { r.RuleId, r.Passed, r.Message }),
                    citations = verdict.Citations.Select(c => new { c.SourceId, c.Description }),
                    humanGate = verdict.RequiresDepotManagerGate ? "G1 Depot Manager — agent cannot approve" : "none"
                },
                _logger,
                cancellationToken);

            return new NoticeDecisioningAgentResult(verdict, retrieval, explanation);
        });
}
