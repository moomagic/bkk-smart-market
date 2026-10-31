using FluentAssertions;
using SmartMarket.Application.Allocation;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Tests.Allocation;

public sealed class PublicAnnouncementTests
{
    private static AnnouncementFacts Facts(bool pooled, params (Category Category, int Base, int Allocated, int Drawn)[] groups) => new(
        "ทดสอบ", new DateOnly(2026, 11, 1), new DateOnly(2027, 4, 30), new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero),
        "BPL-2569-1025-01", 42, new string('a', 64), new RulesSnapshot(0.50m, 0.25m, 0.25m, 3), pooled ? 1 : 4, pooled,
        [.. groups.Select(g => new QuotaLine(g.Category, g.Base, g.Allocated))], groups.ToDictionary(g => g.Category, g => g.Drawn));

    [Fact]
    public void Rollover_is_spelled_out_on_both_sides_and_dates_are_Buddhist_era()
    {
        var f = Facts(false, (Category.Legacy, 2, 1, 1), (Category.Vulnerable, 1, 2, 3), (Category.New, 1, 1, 4));

        var text = PublicAnnouncement.Compose(f, PublicAnnouncement.Method(f));

        text.Should().Contain("รอบ 1 พ.ย. 2569 – 30 เม.ย. 2570 · อนุมัติผลเมื่อ 25 ต.ค. 2569")
            .And.Contain("- ผู้ค้าเดิม: ผู้เข้าจับฉลาก 1 คน · โควตา 2 · ได้สิทธิ์ 1 (โอนโควตาออก 1)")
            .And.Contain("- กลุ่มเปราะบาง: ผู้เข้าจับฉลาก 3 คน · โควตา 1 · ได้สิทธิ์ 2 (รับโอนโควตา +1)")
            .And.Contain("ผู้ค้าเดิมไม่เกิน 50%").And.Contain("สำรองกลุ่มละไม่เกิน 3 คน")
            .And.NotContain("จอง");
    }

    [Fact]
    public void A_one_lock_space_says_it_was_one_pooled_draw_and_lists_only_the_winning_group()
    {
        var f = Facts(true, (Category.Legacy, 0, 0, 1), (Category.Vulnerable, 0, 1, 1), (Category.New, 0, 0, 2));

        var text = PublicAnnouncement.Compose(f, PublicAnnouncement.Method(f));

        text.Should().Contain("จับฉลากรวมทุกกลุ่ม").And.Contain("- กลุ่มเปราะบาง: ผู้เข้าจับฉลาก 1 คน · ได้สิทธิ์ 1")
            .And.NotContain("- ผู้ค้าเดิม").And.NotContain("โควตา 0");
    }
}
