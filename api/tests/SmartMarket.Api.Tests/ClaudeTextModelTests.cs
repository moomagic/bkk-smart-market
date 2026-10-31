using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Claude;

namespace SmartMarket.Api.Tests;

/// <summary>The shared text model (dashboard summary, public announcement) against a fake Messages API.</summary>
[Collection(ApiCollection.Name)]
public sealed class ClaudeTextModelTests(ApiFactory api)
{
    private static readonly TextPrompt Prompt = new("สรุป 4 บรรทัด", "{\"occupancy\":0.886}");

    [Fact]
    public async Task Sends_instructions_as_system_and_the_data_as_the_message_and_returns_the_trimmed_text()
    {
        var fake = new FakeApi(HttpStatusCode.OK, Reply("end_turn", "  บรรทัดหนึ่ง\nบรรทัดสอง \n"));

        var text = await Write(fake, Prompt);

        text.Should().Be("บรรทัดหนึ่ง\nบรรทัดสอง");
        var body = fake.Bodies.Should().ContainSingle().Subject;
        body["model"]!.GetValue<string>().Should().Be("claude-test-model");
        body["system"]![0]!["text"]!.GetValue<string>().Should().Be("สรุป 4 บรรทัด");
        body["messages"]![0]!["content"]!.GetValue<string>().Should().Be("{\"occupancy\":0.886}");
        body["output_config"]!["format"].Should().BeNull("plain text, no schema");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "end_turn", "ข้อความ")]
    [InlineData(HttpStatusCode.OK, "refusal", "")]
    [InlineData(HttpStatusCode.OK, "end_turn", "   ")]
    public async Task Any_failure_or_empty_reply_is_null_so_the_caller_uses_its_template(HttpStatusCode status, string stop, string text)
    {
        (await Write(new FakeApi(status, Reply(stop, text)), Prompt)).Should().BeNull();
    }

    [Fact]
    public void Without_a_key_it_is_not_configured()
    {
        api.Services.GetRequiredService<ITextModel>().IsConfigured.Should().BeFalse();
    }

    private async Task<string?> Write(FakeApi fake, TextPrompt prompt)
    {
        await using var withClaude = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Assistant:ApiKey", "test-key");
            b.UseSetting("Assistant:Model", "claude-test-model");
            b.ConfigureTestServices(s => s.AddHttpClient<ClaudeTextModel>().ConfigurePrimaryHttpMessageHandler(() => fake));
        });
        await using var scope = withClaude.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ITextModel>();
        model.IsConfigured.Should().BeTrue();
        return await model.Write(prompt, CancellationToken.None);
    }

    private static JsonObject Reply(string stop, string text) => new()
    {
        ["type"] = "message",
        ["stop_reason"] = stop,
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
    };

    private sealed class FakeApi(HttpStatusCode status, JsonObject reply) : HttpMessageHandler
    {
        public List<JsonNode> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!);
            return new HttpResponseMessage(status) { Content = JsonContent.Create(reply) };
        }
    }
}
