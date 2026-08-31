using ARC.Agents.Context;
using ARC.Domain.Entities;
using ARC.Tools.Insights;
using ARC.Tools.Knowledge;
using ARC.Knowledge.Retrieval;

namespace ARC.Agents.Models;

public sealed record SupervisoryInsightAgentRequest(
    string CycleId,
    string? Region,
    string? DealerUrn,
    string? NaturalLanguageQuestion,
    IReadOnlyList<PromiseToPay>? PromisesToPay,
    AgentContext Context);

public sealed record SupervisoryInsightAgentResult(
    SupervisoryInsightResult Insights,
    RetrievalResult? Retrieval,
    string? Explanation);
