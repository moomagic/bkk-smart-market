using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Claude;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

/// <summary>The dashboard summary written by Claude (fake Messages API): figures in, four lines out, the template otherwise.</summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardSummaryClaudeTests(ApiFactory api)
{
    private const string FourLines = """
        ภาพรวม: ใช้พื้นที่ 88.6%
        - พื้นที่ที่ควรดู: ตลาดนัดเขตบางพลัด
        2. การกระจายโอกาส: ผู้ค้าหน้าใหม่ 35.5%
        การใช้แผงจริง: ข้อสังเกต 13 รายการ
        """;

    [Fact]
    public async Task With_a_key_claude_writes_the_four_lines_from_figures_that_name_no_one()
    {
        var fake = new FakeApi(HttpStatusCode.OK, "end_turn", FourLines);

        var summary = await Summary(fake);

        summary.Source.Should().Be("claude");
        summary.Lines.Should().Equal("ภาพรวม: ใช้พื้นที่ 88.6%", "พื้นที่ที่ควรดู: ตลาดนัดเขตบางพลัด",
            "การกระจายโอกาส: ผู้ค้าหน้าใหม่ 35.5%", "การใช้แผงจริง: ข้อสังเกต 13 รายการ");

        var body = fake.Bodies.Should().ContainSingle().Subject;
        body["model"]!.GetValue<string>().Should().Be("claude-test-model");
        var instructions = body["system"]![0]!["text"]!.GetValue<string>();
        instructions.Should().Contain("4 บรรทัด").And.Contain("ห้ามตั้งเป้า").And.Contain("ห้ามแนะนำให้ตัดสิทธิ์").And.Contain("ข้อมูลจำลอง");
        var data = body["messages"]![0]!["content"]!.GetValue<string>();
        data.Should().Contain("ตลาดนัดเขตบางพลัด").And.Contain("88.6").And.Contain("ขาดเช็คอิน");

        await using var scope = api.Services.CreateAsyncScope();
        var vendors = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Vendors
            .Select(v => new { v.Id, v.MaskedName, v.DisplayName, v.Phone }).ToListAsync();
        foreach (var v in vendors)
            data.Should().NotContain(v.MaskedName).And.NotContain(v.DisplayName).And.NotContain(v.Phone).And.NotContain(v.Id.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "end_turn", "บรรทัดเดียว")]
    [InlineData(HttpStatusCode.OK, "end_turn", "1\n2\n3\n4\n5")]
    [InlineData(HttpStatusCode.OK, "refusal", "")]
    [InlineData(HttpStatusCode.InternalServerError, "end_turn", "")]
    public async Task A_failed_or_misshapen_reply_falls_back_to_the_template(HttpStatusCode status, string stop, string text)
    {
        var summary = await Summary(new FakeApi(status, stop, text));

        summary.Source.Should().Be("template");
        summary.Lines.Should().HaveCount(4).And.Subject.First().Should().StartWith("ภาพรวม: ใช้พื้นที่");
    }

    private async Task<DashboardSummaryTests.SummaryDto> Summary(FakeApi fake)
    {
        await using var withClaude = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Assistant:ApiKey", "test-key");
            b.UseSetting("Assistant:Model", "claude-test-model");
            b.ConfigureTestServices(s => s.AddHttpClient<ClaudeTextModel>().ConfigurePrimaryHttpMessageHandler(() => fake));
        });
        var client = withClaude.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-Role", "executive");
        client.DefaultRequestHeaders.Add("X-Demo-User", "executive-bkk");
        var response = await client.PostAsync($"/api/dashboard/summary?districtId={SeedData.BangPhlat}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DashboardSummaryTests.SummaryDto>())!;
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
