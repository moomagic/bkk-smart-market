using FluentAssertions;
using SmartMarket.Application.Reporting;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Tests.Reporting;

public sealed class DashboardSummaryTests
{
    private static SpaceRow Space(string name, int locks, int confirmed, bool allocated = true) =>
        new(Guid.NewGuid(), name, allocated ? Guid.NewGuid() : null, null, null, allocated ? "BPL-2569-0101-01" : null,
            locks, confirmed, confirmed, allocated ? [new QuotaActual(Category.Legacy, 1, 1), new QuotaActual(Category.New, 1, 0)] : []);

    private static SummaryFacts Facts(SpaceRow[] spaces, params (SpaceRow Space, int ToCheck, InsightKind Kind)[] flags) => new(
        new DashboardKpis(Ratio.From(spaces.Sum(s => s.Confirmed), spaces.Sum(s => s.Locks)), new LeadTime(null, 0),
            Ratio.From(0, 1), Ratio.From(1, 1), spaces),
        [.. flags.Select(f => new SpaceFlags(f.Space.SpaceId, f.ToCheck, new Dictionary<InsightKind, int> { [f.Kind] = f.ToCheck }))]);

    [Fact]
    public void The_space_with_most_flags_to_check_comes_first_even_when_full()
    {
        var full = Space("เต็ม", 4, 4);
        var empty = Space("ว่าง", 4, 1);

        var lines = DashboardSummary.Template(Facts([full, empty], (full, 2, InsightKind.Absent)));

        lines.Should().HaveCount(4);
        lines[1].Should().StartWith("พื้นที่ที่ควรดู: เต็ม").And.Contain("ข้อสังเกตที่ต้องตรวจ 2 รายการ");
    }

    [Fact]
    public void Without_flags_the_emptiest_space_comes_first_and_a_full_district_says_so()
    {
        var full = Space("เต็ม", 4, 4);
        DashboardSummary.Template(Facts([full, Space("ว่าง", 4, 1)]))[1].Should().StartWith("พื้นที่ที่ควรดู: ว่าง — ใช้พื้นที่ 25.0%");
        DashboardSummary.Template(Facts([full]))[1].Should().Be("พื้นที่ที่ควรดู: ไม่มีพื้นที่ที่มีข้อสังเกตหรือล็อคว่าง");
    }

    [Fact]
    public void No_flags_and_no_lead_time_are_stated_plainly_and_unallocated_spaces_are_named()
    {
        var lines = DashboardSummary.Template(Facts([Space("เต็ม", 4, 4), Space("ใหม่", 3, 0, allocated: false)]));

        lines[0].Should().Be("ภาพรวม: ใช้พื้นที่ 57.1% (4/7 ล็อคมีผู้ยืนยันสิทธิ์) · ยังไม่มีผลจัดสรรที่อนุมัติ: ใหม่");
        lines[3].Should().Be("การใช้แผงจริง: ไม่มีข้อสังเกตการใช้แผงในรอบที่นับ");
    }
}
