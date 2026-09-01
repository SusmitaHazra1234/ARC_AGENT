using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ARC.Agents.DependencyInjection;
using ARC.Data.Configuration;
using ARC.Data.DependencyInjection;
using ARC.Data.Serialization;
using ARC.Host.Functions;
using ARC.Host.Functions.Checkpointing;
using ARC.Host.Functions.Runtime;
using ARC.Knowledge.DependencyInjection;
using ARC.Tools.DependencyInjection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, config) => config.AddArcKeyVault())
    .ConfigureServices((context, services) =>
    {
        services.Configure<ArcHostOptions>(context.Configuration.GetSection(ArcHostOptions.SectionName));
        services.AddArcData(context.Configuration);
        services.AddArcKnowledge(context.Configuration);
        services.AddArcTools(context.Configuration);
        services.AddArcLlm(context.Configuration);
        services.AddArcAgents();

        services.AddSingleton<ICheckpointStore<JsonElement>, CosmosJsonCheckpointStore>();
        services.AddSingleton(sp => CheckpointManager.CreateJson(
            sp.GetRequiredService<ICheckpointStore<JsonElement>>(),
            new JsonSerializerOptions(ArcJson.Options)));

        services.AddSingleton<DealerWorkflowRunner>();
        services.AddSingleton<OdosCycleFanOut>();
    })
    .Build();

host.Run();
