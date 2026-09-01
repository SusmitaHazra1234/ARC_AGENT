using System.ComponentModel;
using ARC.Tools.Insights;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class InsightsMcpTools
{
    private readonly SupervisoryInsightTool _tool;

    public InsightsMcpTools(SupervisoryInsightTool tool) => _tool = tool;

    [McpServerTool, Description("Get supervisory exceptions and dealer insight queue.")]
    public async Task<string> GetSupervisoryInsights(
        [Description("Cycle id")] string cycleId,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Optional region filter")] string? region = null,
        [Description("Optional dealer URN filter")] string? dealerUrn = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tool.GetAsync(
            new SupervisoryInsightRequest(cycleId, DateOnly.Parse(asOf), region, dealerUrn, correlationId, PromisesToPay: null),
            cancellationToken);

        return McpJson.Serialize(result);
    }
}
