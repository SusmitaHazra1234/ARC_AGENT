using System.ComponentModel;
using ARC.Data.Sql;
using ARC.Domain.ValueObjects;
using ARC.Tools.Notice;
using ARC.Tools.Reconciliation;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class NoticeMcpTools
{
    private readonly IDealerRepository _dealers;
    private readonly ReconciliationTool _reconciliation;
    private readonly NoticeDecisionTool _tool;

    public NoticeMcpTools(
        IDealerRepository dealers,
        ReconciliationTool reconciliation,
        NoticeDecisionTool tool)
    {
        _dealers = dealers;
        _reconciliation = reconciliation;
        _tool = tool;
    }

    [McpServerTool, Description("Decide whether to issue, hold, or reconcile a demand notice.")]
    public async Task<string> DecideNotice(
        [Description("Dealer URN")] string dealerUrn,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.Parse(asOf);
        var dealer = await _dealers.GetAsync(new DealerUrn(dealerUrn), cancellationToken)
            ?? throw new InvalidOperationException($"Dealer '{dealerUrn}' was not found.");

        var facts = await _reconciliation.ComputeNetExposureAsync(
            new ComputeNetExposureRequest(dealerUrn, asOfDate, null, correlationId),
            cancellationToken);

        var verdict = _tool.Decide(new NoticeDecisionRequest(
            dealer,
            facts.Exposure,
            asOfDate,
            OpenDispute: null,
            ActivePromiseToPay: null,
            Citations: null,
            correlationId));

        return McpJson.Serialize(verdict);
    }
}
