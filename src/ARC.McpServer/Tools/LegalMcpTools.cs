using System.ComponentModel;
using ARC.Tools.Legal;
using ARC.Tools.Reconciliation;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class LegalMcpTools
{
    private readonly ReconciliationTool _reconciliation;
    private readonly LegalEligibilityTool _tool;

    public LegalMcpTools(ReconciliationTool reconciliation, LegalEligibilityTool tool)
    {
        _reconciliation = reconciliation;
        _tool = tool;
    }

    [McpServerTool, Description("Check Section 138 legal eligibility and limitation clock alerts.")]
    public async Task<string> CheckSection138Eligibility(
        [Description("Dealer URN")] string dealerUrn,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Optional cycle id")] string? cycleId = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.Parse(asOf);
        var facts = await _reconciliation.ComputeNetExposureAsync(
            new ComputeNetExposureRequest(dealerUrn, asOfDate, cycleId, correlationId),
            cancellationToken);

        var result = await _tool.CheckSection138EligibilityAsync(
            new LegalEligibilityRequest(dealerUrn, asOfDate, facts.Exposure, DemandNotice: null, cycleId, correlationId),
            cancellationToken);

        return McpJson.Serialize(result);
    }

    [McpServerTool, Description("Get the Section 138 limitation clock for a dealer.")]
    public async Task<string> GetLimitationClock(
        [Description("Dealer URN")] string dealerUrn,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Optional cycle id")] string? cycleId = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tool.GetLimitationClockAsync(
            new GetLimitationClockRequest(dealerUrn, DateOnly.Parse(asOf), DemandNotice: null, cycleId, correlationId),
            cancellationToken);

        return McpJson.Serialize(result);
    }
}
