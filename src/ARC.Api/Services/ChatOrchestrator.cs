using System.Text;
using System.Text.RegularExpressions;
using ARC.Agents.A1Reconciliation;
using ARC.Agents.A2RiskPrioritisation;
using ARC.Agents.A3NoticeDecisioning;
using ARC.Agents.A4LegalEligibility;
using ARC.Agents.A8SupervisoryInsight;
using ARC.Agents.Context;
using ARC.Agents.Models;
using ARC.Api.Auth;
using ARC.Api.DTOs;
using ARC.Data.Sql;
using ARC.Domain.ValueObjects;

namespace ARC.Api.Services;

public sealed class ChatOrchestrator
{
    private static readonly Regex DealerUrnPattern = new(@"dealer:[\w-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ReconciliationAgent _a1;
    private readonly RiskPrioritisationAgent _a2;
    private readonly NoticeDecisioningAgent _a3;
    private readonly LegalEligibilityAgent _a4;
    private readonly SupervisoryInsightAgent _a8;
    private readonly IDealerRepository _dealers;

    public ChatOrchestrator(
        ReconciliationAgent a1,
        RiskPrioritisationAgent a2,
        NoticeDecisioningAgent a3,
        LegalEligibilityAgent a4,
        SupervisoryInsightAgent a8,
        IDealerRepository dealers)
    {
        _a1 = a1;
        _a2 = a2;
        _a3 = a3;
        _a4 = a4;
        _a8 = a8;
        _dealers = dealers;
    }

    public async Task<ChatMessageResponse> HandleAsync(
        ChatMessageRequest request,
        ArcActor actor,
        CancellationToken cancellationToken)
    {
        var message = request.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return Reply("Please type a message.", "ARC Assistant");

        if (IsHelp(message))
            return Reply(BuildHelpText(), "ARC Assistant");

        var dealerUrn = ResolveDealerUrn(message, request.DealerUrn);
        if (dealerUrn is null)
        {
            return Reply(
                "Please include a dealer URN (for example `dealer:s1`) or set one in chat settings.",
                "ARC Assistant");
        }

        var cycleId = string.IsNullOrWhiteSpace(request.CycleId) ? "2026-03-chat" : request.CycleId.Trim();
        var region = GateAccess.ForcedRegion(actor) ?? request.Region;
        var context = new AgentContext(
            DateOnly.FromDateTime(DateTime.UtcNow),
            cycleId,
            CorrelationId.New().Value,
            dealerUrn);

        try
        {
            if (IsIntent(message, "exposure", "reconcile", "net", "ledger", "a1"))
                return await HandleExposureAsync(dealerUrn, context, cancellationToken);

            if (IsIntent(message, "priorit", "tier", "risk", "rank", "a2"))
                return await HandlePrioritisationAsync(dealerUrn, context, cancellationToken);

            if (IsIntent(message, "notice", "issue", "hold", "reconcile decision", "a3"))
                return await HandleNoticeAsync(dealerUrn, context, cancellationToken);

            if (IsIntent(message, "legal", "section 138", "s138", "eligibility", "limitation", "a4"))
                return await HandleLegalAsync(dealerUrn, context, cancellationToken);

            if (IsIntent(message, "insight", "exception", "supervisory", "dashboard", "a8"))
                return await HandleInsightsAsync(cycleId, region, dealerUrn, question: null, context, cancellationToken);

            return await HandleInsightsAsync(cycleId, region, dealerUrn, message, context, cancellationToken);
        }
        catch (Exception ex)
        {
            return Reply($"Something went wrong: {ex.Message}", "ARC Assistant");
        }
    }

    private async Task<ChatMessageResponse> HandleExposureAsync(
        string dealerUrn,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var result = await _a1.RunAsync(new ReconciliationAgentRequest(dealerUrn, context), cancellationToken);
        var e = result.Facts.Exposure;
        var text = new StringBuilder()
            .AppendLine($"Net recoverable exposure for *{dealerUrn}*")
            .AppendLine($"• Gross open AR: {e.GrossOpenAr.Amount:N2} {e.GrossOpenAr.Currency}")
            .AppendLine($"• Credits / rebates / returns: {e.UnappliedCreditNotes.Amount:N2} / {e.AccruedSchemeRebates.Amount:N2} / {e.GoodsReturnInTransit.Amount:N2}")
            .AppendLine($"• *Net exposure: {e.NetRecoverableExposure.Amount:N2} {e.NetRecoverableExposure.Currency}*")
            .AppendLine($"• Status: {e.Status}")
            .AppendLine($"• Ledger lines: {result.Facts.LedgerLineCount}")
            .ToString();

        if (!string.IsNullOrWhiteSpace(result.Explanation))
            text += "\n" + result.Explanation;

        return Reply(text.Trim(), ReconciliationAgent.Name, result.Facts);
    }

    private async Task<ChatMessageResponse> HandlePrioritisationAsync(
        string dealerUrn,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var exposure = await _a1.RunAsync(new ReconciliationAgentRequest(dealerUrn, context), cancellationToken);
        var result = await _a2.RunAsync(new RiskPrioritisationAgentRequest(
            exposure.Facts.Exposure,
            HasBouncedSecurityCheque: false,
            DaysSinceDemandNotice: null,
            TsiRemarks: null,
            context), cancellationToken);

        var text = new StringBuilder()
            .AppendLine($"Recovery tier for *{dealerUrn}*")
            .AppendLine($"• Tier: *{result.Assessment.Tier}*")
            .AppendLine($"• Score: {(result.Assessment.Score?.ToString("N2") ?? "n/a")}")
            .ToString();

        if (!string.IsNullOrWhiteSpace(result.Explanation))
            text += "\n" + result.Explanation;

        return Reply(text.Trim(), RiskPrioritisationAgent.Name, result.Assessment);
    }

    private async Task<ChatMessageResponse> HandleNoticeAsync(
        string dealerUrn,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var dealer = await _dealers.GetAsync(new DealerUrn(dealerUrn), cancellationToken)
            ?? throw new InvalidOperationException($"Dealer '{dealerUrn}' was not found.");

        var exposure = await _a1.RunAsync(new ReconciliationAgentRequest(dealerUrn, context), cancellationToken);
        var result = await _a3.RunAsync(new NoticeDecisioningAgentRequest(
            dealer,
            exposure.Facts.Exposure,
            OpenDispute: null,
            ActivePromiseToPay: null,
            SearchText: null,
            context), cancellationToken);
        var verdict = result.Verdict;
        var text = new StringBuilder()
            .AppendLine($"Notice recommendation for *{dealerUrn}*")
            .AppendLine($"• Decision: *{verdict.Decision}*")
            .AppendLine($"• Requires G1: {(verdict.RequiresDepotManagerGate ? "Yes" : "No")}")
            .ToString();

        if (!string.IsNullOrWhiteSpace(result.Explanation))
            text += "\n" + result.Explanation;

        return Reply(text.Trim(), NoticeDecisioningAgent.Name, verdict);
    }

    private async Task<ChatMessageResponse> HandleLegalAsync(
        string dealerUrn,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var exposure = await _a1.RunAsync(new ReconciliationAgentRequest(dealerUrn, context), cancellationToken);
        var result = await _a4.RunAsync(new LegalEligibilityAgentRequest(
            dealerUrn,
            exposure.Facts.Exposure,
            DemandNotice: null,
            context), cancellationToken);

        var text = new StringBuilder()
            .AppendLine($"Section 138 eligibility for *{dealerUrn}*")
            .AppendLine($"• Eligible: *{(result.Facts.Eligibility.Eligible ? "Yes" : "No")}*")
            .AppendLine($"• Clock: {result.Facts.Clock?.Status.ToString() ?? "n/a"}")
            .AppendLine($"• Alerts: {result.Facts.Alerts.Count}")
            .ToString();

        if (!result.Facts.Eligibility.Eligible && !string.IsNullOrWhiteSpace(result.Facts.Eligibility.BlockReason))
            text += "• Reason: " + result.Facts.Eligibility.BlockReason;

        if (!string.IsNullOrWhiteSpace(result.Explanation))
            text += "\n" + result.Explanation;

        return Reply(text.Trim(), LegalEligibilityAgent.Name, result.Facts);
    }

    private async Task<ChatMessageResponse> HandleInsightsAsync(
        string cycleId,
        string? region,
        string dealerUrn,
        string? question,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var result = await _a8.RunAsync(new SupervisoryInsightAgentRequest(
            cycleId,
            region,
            dealerUrn,
            question,
            PromisesToPay: null,
            context), cancellationToken);

        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(question))
            text.AppendLine($"Answer for *{dealerUrn}*:");
        else
            text.AppendLine($"Supervisory snapshot for *{dealerUrn}* (cycle {cycleId})");

        text.AppendLine($"• Exceptions: {result.Insights.Exceptions.Count}");
        text.AppendLine($"• Dealers in view: {result.Insights.Dealers.Count}");

        foreach (var ex in result.Insights.Exceptions.Take(3))
            text.AppendLine($"  - {ex.Kind}: {ex.Detail ?? ex.DealerUrn}");

        if (!string.IsNullOrWhiteSpace(result.Explanation))
            text.AppendLine().Append(result.Explanation);

        return Reply(text.ToString().Trim(), SupervisoryInsightAgent.Name, result.Insights);
    }

    private static bool IsHelp(string message)
        => message.Equals("help", StringComparison.OrdinalIgnoreCase)
           || message.Equals("?", StringComparison.OrdinalIgnoreCase)
           || message.StartsWith("/help", StringComparison.OrdinalIgnoreCase);

    private static bool IsIntent(string message, params string[] keywords)
        => keywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveDealerUrn(string message, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return requested.Trim();

        var match = DealerUrnPattern.Match(message);
        return match.Success ? match.Value : null;
    }

    private static string BuildHelpText() =>
        """
        *ARC Assistant* — try:
        • `exposure for dealer:s1` — net recoverable amount (A1)
        • `prioritise dealer:s1` — recovery tier (A2)
        • `notice for dealer:s1` — Issue / Hold / Reconcile (A3)
        • `legal dealer:s3` — Section 138 eligibility (A4)
        • `insights dealer:s1` — exceptions queue (A8)
        • Ask any question with a dealer URN for NLQ

        Shadow mode — no live outbound actions.
        """;

    private static ChatMessageResponse Reply(string reply, string agent, object? data = null)
        => new()
        {
            Reply = reply,
            Agent = agent,
            Timestamp = DateTimeOffset.UtcNow,
            Data = data
        };
}
