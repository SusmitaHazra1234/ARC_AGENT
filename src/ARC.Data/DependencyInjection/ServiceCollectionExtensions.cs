using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ARC.Data.Blob;
using ARC.Data.Configuration;
using ARC.Data.Cosmos;
using ARC.Data.Messaging;
using ARC.Data.Sql;

namespace ARC.Data.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArcData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArcDataOptions>(configuration.GetSection(ArcDataOptions.SectionName));

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IDealerRepository, DealerRepository>();
        services.AddSingleton<ILedgerRepository, LedgerRepository>();
        services.AddSingleton<IChequeRepository, ChequeRepository>();
        services.AddSingleton<IGateDecisionRepository, GateDecisionRepository>();
        services.AddSingleton<ILegalCaseRepository, LegalCaseRepository>();
        services.AddSingleton<IRecoveryCaseRepository, RecoveryCaseRepository>();

        services.AddSingleton<ICosmosClientFactory, CosmosClientFactory>();
        services.AddSingleton<IWorkflowStateRepository, WorkflowStateRepository>();
        services.AddSingleton<IMafCheckpointDocumentStore, MafCheckpointDocumentStore>();
        services.AddSingleton<IConversationStateRepository, ConversationStateRepository>();
        services.AddSingleton<IAuditRepository, AuditRepository>();

        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<IEvidenceDocumentRepository, EvidenceDocumentRepository>();

        services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();

        return services;
    }
}
