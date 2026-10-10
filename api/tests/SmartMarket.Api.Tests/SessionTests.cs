using System.Net.Http.Json;
using FluentAssertions;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SessionTests(ApiFactory api)
{
    private sealed record SessionDto(string User, string Role);

    [Fact]
    public async Task Demo_headers_identify_the_actor()
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Role", "officer");
        client.DefaultRequestHeaders.Add("X-Demo-User", "officer-bangphlat");

        var session = await client.GetFromJsonAsync<SessionDto>("/api/session");

        session.Should().Be(new SessionDto("officer-bangphlat", "officer"));
    }

    [Fact]
    public async Task No_headers_means_anonymous()
    {
        var session = await api.CreateClient().GetFromJsonAsync<SessionDto>("/api/session");

        session.Should().Be(new SessionDto("anonymous", "anonymous"));
    }

    [Fact]
    public async Task Unknown_role_is_not_trusted()
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Role", "admin");

        var session = await client.GetFromJsonAsync<SessionDto>("/api/session");

        session!.Role.Should().Be("anonymous");
    }
}
