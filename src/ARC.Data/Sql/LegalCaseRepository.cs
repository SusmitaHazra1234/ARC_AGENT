using Dapper;
using ARC.Data.Exceptions;
using ARC.Domain.Entities;
using ARC.Domain.ValueObjects;

namespace ARC.Data.Sql;

public sealed class LegalCaseRepository : ILegalCaseRepository
{
    private readonly ISqlConnectionFactory _connections;

    public LegalCaseRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<LegalCase?> GetAsync(DealerUrn urn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DealerUrn, CaseReference, CompletenessScore, GapsJson
            FROM dbo.LegalCase
            WHERE DealerUrn = @Urn
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LegalRow>(
            new CommandDefinition(sql, new { Urn = urn.Value }, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    public async Task UpsertAsync(LegalCase legalCase, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.LegalCase AS t
            USING (SELECT @DealerUrn AS DealerUrn) AS s
            ON t.DealerUrn = s.DealerUrn
            WHEN MATCHED THEN UPDATE SET
                CaseReference = @CaseReference,
                CompletenessScore = @CompletenessScore,
                GapsJson = @GapsJson
            WHEN NOT MATCHED THEN INSERT (DealerUrn, CaseReference, CompletenessScore, GapsJson)
                VALUES (@DealerUrn, @CaseReference, @CompletenessScore, @GapsJson);
            """;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                DealerUrn = legalCase.DealerUrn.Value,
                legalCase.CaseReference,
                legalCase.CompletenessScore,
                GapsJson = string.Join('\n', legalCase.Gaps)
            }, cancellationToken: cancellationToken));
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException("Failed to persist legal case.", ex);
        }
    }

    private sealed class LegalRow
    {
        public string DealerUrn { get; set; } = "";
        public string? CaseReference { get; set; }
        public decimal CompletenessScore { get; set; }
        public string? GapsJson { get; set; }

        public LegalCase ToDomain() => new(
            new DealerUrn(DealerUrn),
            CompletenessScore,
            string.IsNullOrWhiteSpace(GapsJson) ? [] : GapsJson.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            CaseReference);
    }
}
