using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using SmartMarket.Api.Auth;
using SmartMarket.Api.Endpoints;
using SmartMarket.Application;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
builder.Services
    .AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(DemoPolicies.Vendor, p => p.RequireRole("vendor"))
    .AddPolicy(DemoPolicies.Officer, p => p.RequireRole("officer", "executive"));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ConcurrentUpdateHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<DemoHeadersDocument>());

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI at /openapi/v1.json, browsable with Scalar at /docs (send X-Demo-Role / X-Demo-User from there).
app.MapOpenApi();
app.MapScalarApiReference("/docs", o => o
    .AddApiKeyAuthentication(DemoAuthenticationHandler.RoleHeader, k => k.Value = "officer")
    .AddApiKeyAuthentication(DemoAuthenticationHandler.UserHeader, k => k.Value = "officer-bangphlat"));

var api = app.MapGroup("/api");
api.MapSpaces();
api.MapSession();
api.MapStallRequests();
api.MapRounds();
api.MapAllocationRuns();
api.MapCheckIns();
api.MapAudit();
api.MapAssistant();
api.MapDashboard();
api.MapReviews();
if (app.Environment.IsDevelopment())
    api.MapDemo();

app.Run();

public partial class Program;
