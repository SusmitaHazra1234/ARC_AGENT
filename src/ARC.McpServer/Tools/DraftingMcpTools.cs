using System.ComponentModel;
using ARC.Data.Sql;
using ARC.Domain.ValueObjects;
using ARC.Tools.Drafting;
using ARC.Tools.Legal;
using ARC.Tools.Reconciliation;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class DraftingMcpTools
{
    private readonly IDealerRepository _dealers;
    private readonly IChequeRepository _cheques;
    private readonly ReconciliationTool _reconciliation;
    private readonly LegalEligibilityTool _legal;
    private readonly DraftingVerificationTool _tool;

    public DraftingMcpTools(
        IDealerRepository dealers,
        IChequeRepository cheques,
        ReconciliationTool reconciliation,
        LegalEligibilityTool legal,
        DraftingVerificationTool tool)
    {
        _dealers = dealers;
        _cheques = cheques;
        _reconciliation = reconciliation;
        _legal = legal;
        _tool = tool;
    }

    [McpServerTool, Description("Verify a draft notice against authoritative dealer facts.")]
    public async Task<string> VerifyDraft(
        [Description("Dealer URN")] string dealerUrn,
        [Description("As-of date yyyy-MM-dd")] string asOf,
        [Description("Draft kind: DemandNotice or Section138Notice")] string draftKind,
        [Description("JSON object matching DraftQuotedFields")] string draftFieldsJson,
        [Description("Optional cycle id")] string? cycleId = null,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.Parse(asOf);
        var kind = Enum.Parse<DraftKind>(draftKind, ignoreCase: true);
        var draft = McpJson.Deserialize<DraftQuotedFields>(draftFieldsJson);

        var dealer = await _dealers.GetAsync(new DealerUrn(dealerUrn), cancellationToken)
            ?? throw new InvalidOperationException($"Dealer '{dealerUrn}' was not found.");

        var facts = await _reconciliation.ComputeNetExposureAsync(
            new ComputeNetExposureRequest(dealerUrn, asOfDate, cycleId, correlationId),
            cancellationToken);

        var cheques = await _cheques.ListChequesAsync(dealer.Urn, cancellationToken);
        var memos = await _cheques.ListReturnMemosAsync(dealer.Urn, cancellationToken);
        var cheque = cheques.FirstOrDefault();
        var memo = cheque is null
            ? null
            : memos.FirstOrDefault(m => string.Equals(m.ChequeNumber, cheque.ChequeNumber, StringComparison.OrdinalIgnoreCase));

        var clockResult = memo is null
            ? null
            : await _legal.GetLimitationClockAsync(
                new GetLimitationClockRequest(dealerUrn, asOfDate, DemandNotice: null, cycleId, correlationId),
                cancellationToken);

        var result = _tool.Verify(new DraftingVerificationRequest(
            draft,
            kind,
            facts.Exposure,
            dealer,
            cheque,
            memo,
            clockResult?.Clock,
            cycleId,
            correlationId));

        return McpJson.Serialize(result);
    }
}
