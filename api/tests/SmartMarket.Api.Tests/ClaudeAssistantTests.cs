using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Infrastructure.Assistant;

namespace SmartMarket.Api.Tests;

/// <summary>The Claude path of the Vendor Assistant, against a fake Messages API — no network, no key.</summary>
[Collection(ApiCollection.Name)]
public sealed class ClaudeAssistantTests(ApiFactory api)
{
    private sealed record CitationDto(string File, string Heading);
    private sealed record AnswerDto(string Text, bool Found, CitationDto[] Citations, string Source);

    [Fact]
    public async Task With_a_key_the_answer_comes_from_claude_with_the_rules_in_a_cached_system_prompt()
    {
        var fake = FakeMessagesApi.Answering(Structured(
            "ใช้บัตรประจำตัวประชาชนและเบอร์โทรที่ติดต่อได้\nยื่นในพื้นที่อาหารต้องมีใบรับรองอบรมสุขาภิบาลอาหาร",
            found: true, ("documents.md", "เอกสารที่ต้องใช้ในการยื่นคำขอ")));

        var answer = await Ask(fake, "ต้องใช้เอกสารอะไรบ้าง");

        answer.Source.Should().Be("claude");
        answer.Found.Should().BeTrue();
        answer.Text.Should().Be("ใช้บัตรประจำตัวประชาชนและเบอร์โทรที่ติดต่อได้\nยื่นในพื้นที่อาหารต้องมีใบรับรองอบรมสุขาภิบาลอาหาร\n"
                                + "อ้างอิง: documents.md › เอกสารที่ต้องใช้ในการยื่นคำขอ");
        answer.Citations.Should().Equal(new CitationDto("documents.md", "เอกสารที่ต้องใช้ในการยื่นคำขอ"));

        var call = fake.Requests.Should().ContainSingle().Subject;
        call.Headers["x-api-key"].Should().Be("test-key");
        call.Headers["anthropic-version"].Should().Be("2023-06-01");
        call.Body["model"]!.GetValue<string>().Should().Be("claude-test-model", "the model comes from config/env");
        var system = call.Body["system"]!.AsArray().Should().ContainSingle().Subject!;
        system["cache_control"]!["type"]!.GetValue<string>().Should().Be("ephemeral");
        system["text"]!.GetValue<string>().Should().Contain("## เอกสารที่ต้องใช้ในการยื่นคำขอ").And.Contain("allocation.md",
            "every rules file is in the context");
        call.Body["messages"]![0]!["content"]!.GetValue<string>().Should().Be("ต้องใช้เอกสารอะไรบ้าง");
    }

    [Fact]
    public async Task Claude_saying_not_found_gives_the_standard_reply()
    {
        var answer = await Ask(FakeMessagesApi.Answering(Structured("", found: false)), "ราคาทองวันนี้");

        answer.Should().Match<AnswerDto>(a => !a.Found && a.Source == "claude"
            && a.Text == "ไม่พบในระเบียบที่ประกาศ กรุณาติดต่อสำนักงานเขตบางพลัด");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task An_api_error_falls_back_to_keyword_matching(HttpStatusCode status)
    {
        var answer = await Ask(FakeMessagesApi.Failing(status), "ต้องใช้เอกสารอะไร");

        answer.Source.Should().Be("faq");
        answer.Citations.Should().ContainSingle().Which.File.Should().Be("documents.md");
    }

    [Fact]
    public async Task A_timeout_falls_back_to_keyword_matching()
    {
        var answer = await Ask(FakeMessagesApi.Hanging(), "ต้องใช้เอกสารอะไร", timeoutSeconds: 1);

        answer.Source.Should().Be("faq");
        answer.Found.Should().BeTrue();
    }

    [Fact]
    public async Task A_refusal_or_an_answer_citing_no_real_section_falls_back()
    {
        (await Ask(FakeMessagesApi.Answering(Refusal()), "ต้องใช้เอกสารอะไร")).Source.Should().Be("faq");
        (await Ask(FakeMessagesApi.Answering(Structured("ตอบมั่ว", found: true, ("made-up.md", "ไม่มีจริง"))), "ต้องใช้เอกสารอะไร"))
            .Source.Should().Be("faq", "an answer must cite a section that exists in docs/rules");
    }

    [Fact]
    public async Task Long_answers_are_cut_to_four_lines_including_the_citation()
    {
        var answer = await Ask(FakeMessagesApi.Answering(Structured("1\n2\n3\n4\n5", found: true, ("documents.md", "เอกสารที่ต้องใช้ในการยื่นคำขอ"))),
            "ต้องใช้เอกสารอะไร");

        answer.Text.Split('\n').Should().HaveCount(4).And.EndWith("อ้างอิง: documents.md › เอกสารที่ต้องใช้ในการยื่นคำขอ");
    }

    [Fact]
    public async Task Asking_about_someone_elses_outcome_is_declined_without_calling_claude()
    {
        var fake = FakeMessagesApi.Answering(Structured("สมศรีได้ล็อค B2", found: true, ("allocation.md", "วิธีจัดสรรล็อค")));

        var answer = await Ask(fake, "สมศรีได้ล็อคไหม");

        answer.Found.Should().BeFalse();
        answer.Text.Should().Contain("ตอบเรื่องผลการจัดสรรของแต่ละคน");
        fake.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_a_key_the_assistant_uses_keyword_matching()
    {
        var response = await api.Vendor(Guid.NewGuid()).PostAsJsonAsync("/api/assistant/ask", new { question = "ต้องใช้เอกสารอะไร" });

        (await response.Content.ReadFromJsonAsync<AnswerDto>())!.Source.Should().Be("faq");
    }

    private async Task<AnswerDto> Ask(FakeMessagesApi fake, string question, int timeoutSeconds = 15)
    {
        await using var withClaude = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Assistant:ApiKey", "test-key");
            b.UseSetting("Assistant:Model", "claude-test-model");
            b.UseSetting("Assistant:TimeoutSeconds", timeoutSeconds.ToString());
            b.ConfigureTestServices(s => s.AddHttpClient<ClaudeAssistantModel>().ConfigurePrimaryHttpMessageHandler(() => fake));
        });
        var client = withClaude.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Role", "vendor");
        client.DefaultRequestHeaders.Add("X-Demo-User", Guid.NewGuid().ToString());
        var response = await client.PostAsJsonAsync("/api/assistant/ask", new { question });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AnswerDto>())!;
    }

    /// <summary>A Messages API response whose text block is the structured answer the assistant asks for.</summary>
    private static JsonObject Structured(string answer, bool found, params (string File, string Heading)[] citations) => new()
    {
        ["id"] = "msg_test",
        ["type"] = "message",
        ["role"] = "assistant",
        ["stop_reason"] = "end_turn",
        ["content"] = new JsonArray(new JsonObject
        {
            ["type"] = "text",
            ["text"] = JsonSerializer.Serialize(new
            {
                answer, found, citations = citations.Select(c => new { file = c.File, heading = c.Heading }),
            }),
        }),
    };

    private static JsonObject Refusal() => new()
    {
        ["id"] = "msg_test", ["type"] = "message", ["role"] = "assistant", ["stop_reason"] = "refusal",
        ["content"] = new JsonArray(),
    };

    private sealed record Recorded(Dictionary<string, string> Headers, JsonNode Body);

    private sealed class FakeMessagesApi(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Recorded> Requests { get; } = [];

        public static FakeMessagesApi Answering(JsonObject body) =>
            new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) }));

        public static FakeMessagesApi Failing(HttpStatusCode status) =>
            new(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"type\":\"error\"}") }));

        public static FakeMessagesApi Hanging() =>
            new(async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            Requests.Add(new Recorded(headers, JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!));
            return await respond(ct);
        }
    }
}
