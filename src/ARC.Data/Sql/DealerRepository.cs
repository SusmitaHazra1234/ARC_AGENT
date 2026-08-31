using Dapper;
using ARC.Domain.Entities;
using ARC.Domain.ValueObjects;

namespace ARC.Data.Sql;

public sealed class DealerRepository : IDealerRepository
{
    private readonly ISqlConnectionFactory _connections;

    public DealerRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<Dealer?> GetAsync(DealerUrn urn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Urn, SapCode, PortalId, Depot, Region, CoveringTsi, UnderInsolvencyMoratorium
            FROM dbo.Dealer
            WHERE Urn = @Urn
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DealerRow>(
            new CommandDefinition(sql, new { Urn = urn.Value }, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<Dealer>> ListByRegionAsync(string region, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required for server-side isolation.", nameof(region));

        const string sql = """
            SELECT Urn, SapCode, PortalId, Depot, Region, CoveringTsi, UnderInsolvencyMoratorium
            FROM dbo.Dealer
            WHERE Region = @Region
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<DealerRow>(
            new CommandDefinition(sql, new { Region = region }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Dealer>> ListAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Urn, SapCode, PortalId, Depot, Region, CoveringTsi, UnderInsolvencyMoratorium
            FROM dbo.Dealer
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<DealerRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    private sealed class DealerRow
    {
        public string Urn { get; set; } = "";
        public string? SapCode { get; set; }
        public string? PortalId { get; set; }
        public string? Depot { get; set; }
        public string? Region { get; set; }
        public string? CoveringTsi { get; set; }
        public bool UnderInsolvencyMoratorium { get; set; }

        public Dealer ToDomain() => new(
            new DealerUrn(Urn),
            UnderInsolvencyMoratorium,
            SapCode,
            PortalId,
            Depot,
            Region,
            CoveringTsi);
    }
}
