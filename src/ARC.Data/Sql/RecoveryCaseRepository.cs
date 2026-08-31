using Dapper;
using ARC.Data.Exceptions;
using ARC.Domain.ValueObjects;

namespace ARC.Data.Sql;

public sealed class RecoveryCaseRepository : IRecoveryCaseRepository
{
    private readonly ISqlConnectionFactory _connections;

    public RecoveryCaseRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task UpsertIndexAsync(RecoveryCaseIndex index, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.RecoveryCaseIndex AS t
            USING (SELECT @CycleId AS CycleId, @DealerUrn AS DealerUrn) AS s
            ON t.CycleId = s.CycleId AND t.DealerUrn = s.DealerUrn
            WHEN MATCHED THEN UPDATE SET
                Status = @Status,
                CorrelationId = @CorrelationId,
                WaitingGate = @WaitingGate,
                UpdatedUtc = @UpdatedUtc
            WHEN NOT MATCHED THEN INSERT
                (CycleId, DealerUrn, Status, CorrelationId, WaitingGate, UpdatedUtc)
                VALUES (@CycleId, @DealerUrn, @Status, @CorrelationId, @WaitingGate, @UpdatedUtc);
            """;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                CycleId = index.CycleId.Value,
                DealerUrn = index.DealerUrn.Value,
                index.Status,
                index.CorrelationId,
                index.WaitingGate,
                index.UpdatedUtc
            }, cancellationToken: cancellationToken));
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException("Failed to persist recovery case index.", ex);
        }
    }

    public async Task<RecoveryCaseIndex?> GetAsync(CycleId cycleId, DealerUrn dealerUrn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CycleId, DealerUrn, Status, CorrelationId, WaitingGate, UpdatedUtc
            FROM dbo.RecoveryCaseIndex
            WHERE CycleId = @CycleId AND DealerUrn = @DealerUrn
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<IndexRow>(
            new CommandDefinition(sql, new { CycleId = cycleId.Value, DealerUrn = dealerUrn.Value }, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<RecoveryCaseIndex>> ListByCycleAsync(
        CycleId cycleId, string? region, string? depot, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT i.CycleId, i.DealerUrn, i.Status, i.CorrelationId, i.WaitingGate, i.UpdatedUtc
            FROM dbo.RecoveryCaseIndex i
            INNER JOIN dbo.Dealer d ON d.Urn = i.DealerUrn
            WHERE i.CycleId = @CycleId
              AND (@Region IS NULL OR d.Region = @Region)
              AND (@Depot IS NULL OR d.Depot = @Depot)
            ORDER BY i.UpdatedUtc DESC
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<IndexRow>(
            new CommandDefinition(sql, new
            {
                CycleId = cycleId.Value,
                Region = string.IsNullOrWhiteSpace(region) ? null : region,
                Depot = string.IsNullOrWhiteSpace(depot) ? null : depot
            }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    private sealed class IndexRow
    {
        public string CycleId { get; set; } = "";
        public string DealerUrn { get; set; } = "";
        public string Status { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public string? WaitingGate { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }

        public RecoveryCaseIndex ToDomain() => new(
            new CycleId(CycleId),
            new DealerUrn(DealerUrn),
            Status,
            CorrelationId,
            WaitingGate,
            UpdatedUtc);
    }
}
