using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using SmartMarket.Api.Auth;
using SmartMarket.Api.Endpoints;
using SmartMarket.Application;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure;

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
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.MapSpaces();
api.MapSession();
api.MapStallRequests();
api.MapRounds();
api.MapAllocationRuns();
api.MapCheckIns();
api.MapAudit();
api.MapAssistant();
if (app.Environment.IsDevelopment())
    api.MapDemo();

app.Run();

public partial class Program;
