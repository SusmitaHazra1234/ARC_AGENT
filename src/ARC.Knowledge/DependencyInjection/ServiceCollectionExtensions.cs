using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Documents;
using ARC.Knowledge.Graph;
using ARC.Knowledge.Retrieval;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArcKnowledge(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArcKnowledgeOptions>(configuration.GetSection(ArcKnowledgeOptions.SectionName));
        services.AddSingleton<IDocumentIntelligenceService, DocumentIntelligenceService>();
        services.AddSingleton<IGraphTraversal, GraphTraversal>();
        services.AddSingleton<IVectorSearch, CosmosHybridSearch>();
        services.AddSingleton<IKnowledgeRetrievalService, KnowledgeRetrievalService>();
        return services;
    }
}
