using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ARC.Agents.A1Reconciliation;
using ARC.Agents.A2RiskPrioritisation;
using ARC.Agents.A3NoticeDecisioning;
using ARC.Agents.A4LegalEligibility;
using ARC.Agents.A5DraftingVerification;
using ARC.Agents.A6FieldOrchestration;
using ARC.Agents.A7EvidenceCaseFile;
using ARC.Agents.A8SupervisoryInsight;
using ARC.Agents.Llm;
using ARC.Agents.Workflows;
using ARC.Agents.Workflows.Executors;
using ARC.Agents.Workflows.Outbound;
using ARC.Agents.Workflows.Persistence;

namespace ARC.Agents.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers A1–A8, ODOS/S138 workflow graphs, RequestPort gates, and a Shadow outbound gate.
    /// The host must also register <c>AddArcLlm</c> (or <c>IChatClient</c>), <c>AddArcData</c>, <c>AddArcKnowledge</c>, and <c>AddArcTools</c>.
    /// Human-approval HTTP APIs and Azure Functions triggers are not registered here.
    /// </summary>
    public static IServiceCollection AddArcAgents(this IServiceCollection services)
    {
        services.AddSingleton<ReconciliationAgent>();
        services.AddSingleton<RiskPrioritisationAgent>();
        services.AddSingleton<NoticeDecisioningAgent>();
        services.AddSingleton<LegalEligibilityAgent>();
        services.AddSingleton<DraftingVerificationAgent>();
        services.AddSingleton<FieldOrchestrationAgent>();
        services.AddSingleton<EvidenceCaseFileAgent>();
        services.AddSingleton<SupervisoryInsightAgent>();

        services.AddSingleton<WorkflowNodePersistence>();
        services.AddSingleton<IOutboundGate, ShadowOutboundGate>();
        services.AddSingleton<ArcWorkflowExecutors>();
        services.AddSingleton<OdosCycleWorkflow>();
        services.AddSingleton<Section138Workflow>();
        services.AddKeyedSingleton<Workflow>(OdosCycleWorkflow.Name, (sp, _) => sp.GetRequiredService<OdosCycleWorkflow>().Build());
        services.AddKeyedSingleton<Workflow>(Section138Workflow.Name, (sp, _) => sp.GetRequiredService<Section138Workflow>().Build());
        return services;
    }

    /// <summary>
    /// Registers <see cref="IChatClient"/> from <c>Llm:Provider</c> (Shadow default, or AzureOpenAI).
    /// Agents keep depending on <see cref="IChatClient"/> only.
    /// </summary>
    public static IServiceCollection AddArcLlm(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        var provider = configuration[$"{LlmOptions.SectionName}:Provider"] ?? "Shadow";
        if (string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IChatClient, AzureOpenAILlmFactory>();
        else
            services.AddSingleton<IChatClient, ShadowLlmFactory>();
        return services;
    }
}
