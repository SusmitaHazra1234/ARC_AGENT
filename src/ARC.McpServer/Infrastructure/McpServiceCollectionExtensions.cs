using ARC.Data.Configuration;
using ARC.Data.DependencyInjection;
using ARC.Knowledge.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ARC.McpServer.Infrastructure;

internal static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddArcMcpInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dataOptions = configuration.GetSection(ArcDataOptions.SectionName).Get<ArcDataOptions>() ?? new ArcDataOptions();

        if (!HasAzureSql(dataOptions))
        {
            throw new InvalidOperationException(
                "ArcData:Sql:ConnectionString is required in appsettings.Development.local.json or Key Vault.");
        }

        if (!HasAzureCosmos(dataOptions))
        {
            throw new InvalidOperationException(
                "ArcData:Cosmos:ConnectionString is required (primary key from arc-vector-store in Azure Portal).");
        }

        services.AddArcData(configuration);
        services.AddArcKnowledge(configuration);
        return services;
    }

    private static bool HasAzureSql(ArcDataOptions dataOptions)
        => !string.IsNullOrWhiteSpace(dataOptions.Sql.ConnectionString)
           && !dataOptions.Sql.ConnectionString.Contains("YOUR_SQL_SERVER", StringComparison.OrdinalIgnoreCase);

    private static bool HasAzureCosmos(ArcDataOptions dataOptions)
        => (!string.IsNullOrWhiteSpace(dataOptions.Cosmos.ConnectionString)
            && !dataOptions.Cosmos.ConnectionString.Contains("YOUR_COSMOS_PRIMARY_KEY", StringComparison.OrdinalIgnoreCase))
           || (!string.IsNullOrWhiteSpace(dataOptions.Cosmos.AccountEndpoint) && dataOptions.Cosmos.UseManagedIdentity);
}
