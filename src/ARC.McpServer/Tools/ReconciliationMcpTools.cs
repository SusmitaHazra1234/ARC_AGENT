using System.ComponentModel;
using ARC.Tools.Reconciliation;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class ReconciliationMcpTools
{
    private readonly ReconciliationTool _tool;

    public ReconciliationMcpTools(ReconciliationTool tool) => _tool = tool;

    [McpServerTool, Description("Compute net recoverable exposure for a dealer.")]
    public async Task<string> ComputeNetExposure(
        [Description("Dealer URN")] string dealerUrn,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        CancellationToken cancellationToken)
    {
        var result = await _tool.ComputeNetExposureAsync(
            new ComputeNetExposureRequest(dealerUrn, DateOnly.Parse(asOf), null, null),
            cancellationToken);

        return McpJson.Serialize(result);
    }
}
