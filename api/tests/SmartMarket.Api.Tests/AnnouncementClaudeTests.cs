using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Infrastructure.Claude;

namespace SmartMarket.Api.Tests;

/// <summary>The announcement's method paragraph written by Claude (fake Messages API); the figures always from the run.</summary>
[Collection(ApiCollection.Name)]
public sealed class AnnouncementClaudeTests(ApiFactory api)
{
    private const string Paragraph = "พื้นที่นี้ใช้การจับฉลากแยกตามกลุ่ม\nไม่ใช่ใครมาก่อนได้ก่อน (เลขอ้างอิง BPL-0000-0000-99)";

    [Fact]
    public async Task With_a_key_claude_writes_the_method_and_the_verifiable_figures_still_come_from_the_run()
    {
        var (_, run) = await api.ApprovedRound(legacy: 2, vulnerable: 2, @new: 3);
        var fake = new FakeApi(HttpStatusCode.OK, "end_turn", Paragraph);

        var draft = await Announcement(fake, run.Id);

        draft.Source.Should().Be("claude");
        draft.Text.Should().Contain("วิธีจัดสรร\nพื้นที่นี้ใช้การจับฉลากแยกตามกลุ่ม ไม่ใช่ใครมาก่อนได้ก่อน", "the paragraph is set on one line")
            .And.Contain($"เลขอ้างอิง {run.ReferenceNo} · เลขตั้งต้นการสุ่ม (seed) {run.Seed}").And.Contain(run.InputHash)
            .And.Contain("- ผู้ค้าเดิม: ผู้เข้าจับฉลาก 2 คน · โควตา 2 · ได้สิทธิ์ 2").And.Contain("ไม่มีช่องทางจ่ายเงินเพื่อได้แผง");

        var body = fake.Bodies.Should().ContainSingle().Subject;
        body["system"]![0]!["text"]!.GetValue<string>().Should().Contain("ห้ามใช้คำว่า \"จอง\"").And.Contain("ห้ามใส่จำนวนผู้ได้สิทธิ์");
        var data = body["messages"]![0]!["content"]!.GetValue<string>();
        data.Should().Contain("ผู้ค้าเดิมไม่เกิน").And.NotContain(run.ReferenceNo).And.NotContain(run.InputHash)
            .And.NotContain(run.Seed.ToString());
        foreach (var o in run.Outcomes)
            data.Should().NotContain(o.VendorName).And.NotContain(o.VendorId.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "end_turn", "ยื่นจองแผงได้ทุกคน")]
    [InlineData(HttpStatusCode.OK, "end_turn", "   ")]
    [InlineData(HttpStatusCode.OK, "refusal", "ข้อความ")]
    [InlineData(HttpStatusCode.InternalServerError, "end_turn", "ข้อความ")]
    public async Task A_failed_or_unsafe_paragraph_falls_back_to_the_template(HttpStatusCode status, string stop, string text)
    {
        var (_, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 1);

        var draft = await Announcement(new FakeApi(status, stop, text), run.Id);

        draft.Source.Should().Be("template");
        draft.Text.Should().Contain("จัดสรรด้วยการจับฉลากแยกตามกลุ่ม").And.NotContain("จอง");
    }

    [Fact]
    public async Task An_overlong_paragraph_falls_back_to_the_template()
    {
        var (_, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 1);

        (await Announcement(new FakeApi(HttpStatusCode.OK, "end_turn", new string('ก', 701)), run.Id)).Source.Should().Be("template");
    }

    private async Task<AnnouncementTests.AnnouncementDto> Announcement(FakeApi fake, Guid runId)
    {
        await using var withClaude = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Assistant:ApiKey", "test-key");
            b.UseSetting("Assistant:Model", "claude-test-model");
            b.ConfigureTestServices(s => s.AddHttpClient<ClaudeTextModel>().ConfigurePrimaryHttpMessageHandler(() => fake));
        });
        var client = withClaude.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Role", "officer");
        client.DefaultRequestHeaders.Add("X-Demo-User", "officer-bangphlat");
        return await AnnouncementTests.Announcement(client, runId);
    }

    private sealed class FakeApi(HttpStatusCode status, string stop, string text) : HttpMessageHandler
    {
        public List<JsonNode> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!);
            return new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(new JsonObject
                {
                    ["type"] = "message",
                    ["stop_reason"] = stop,
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
                }),
            };
        }
    }
}
