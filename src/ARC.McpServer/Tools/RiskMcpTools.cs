using System.ComponentModel;
using ARC.Tools.Reconciliation;
using ARC.Tools.Risk;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class RiskMcpTools
{
    private readonly ReconciliationTool _reconciliation;
    private readonly RiskPrioritisationTool _tool;

    public RiskMcpTools(ReconciliationTool reconciliation, RiskPrioritisationTool tool)
    {
        _reconciliation = reconciliation;
        _tool = tool;
    }

    [McpServerTool, Description("Prioritise recovery tier and score for a dealer.")]
    public async Task<string> PrioritiseRecovery(
        [Description("Dealer URN")] string dealerUrn,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Whether a security cheque has bounced")] bool hasBouncedSecurityCheque = false,
        [Description("Days since demand notice was served")] int? daysSinceDemandNotice = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var exposure = await _reconciliation.ComputeNetExposureAsync(
            new ComputeNetExposureRequest(dealerUrn, DateOnly.Parse(asOf), null, correlationId),
            cancellationToken);

        var result = _tool.Prioritise(new RiskPrioritisationRequest(
            exposure.Exposure,
            hasBouncedSecurityCheque,
            daysSinceDemandNotice,
            correlationId));

        return McpJson.Serialize(result);
    }
}
