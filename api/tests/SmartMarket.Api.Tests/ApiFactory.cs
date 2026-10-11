using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartMarket.Application.Common;
using Testcontainers.PostgreSql;

namespace SmartMarket.Api.Tests;

/// <summary>The real API against a throwaway PostgreSQL 17, with every log line captured.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public ConcurrentQueue<string> Logs { get; } = new();
    public TestClock Clock { get; } = new();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server; // boot the host: runs migrations + seed
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SmartMarket", _db.GetConnectionString());
        builder.ConfigureServices(s => s.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CapturingLoggerProvider(Logs))));
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<IClock>(Clock);
            s.AddSingleton<IDemoClock>(Clock);
        });
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            sink.Enqueue($"{category}: {formatter(state, exception)} {exception}");
    }
}

public static class ApiFactoryExtensions
{
    /// <summary>An HttpClient acting as the given demo role/user (mock auth headers).</summary>
    public static HttpClient ClientAs(this ApiFactory api, string role, string user)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Role", role);
        client.DefaultRequestHeaders.Add("X-Demo-User", user);
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
