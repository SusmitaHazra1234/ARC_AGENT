using Dapper;
using ARC.Domain.Entities;
using ARC.Domain.Enums;
using ARC.Domain.ValueObjects;

namespace ARC.Data.Sql;

public sealed class ChequeRepository : IChequeRepository
{
    private readonly ISqlConnectionFactory _connections;

    public ChequeRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<IReadOnlyList<SecurityCheque>> ListChequesAsync(DealerUrn urn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DealerUrn, ChequeNumber, Micr, Amount, Currency, Status, DepositDate, ValidityEnd, ExtractionConfidence
            FROM dbo.SecurityCheque
            WHERE DealerUrn = @Urn
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ChequeRow>(
            new CommandDefinition(sql, new { Urn = urn.Value }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<ChequeReturnMemo>> ListReturnMemosAsync(DealerUrn urn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DealerUrn, ChequeNumber, ReturnReasonCode, MemoIssueDate, MemoReceivedDate, ExtractionConfidence
            FROM dbo.ChequeReturnMemo
            WHERE DealerUrn = @Urn
            """;
        await using var connection = await _connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemoRow>(
            new CommandDefinition(sql, new { Urn = urn.Value }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    private sealed class ChequeRow
    {
        public string DealerUrn { get; set; } = "";
        public string ChequeNumber { get; set; } = "";
        public string? Micr { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string Status { get; set; } = "";
        public DateTime? DepositDate { get; set; }
        public DateTime? ValidityEnd { get; set; }
        public decimal? ExtractionConfidence { get; set; }

        public SecurityCheque ToDomain() => new(
            new DealerUrn(DealerUrn),
            ChequeNumber,
            new Money(Amount, Currency),
            Enum.Parse<ChequeStatus>(Status, ignoreCase: true),
            Micr,
            DepositDate is { } d ? DateOnly.FromDateTime(d) : null,
            ValidityEnd is { } v ? DateOnly.FromDateTime(v) : null,
            ExtractionConfidence);
    }

    private sealed class MemoRow
    {
        public string DealerUrn { get; set; } = "";
        public string ChequeNumber { get; set; } = "";
        public string ReturnReasonCode { get; set; } = "";
        public DateTime MemoIssueDate { get; set; }
        public DateTime MemoReceivedDate { get; set; }
        public decimal? ExtractionConfidence { get; set; }

        public ChequeReturnMemo ToDomain() => new(
            new DealerUrn(DealerUrn),
            ChequeNumber,
            ReturnReasonCode,
            DateOnly.FromDateTime(MemoIssueDate),
            DateOnly.FromDateTime(MemoReceivedDate),
            ExtractionConfidence);
    }
}
