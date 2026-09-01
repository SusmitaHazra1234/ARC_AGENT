using System.ComponentModel;
using ARC.Domain.Enums;
using ARC.Tools.Field;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class FieldMcpTools
{
    private readonly FieldOrchestrationTool _tool;

    public FieldMcpTools(FieldOrchestrationTool tool) => _tool = tool;

    [McpServerTool, Description("Plan a field visit task for a dealer.")]
    public async Task<string> PlanVisit(
        [Description("Dealer URN")] string dealerUrn,
        [Description("Recovery tier: Notice, Visit, or Section138")] string tier,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Cycle id")] string cycleId,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tool.PlanVisitAsync(
            new PlanVisitRequest(
                dealerUrn,
                Enum.Parse<RecoveryTier>(tier, ignoreCase: true),
                DateOnly.Parse(asOf),
                cycleId,
                correlationId),
            cancellationToken);

        return McpJson.Serialize(result);
    }

    [McpServerTool, Description("Capture a structured promise-to-pay without confirming it.")]
    public Task<string> CapturePromiseToPay(
        [Description("Dealer URN")] string dealerUrn,
        [Description("Commitment date yyyy-MM-dd")] string commitmentDate,
        [Description("Promised amount")] decimal amount,
        [Description("Whether TSI already confirmed the capture")] bool confirmedByTsi = false,
        [Description("Optional speech confidence score")] decimal? speechConfidence = null,
        [Description("As-of date yyyy-MM-dd")] string? asOf = null,
        [Description("Cycle id")] string? cycleId = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _tool.CapturePromiseToPay(new CapturePromiseToPayRequest(
            dealerUrn,
            DateOnly.Parse(commitmentDate),
            amount,
            confirmedByTsi,
            speechConfidence,
            DateOnly.Parse(asOf ?? commitmentDate),
            cycleId ?? "mcp-cycle",
            correlationId));

        return Task.FromResult(McpJson.Serialize(result));
    }

    [McpServerTool, Description("Check whether a promise-to-pay is broken as of a date.")]
    public Task<string> CheckBrokenPromise(
        [Description("Dealer URN")] string dealerUrn,
        [Description("Commitment date yyyy-MM-dd")] string commitmentDate,
        [Description("Promised amount")] decimal amount,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var promise = new ARC.Domain.Entities.PromiseToPay(
            new ARC.Domain.ValueObjects.DealerUrn(dealerUrn),
            DateOnly.Parse(commitmentDate),
            new ARC.Domain.ValueObjects.Money(amount),
            confirmedByTsi: false);

        var result = _tool.CheckBrokenPromise(new BrokenPromiseCheckRequest(
            promise,
            DateOnly.Parse(asOf),
            correlationId));

        return Task.FromResult(McpJson.Serialize(result));
    }
}
