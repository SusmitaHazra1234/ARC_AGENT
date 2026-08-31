using Microsoft.Data.SqlClient;

namespace ARC.Integration.Tests.Fixtures;

public sealed class SqlFixture
{
    public string ConnectionString { get; }

    public SqlFixture()
    {
        var builder = new SqlConnectionStringBuilder(InfrastructureGate.ResolveSqlConnectionString());
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            builder.InitialCatalog = "ARC_Integration";
        ConnectionString = builder.ConnectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
            database = "ARC_Integration";

        builder.InitialCatalog = "master";
        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync(cancellationToken);
            await using var create = master.CreateCommand();
            create.CommandText = $"""
                IF DB_ID(N'{database}') IS NULL
                    CREATE DATABASE [{database}];
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "ReferenceSchema.sql");
        var schema = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        foreach (var batch in CreateTableBatches(schema))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = WrapIdempotentSchema(batch);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task SeedOdosDealerAsync(string dealerUrn, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var delete = connection.CreateCommand())
        {
            delete.CommandText = """
                DELETE FROM dbo.GateDecision WHERE DealerUrn = @Urn;
                DELETE FROM dbo.RecoveryCaseIndex WHERE DealerUrn = @Urn;
                DELETE FROM dbo.LedgerPosition WHERE DealerUrn = @Urn;
                DELETE FROM dbo.Dealer WHERE Urn = @Urn;
                """;
            delete.Parameters.AddWithValue("@Urn", dealerUrn);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertDealer = connection.CreateCommand())
        {
            insertDealer.CommandText = """
                INSERT INTO dbo.Dealer (Urn, SapCode, PortalId, Depot, Region, CoveringTsi, UnderInsolvencyMoratorium)
                VALUES (@Urn, N'SAP-AC2', N'PORTAL-AC2', N'Mumbai-Andheri', N'West', N'tsi.west@paintco.local', 0);
                """;
            insertDealer.Parameters.AddWithValue("@Urn", dealerUrn);
            await insertDealer.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertLedger = connection.CreateCommand())
        {
            insertLedger.CommandText = """
                INSERT INTO dbo.LedgerPosition
                    (DealerUrn, DocumentType, DueDate, PostedOn, Amount, Currency, SourceSystem, SourceTable, SourceKey)
                VALUES
                    (@Urn, N'Invoice', '2025-12-01', '2025-11-15', 100000.00, N'INR', N'SAP-FI-AR', N'BSEG', N'INV-AC2');
                """;
            insertLedger.Parameters.AddWithValue("@Urn", dealerUrn);
            await insertLedger.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static IEnumerable<string> CreateTableBatches(string schema)
    {
        const string marker = "CREATE TABLE";
        var index = 0;
        while (true)
        {
            var start = schema.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                yield break;

            var next = schema.IndexOf(marker, start + marker.Length, StringComparison.OrdinalIgnoreCase);
            var batch = (next < 0 ? schema[start..] : schema[start..next]).Trim();
            if (batch.Length > 0)
                yield return batch;

            if (next < 0)
                yield break;
            index = next;
        }
    }

    private static string WrapIdempotentSchema(string batch)
    {
        var nameStart = batch.IndexOf("dbo.", StringComparison.OrdinalIgnoreCase);
        if (nameStart < 0)
            return batch;
        var nameEnd = batch.IndexOfAny([' ', '\r', '\n', '('], nameStart);
        if (nameEnd < 0)
            return batch;
        var table = batch[nameStart..nameEnd].Trim();
        return $"""
            IF OBJECT_ID(N'{table}', N'U') IS NULL
            BEGIN
            {batch}
            END
            """;
    }
}
