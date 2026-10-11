using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DemoReadinessTests(ApiFactory api)
{
    [Fact]
    public async Task The_openapi_document_lists_every_endpoint_and_scalar_serves_it_at_docs()
    {
        var doc = JsonDocument.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        paths.Should().Contain([
            "/api/spaces", "/api/spaces/{id}", "/api/spaces/match", "/api/session", "/api/stall-requests", "/api/stall-requests/me",
            "/api/stall-requests/{id}/confirm", "/api/stall-requests/{id}/forfeit", "/api/stall-requests/{id}/reviews",
            "/api/rounds", "/api/rounds/{id}/eligibility", "/api/rounds/{id}/allocation-runs", "/api/rounds/{id}/reviews",
            "/api/allocation-runs/{id}", "/api/allocation-runs/{id}/approve", "/api/allocation-runs/{id}/reject",
            "/api/reviews/{id}/respond", "/api/check-ins", "/api/dashboard/kpis", "/api/audit", "/api/assistant/ask",
            "/api/demo/clock", "/api/demo/reset",
        ]);
        (await api.CreateClient().GetAsync("/docs")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void No_source_file_says_a_request_is_a_successful_booking()
    {
        // Guardrail 4: submitting is a request in a queue, never "จองสำเร็จ". CLAUDE.md states the rule, so it is skipped.
        var root = RepoRoot();
        var offenders = new[] { "api/src", "web/src", "docs" }
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("จองสำเร็จ"))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))) return dir.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
