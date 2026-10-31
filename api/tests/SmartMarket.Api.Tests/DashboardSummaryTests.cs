using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DashboardSummaryTests(ApiFactory api)
{
    internal sealed record SummaryDto(string[] Lines, string Source, DateTimeOffset GeneratedAt);

    [Fact]
    public async Task Without_a_key_the_executive_gets_four_fixed_lines_read_off_the_seeded_figures()
    {
        var summary = await Summary(api.ClientAs("executive", "executive-bkk"));

        summary.Source.Should().Be("template");
        summary.Lines.Should().Equal(
            "ภาพรวม: ใช้พื้นที่ 88.6% (31/35 ล็อคมีผู้ยืนยันสิทธิ์) · ประกาศผลใช้เวลามัธยฐาน 3 วัน จาก 3 รอบ · ยังไม่มีผลจัดสรรที่อนุมัติ: ปากซอยจรัญฯ 79",
            "พื้นที่ที่ควรดู: ตลาดนัดเขตบางพลัด — ใช้พื้นที่ 100.0% (20/20 ล็อค) · ข้อสังเกตที่ต้องตรวจ 5 รายการ",
            "การกระจายโอกาส: ผู้ค้าหน้าใหม่ 35.5% · ไม่ได้ถือล็อคที่เดิมรอบก่อน 71.0% · ได้สิทธิ์จริงเทียบโควตา ผู้ค้าเดิม 9/13 · กลุ่มเปราะบาง 11/9 · ผู้ค้าใหม่ 11/9",
            "การใช้แผงจริง: ข้อสังเกต 13 รายการใน 3 พื้นที่ — เช็คอินต่ำกว่าเกณฑ์ 6 · เช็คอินนอกพื้นที่ 3 · ขาดเช็คอิน 2 · นามสกุลซ้ำ 1 · เบอร์โทรซ้ำ 1 · ให้เจ้าหน้าที่ตรวจหน้างาน ระบบไม่ตัดสิทธิ์เอง");
        summary.Lines.Should().NotContain(l => l.Contains("เป้า") || l.Contains("คะแนน"), "no targets, no scores");
    }

    [Fact]
    public async Task Summarizing_writes_nothing_and_vendors_cannot_ask()
    {
        var since = await api.AuditMark();

        var first = await Summary(api.Officer());
        var again = await Summary(api.Officer());

        again.Lines.Should().Equal(first.Lines, "the same figures give the same lines");
        (await api.AuditSince(since)).Should().BeEmpty();
        (await api.Vendor(Guid.NewGuid()).PostAsync($"/api/dashboard/summary?districtId={SeedData.BangPhlat}", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    internal static async Task<SummaryDto> Summary(HttpClient client)
    {
        var response = await client.PostAsync($"/api/dashboard/summary?districtId={SeedData.BangPhlat}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<SummaryDto>())!;
    }
}
