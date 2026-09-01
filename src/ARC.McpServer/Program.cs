using ARC.Data.Configuration;
using ARC.Tools.DependencyInjection;
using ARC.Tools.Models;
using ARC.McpServer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddArcKeyVault();

builder.Logging.AddConsole(console =>
{
    console.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddArcMcpInfrastructure(builder.Configuration);
builder.Services.AddArcTools(builder.Configuration);
builder.Services.PostConfigure<ArcToolsOptions>(options =>
{
    options.VoicePtpConfirmBelow = 0.80m;
});

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
