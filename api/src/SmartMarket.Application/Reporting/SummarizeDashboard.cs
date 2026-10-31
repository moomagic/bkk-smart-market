using System.Globalization;
using SmartMarket.Application.Common;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Reporting;

/// <param name="Source">"template" (no model, or it failed) or "claude".</param>
public sealed record DashboardSummaryDto(IReadOnlyList<string> Lines, string Source, DateTimeOffset GeneratedAt);

/// <summary>Insight flags of one space's counted round, by kind; counts only — no names (PDPA).</summary>
/// <param name="ToCheck">Warning and Critical flags: what an officer should look into.</param>
public sealed record SpaceFlags(Guid SpaceId, int ToCheck, IReadOnlyDictionary<InsightKind, int> ByKind);

/// <summary>Everything a summary may say: the dashboard's figures and flag counts. Nothing about any one vendor.</summary>
public sealed record SummaryFacts(DashboardKpis Kpis, IReadOnlyList<SpaceFlags> Flags);

/// <summary>
/// The executive summary on /dashboard: four Thai lines read off the figures on the page — overview, the space to look
/// at, how opportunity is spread, how the stalls are actually used. No targets, no scores, no names (CLAUDE.md › AI
/// components; Guardrail 1). Asked for, never made on load. A read: no audit entry.
/// </summary>
public sealed class SummarizeDashboardHandler(GetDashboardHandler dashboard, GetRoundInsightsHandler insights, IClock clock)
{
    public const string TemplateSource = "template";

    public async Task<DashboardSummaryDto> Handle(Guid? districtId, CancellationToken ct)
    {
        var facts = await Facts(districtId, ct);
        return new DashboardSummaryDto(DashboardSummary.Template(facts), TemplateSource, clock.Now);
    }

    private async Task<SummaryFacts> Facts(Guid? districtId, CancellationToken ct)
    {
        var kpis = await dashboard.Handle(districtId, ct);
        var flags = new List<SpaceFlags>();
        foreach (var space in kpis.Spaces.Where(s => s.RoundId is not null))
        {
            var found = await insights.Handle(space.RoundId!.Value, ct);
            var list = found.IsSuccess ? found.Value : [];
            flags.Add(new SpaceFlags(space.SpaceId, list.Count(f => f.Severity != InsightSeverity.Info),
                list.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Count())));
        }
        return new SummaryFacts(kpis, flags);
    }
}

/// <summary>The summary without a model: fixed wording, the same four lines for the same figures.</summary>
public static class DashboardSummary
{
    private static readonly IReadOnlyDictionary<Category, string> Group = new Dictionary<Category, string>
    {
        [Category.Legacy] = "ผู้ค้าเดิม", [Category.Vulnerable] = "กลุ่มเปราะบาง", [Category.New] = "ผู้ค้าใหม่",
    };

    /// <summary>The portal's labels, by kind name (SharedDevice arrives with #71 on another branch).</summary>
    private static readonly IReadOnlyDictionary<string, string> Kind = new Dictionary<string, string>
    {
        [nameof(InsightKind.Absent)] = "ขาดเช็คอิน",
        [nameof(InsightKind.LowCheckIn)] = "เช็คอินต่ำกว่าเกณฑ์",
        [nameof(InsightKind.OutsideGeofence)] = "เช็คอินนอกพื้นที่",
        [nameof(InsightKind.SharedSurname)] = "นามสกุลซ้ำ",
        [nameof(InsightKind.SharedPhone)] = "เบอร์โทรซ้ำ",
        ["SharedDevice"] = "อุปกรณ์ใช้ร่วมกัน",
    };

    public static IReadOnlyList<string> Template(SummaryFacts f)
    {
        var k = f.Kpis;
        var flags = f.Flags.ToDictionary(x => x.SpaceId);
        var allocated = k.Spaces.Where(s => s.ReferenceNo is not null).ToList();
        var notAllocated = k.Spaces.Where(s => s.ReferenceNo is null).Select(s => s.Name).ToList();

        var overview = $"ภาพรวม: ใช้พื้นที่ {Pct(k.Occupancy)} ({k.Occupancy.Count}/{k.Occupancy.Of} ล็อคมีผู้ยืนยันสิทธิ์)"
            + (k.LeadTime.MedianDays is { } days ? $" · ประกาศผลใช้เวลามัธยฐาน {Num(days)} วัน จาก {k.LeadTime.Runs} รอบ" : "")
            + (notAllocated.Count > 0 ? $" · ยังไม่มีผลจัดสรรที่อนุมัติ: {string.Join(", ", notAllocated)}" : "");

        // Most flags to check first, then the emptiest, then by name: the same space for the same figures
        var look = allocated
            .Select(s => (Space: s, ToCheck: flags.TryGetValue(s.SpaceId, out var x) ? x.ToCheck : 0))
            .OrderByDescending(x => x.ToCheck)
            .ThenBy(x => x.Space.Locks == 0 ? 1m : (decimal)x.Space.Confirmed / x.Space.Locks)
            .ThenBy(x => x.Space.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        var where = look.Space is null || (look.ToCheck == 0 && look.Space.Confirmed == look.Space.Locks)
            ? "พื้นที่ที่ควรดู: ไม่มีพื้นที่ที่มีข้อสังเกตหรือล็อคว่าง"
            : $"พื้นที่ที่ควรดู: {look.Space.Name} — ใช้พื้นที่ {Pct(Ratio.From(look.Space.Confirmed, look.Space.Locks))} "
              + $"({look.Space.Confirmed}/{look.Space.Locks} ล็อค) · ข้อสังเกตที่ต้องตรวจ {look.ToCheck} รายการ";

        var quota = Group.Keys.Select(c =>
        {
            var lines = allocated.SelectMany(s => s.Quota).Where(q => q.Category == c).ToList();
            return $"{Group[c]} {lines.Sum(q => q.Winners)}/{lines.Sum(q => q.Base)}";
        });
        var spread = $"การกระจายโอกาส: ผู้ค้าหน้าใหม่ {Pct(k.NewVendors)} · ไม่ได้ถือล็อคที่เดิมรอบก่อน {Pct(k.FairRotation)} · "
            + $"ได้สิทธิ์จริงเทียบโควตา {string.Join(" · ", quota)}";

        var byKind = f.Flags.SelectMany(x => x.ByKind).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
        var total = byKind.Values.Sum();
        var use = total == 0
            ? "การใช้แผงจริง: ไม่มีข้อสังเกตการใช้แผงในรอบที่นับ"
            : $"การใช้แผงจริง: ข้อสังเกต {total} รายการใน {f.Flags.Count(x => x.ByKind.Count > 0)} พื้นที่ — "
              + string.Join(" · ", byKind.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => $"{Label(x.Key)} {x.Value}"))
              + " · ให้เจ้าหน้าที่ตรวจหน้างาน ระบบไม่ตัดสิทธิ์เอง";

        return [overview, where, spread, use];
    }

    private static string Label(InsightKind kind) => Kind.TryGetValue(kind.ToString(), out var label) ? label : kind.ToString();

    private static string Pct(Ratio r) => r.Percent is { } p ? $"{p.ToString("0.0", CultureInfo.InvariantCulture)}%" : "–";

    private static string Num(decimal d) => d.ToString("0.#", CultureInfo.InvariantCulture);
}
