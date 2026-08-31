using Microsoft.AspNetCore.Mvc;
using ARC.Agents.A8SupervisoryInsight;
using ARC.Agents.Context;
using ARC.Agents.Models;
using ARC.Api.Auth;
using ARC.Api.DTOs;
using ARC.Domain.ValueObjects;

namespace ARC.Api.Controllers;

[ApiController]
[Route("v1/insights")]
public sealed class InsightsController : ControllerBase
{
    private readonly SupervisoryInsightAgent _a8;

    public InsightsController(SupervisoryInsightAgent a8) => _a8 = a8;

    [HttpGet("exceptions")]
    public async Task<IActionResult> Exceptions(
        [FromQuery] string cycleId,
        [FromQuery] string? dealerUrn,
        [FromQuery] string? region,
        CancellationToken cancellationToken)
    {
        var actor = ArcActorHttp.GetRequired(HttpContext);
        if (!TryBuildRequest(actor, cycleId, dealerUrn, region, question: null, out var request, out var error))
            return BadRequest(new { error });
        var result = await _a8.RunAsync(request, cancellationToken);
        return Ok(new
        {
            exceptions = result.Insights.Exceptions,
            dealers = result.Insights.Dealers.Select(d => new { d.DealerUrn, d.Status, d.WaitingGate }),
            result.Explanation
        });
    }

    [HttpPost("nlq")]
    public async Task<IActionResult> Nlq([FromBody] NlqRequest body, CancellationToken cancellationToken)
    {
        var actor = ArcActorHttp.GetRequired(HttpContext);
        if (!TryBuildRequest(actor, body.CycleId, body.DealerUrn, body.Region, body.Question, out var request, out var error))
            return BadRequest(new { error });
        var result = await _a8.RunAsync(request, cancellationToken);
        return Ok(new
        {
            exceptions = result.Insights.Exceptions,
            sources = result.Retrieval?.Sources.Select(s => new
            {
                s.Reference.DocumentId,
                s.Title,
                s.Reference.SourceSystem,
                s.Snippet
            }),
            result.Explanation
        });
    }

    private static bool TryBuildRequest(
        ArcActor actor,
        string cycleId,
        string? dealerUrn,
        string? region,
        string? question,
        out SupervisoryInsightAgentRequest request,
        out string? error)
    {
        var scopedRegion = GateAccess.ForcedRegion(actor) ?? region;
        if (string.IsNullOrWhiteSpace(dealerUrn) && string.IsNullOrWhiteSpace(scopedRegion))
        {
            request = null!;
            error = "Provide dealerUrn or region. TSI region is always taken from the authenticated actor.";
            return false;
        }

        request = new SupervisoryInsightAgentRequest(
            cycleId,
            scopedRegion,
            dealerUrn,
            question,
            PromisesToPay: null,
            new AgentContext(DateOnly.FromDateTime(DateTime.UtcNow), cycleId, CorrelationId.New().Value, dealerUrn));
        error = null;
        return true;
    }
}
