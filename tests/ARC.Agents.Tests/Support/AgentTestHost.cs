using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ARC.Agents.DependencyInjection;
using ARC.Agents.Tests.Fakes;
using ARC.Data.Blob;
using ARC.Data.Cosmos;
using ARC.Data.Messaging;
using ARC.Data.Sql;
using ARC.Knowledge.Graph;
using ARC.Knowledge.Retrieval;
using ARC.Tools.DependencyInjection;
using ARC.Tools.Models;

namespace ARC.Agents.Tests.Support;

internal static class AgentTestHost
{
    public static (ServiceProvider Services, InMemoryHarness Store) Create()
    {
        var store = new InMemoryHarness();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddSingleton(store);
        services.AddSingleton<IDealerRepository>(store);
        services.AddSingleton<ILedgerRepository>(store);
        services.AddSingleton<IChequeRepository>(store);
        services.AddSingleton<IGateDecisionRepository>(store);
        services.AddSingleton<ILegalCaseRepository>(store);
        services.AddSingleton<IRecoveryCaseRepository>(store);
        services.AddSingleton<IWorkflowStateRepository>(store);
        services.AddSingleton<IConversationStateRepository>(store);
        services.AddSingleton<IAuditRepository>(store);
        services.AddSingleton<IEvidenceDocumentRepository>(store);
        services.AddSingleton<IServiceBusPublisher>(store);
        services.AddSingleton<IKnowledgeRetrievalService, EmptyKnowledgeRetrievalService>();
        services.AddSingleton<IGraphTraversal, EmptyGraphTraversal>();
        services.AddSingleton<IChatClient, EmptyChatClient>();
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddArcTools(configuration);
        services.PostConfigure<ArcToolsOptions>(o => o.VoicePtpConfirmBelow = 0.80m);
        services.AddArcAgents();

        return (services.BuildServiceProvider(), store);
    }
}
