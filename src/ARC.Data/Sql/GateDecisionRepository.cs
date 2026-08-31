using Dapper;
using ARC.Data.Exceptions;
using ARC.Domain.Entities;
using ARC.Domain.Enums;
using ARC.Domain.ValueObjects;

namespace ARC.Data.Sql;

/// <summary>
/// Append-only gate audit. Expired is stored as Expired — never rewritten to Approved.
/// Idempotent on (CycleId, DealerUrn, GateId, CorrelationId).
/// </summary>
public sealed class GateDecisionRepository : IGateDecisionRepository
{
    private readonly ISqlConnectionFactory _connections;

    public GateDecisionRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task SaveAsync(CycleId cycleId, DealerUrn dealerUrn, GateDecision decision, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1 FROM dbo.GateDecision
                WHERE CycleId = @CycleId AND DealerUrn = @DealerUrn
                  AND GateId = @GateId AND CorrelationId = @CorrelationId)
            INSERT INTO dbo.GateDecision
                (CycleId, DealerUrn, GateId, ActorUpn, ActorRole, Decision, Reason,
                 RecommendedAction, DecidedUtc, CorrelationId, WasOverride)
            VALUES
                (@CycleId, @DealerUrn, @GateId, @ActorUpn, @ActorRole, @Decision, @Reason,
                 @RecommendedAction, @DecidedUtc, @CorrelationId, @WasOverride)
            """;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                CycleId = cycleId.Value,
                DealerUrn = dealerUrn.Value,
                GateId = decision.Gate.ToString(),
                decision.ActorUpn,
                ActorRole = decision.ActorRole.ToString(),
                Decision = decision.Decision.ToString(),
                decision.Reason,
                decision.RecommendedAction,
                DecidedUtc = decision.DecidedUtc,
                CorrelationId = decision.CorrelationId.Value,
                decision.WasOverride
            }, cancellationToken: cancellationToken));
        }
        catch (DataAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataAccessException("Failed to persist gate decision.", ex);
        }
    }

    public async Task<IReadOnlyList<GateDecision>> ListAsync(CycleId cycleId, DealerUrn dealerUrn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT GateId, ActorUpn, ActorRole, Decision, Reason, RecommendedAction, DecidedUtc, CorrelationId
            FROM dbo.GateDecision
            WHERE CycleId = @CycleId AND DealerUrn = @DealerUrn
            ORDER BY DecidedUtc
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<GateRow>(
            new CommandDefinition(sql, new { CycleId = cycleId.Value, DealerUrn = dealerUrn.Value }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    private sealed class GateRow
    {
        public string GateId { get; set; } = "";
        public string ActorUpn { get; set; } = "";
        public string ActorRole { get; set; } = "";
        public string Decision { get; set; } = "";
        public string Reason { get; set; } = "";
        public string? RecommendedAction { get; set; }
        public DateTimeOffset DecidedUtc { get; set; }
        public string CorrelationId { get; set; } = "";

        public GateDecision ToDomain() => GateDecision.Create(
            Enum.Parse<GateId>(GateId, ignoreCase: true),
            ActorUpn,
            Enum.Parse<ActorRole>(ActorRole, ignoreCase: true),
            Enum.Parse<GateDecisionStatus>(Decision, ignoreCase: true),
            Reason,
            new CorrelationId(CorrelationId),
            RecommendedAction,
            DecidedUtc);
    }
}
