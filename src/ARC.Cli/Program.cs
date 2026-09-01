using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ARC.Agents.DependencyInjection;
using ARC.Agents.Workflows.Outbound;
using ARC.Cli.Fakes;
using ARC.Cli.Runtime;
using ARC.Cli.Scenarios;
using ARC.Data.Blob;
using ARC.Data.Cosmos;
using ARC.Data.Messaging;
using ARC.Data.Serialization;
using ARC.Data.Sql;
using ARC.Knowledge.Graph;
using ARC.Knowledge.Retrieval;
using ARC.Tools.DependencyInjection;
using ARC.Tools.Models;

var ids = ParseScenarioIds(args);
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton<InMemoryArcStore>();
builder.Services.AddSingleton<IDealerRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<ILedgerRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IChequeRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IGateDecisionRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<ILegalCaseRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IRecoveryCaseRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IWorkflowStateRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IConversationStateRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IAuditRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IEvidenceDocumentRepository>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IServiceBusPublisher>(sp => sp.GetRequiredService<InMemoryArcStore>());
builder.Services.AddSingleton<IKnowledgeRetrievalService, EmptyKnowledgeRetrievalService>();
builder.Services.AddSingleton<IGraphTraversal, EmptyGraphTraversal>();
builder.Services.AddArcLlm(builder.Configuration);
builder.Services.AddSingleton<MemoryJsonCheckpointStore>();
builder.Services.AddSingleton<ICheckpointStore<JsonElement>>(sp => sp.GetRequiredService<MemoryJsonCheckpointStore>());
builder.Services.AddSingleton(sp => CheckpointManager.CreateJson(
    sp.GetRequiredService<ICheckpointStore<JsonElement>>(),
    new JsonSerializerOptions(ArcJson.Options)));

builder.Services.AddArcTools(builder.Configuration);
builder.Services.PostConfigure<ArcToolsOptions>(options =>
{
    options.VoicePtpConfirmBelow = 0.80m;
});
builder.Services.AddArcAgents();
builder.Services.AddSingleton<CliOutboundRecorder>();
builder.Services.AddSingleton<IOutboundGate>(sp => sp.GetRequiredService<CliOutboundRecorder>());
builder.Services.AddSingleton<CliWorkflowDriver>();
builder.Services.AddSingleton<ScenarioRunner>();

using var host = builder.Build();
var runner = host.Services.GetRequiredService<ScenarioRunner>();
var failed = 0;

Console.WriteLine("ARC CLI — local Shadow scenario runner (S1–S9). No Azure, no Live outbound.");
Console.WriteLine();

foreach (var id in ids)
{
    Console.WriteLine($"=== {id} ===");
    try
    {
        var outcome = await runner.RunAsync(id, CancellationToken.None);
        var mark = outcome.Passed ? "PASS" : "FAIL";
        if (!outcome.Passed)
            failed++;
        Console.WriteLine($"{mark}  {outcome.Summary}");
        foreach (var check in outcome.Checks)
            Console.WriteLine($"  {(check.Pass ? "ok" : "x ")} {check.Detail}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  {id} threw:");
        Console.WriteLine(ex);
    }

    Console.WriteLine();
}

Console.WriteLine(failed == 0
    ? $"All {ids.Count} scenario(s) passed. Outbound remains Shadow."
    : $"{failed} of {ids.Count} scenario(s) failed.");

return failed == 0 ? 0 : 1;

static IReadOnlyList<string> ParseScenarioIds(string[] arguments)
{
    if (arguments.Length == 0 || string.Equals(arguments[0], "all", StringComparison.OrdinalIgnoreCase))
        return ["S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8", "S9"];

    return arguments
        .Select(a => a.Trim().ToUpperInvariant())
        .Where(a => a.Length > 0)
        .ToList();
}
