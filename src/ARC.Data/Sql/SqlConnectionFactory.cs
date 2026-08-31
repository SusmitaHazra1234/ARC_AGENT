using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ARC.Data.Configuration;
using ARC.Data.Exceptions;

namespace ARC.Data.Sql;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly SqlStoreOptions _options;

    public SqlConnectionFactory(IOptions<ArcDataOptions> options)
        => _options = options.Value.Sql;

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new DataAccessException("ArcData:Sql:ConnectionString is not configured.");

        try
        {
            var connection = new SqlConnection(_options.ConnectionString);
            if (_options.UseManagedIdentity)
            {
                var credential = new DefaultAzureCredential();
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(["https://database.windows.net/.default"]),
                    cancellationToken);
                connection.AccessToken = token.Token;
            }

            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (DataAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataAccessException("Failed to open Azure SQL connection.", ex);
        }
    }
}
