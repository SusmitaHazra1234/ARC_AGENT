using ARC.Agents.Workflows.Models;
using ARC.Domain.Enums;

namespace ARC.Api.DTOs;

public sealed record GateDecisionRequest
{
    public required string CycleId { get; init; }
    public required string DealerUrn { get; init; }
    public ArcWorkflowKind? Kind { get; init; }
    public required GateDecisionStatus Decision { get; init; }
    public required string Reason { get; init; }
}

public sealed record StartRunRequest
{
    public required ArcWorkflowKind Kind { get; init; }
    public DateOnly? AsOf { get; init; }
    public RunMode? Mode { get; init; }
}

public sealed record NlqRequest
{
    public required string CycleId { get; init; }
    public required string Question { get; init; }
    public string? DealerUrn { get; init; }
    public string? Region { get; init; }
}

public sealed record CaseSummaryDto(
    string CycleId,
    string DealerUrn,
    string Status,
    string? WaitingGate,
    string CorrelationId,
    DateTimeOffset UpdatedUtc);

public sealed record CaseDetailDto(
    string CycleId,
    string DealerUrn,
    string Status,
    string? WaitingGate,
    string? TerminationReason,
    string? NoticeDecision,
    bool? Section138Eligible,
    string? ClockStatus,
    int? ClockDaysRemaining,
    IReadOnlyList<GateAuditDto> Gates);

public sealed record GateAuditDto(
    string Gate,
    string ActorUpn,
    string ActorRole,
    string Decision,
    string Reason,
    DateTimeOffset DecidedUtc);

public sealed record CycleDashboardDto(
    string CycleId,
    int Total,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> WaitingByGate);
