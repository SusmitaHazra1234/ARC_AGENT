using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.AI;
using ARC.Agents.DependencyInjection;
using ARC.Api.Auth;
using ARC.Api.Configuration;
using ARC.Api.Middleware;
using ARC.Api.Services;
using ARC.Data.DependencyInjection;
using ARC.Knowledge.DependencyInjection;
using ARC.Tools.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ArcApiOptions>(builder.Configuration.GetSection(ArcApiOptions.SectionName));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ArcExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var apiOptions = builder.Configuration.GetSection(ArcApiOptions.SectionName).Get<ArcApiOptions>() ?? new();
if (!string.IsNullOrWhiteSpace(apiOptions.JwtAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = apiOptions.JwtAuthority;
            options.Audience = apiOptions.JwtAudience;
            options.MapInboundClaims = false;
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddArcData(builder.Configuration);
builder.Services.AddArcKnowledge(builder.Configuration);
builder.Services.AddArcTools(builder.Configuration);
builder.Services.AddSingleton<IChatClient, ShadowNarrationChatClient>();
builder.Services.AddArcAgents();

var app = builder.Build();
app.UseExceptionHandler();
if (!string.IsNullOrWhiteSpace(apiOptions.JwtAuthority))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseMiddleware<ArcActorMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));
app.MapControllers();
app.Run();
