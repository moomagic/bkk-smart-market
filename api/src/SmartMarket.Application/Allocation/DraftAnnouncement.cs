using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <param name="Source">"template", or "claude" once a model writes the method paragraph (#76).</param>
public sealed record AnnouncementDto(Guid RunId, string Text, string Source);

/// <summary>What a public announcement may say about a run: the round, the rules and the draw — never a person.</summary>
public sealed record AnnouncementFacts(
    string SpaceName, DateOnly StartsOn, DateOnly EndsOn, DateTimeOffset ApprovedAt, string ReferenceNo, int Seed, string InputHash,
    RulesSnapshot Rules, int Locks, bool PooledDraw, IReadOnlyList<QuotaLine> Quota, IReadOnlyDictionary<Category, int> Drawn);

/// <summary>
/// Public announcement (CONTEXT.md): a draft of the approved run's announcement for the officer to check and publish
/// themselves — the system publishes nothing. It states the draw record (not the result after forfeits), so it matches
/// what anyone can replay-verify, and names no vendor (PDPA). A read: no audit entry.
/// </summary>
public sealed class DraftAnnouncementHandler(IAppDbContext db)
{
    public const string TemplateSource = "template";

    public async Task<Result<AnnouncementDto>> Handle(Guid runId, CancellationToken ct)
    {
        var row = await (
            from run in db.AllocationRuns.AsNoTracking()
            join r in db.Rounds on run.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where run.Id == runId
            select new { Run = run, r.StartsOn, r.EndsOn, SpaceName = s.Name }).SingleOrDefaultAsync(ct);
        if (row is null)
            return Error.NotFound("allocation_run.not_found", "ไม่พบผลการจัดสรรนี้");
        if (row.Run.Status != AllocationRunStatus.Approved || row.Run.DecidedAt is null)
            return Error.Conflict("allocation_run.not_approved", "ร่างประกาศได้เฉพาะผลจัดสรรที่อนุมัติแล้ว");

        var result = AllocationJson.Deserialize<AllocationResult>(row.Run.ResultJson);
        var facts = new AnnouncementFacts(
            row.SpaceName, row.StartsOn, row.EndsOn, row.Run.DecidedAt.Value, row.Run.ReferenceNo, row.Run.Seed, row.Run.InputHash,
            AllocationJson.Deserialize<RulesSnapshot>(row.Run.RulesSnapshot),
            result.Outcomes.Count(o => o.LockCode is not null) + result.UnassignedLockCodes.Count, result.PooledDraw, result.Quota,
            result.Outcomes.GroupBy(o => o.Category).ToDictionary(g => g.Key, g => g.Count()));
        return new AnnouncementDto(runId, PublicAnnouncement.Compose(facts, PublicAnnouncement.Method(facts)), TemplateSource);
    }
}

/// <summary>
/// The announcement's text. The figures that make it verifiable — quota, reference number, seed, input hash — are always
/// set here from the run, whoever writes the plain-language method paragraph.
/// </summary>
public static class PublicAnnouncement
{
    private static readonly CultureInfo Thai = new("th-TH"); // Buddhist calendar: 1 พ.ย. 2569
    private static readonly TimeSpan Bangkok = TimeSpan.FromHours(7);

    private static readonly IReadOnlyDictionary<Category, string> Group = new Dictionary<Category, string>
    {
        [Category.Legacy] = "ผู้ค้าเดิม", [Category.Vulnerable] = "กลุ่มเปราะบาง", [Category.New] = "ผู้ค้าใหม่",
    };

    /// <summary>How the draw works, in plain words, from the rules the run used.</summary>
    public static string Method(AnnouncementFacts f)
    {
        var r = f.Rules;
        return f.PooledDraw
            ? $"พื้นที่นี้มี 1 ล็อค จึงจับฉลากรวมทุกกลุ่มในครั้งเดียว ไม่ใช่ใครมาก่อนได้ก่อน "
              + $"ผู้ที่ไม่ได้สิทธิ์รอบก่อนที่พื้นที่นี้มีโอกาส 2 เท่า และมีสำรองตามลำดับที่จับได้"
            : $"จัดสรรด้วยการจับฉลากแยกตามกลุ่ม ไม่ใช่ใครมาก่อนได้ก่อน "
              + $"โควตา: ผู้ค้าเดิมไม่เกิน {Pct(r.LegacyMax)} · กลุ่มเปราะบางอย่างน้อย {Pct(r.VulnerableMin)} · ผู้ค้าใหม่อย่างน้อย {Pct(r.NewMin)} "
              + $"ถ้ากลุ่มใดมีผู้สมัครไม่พอ โควตาที่เหลือโอนไปกลุ่มถัดไป "
              + $"ผู้ที่ไม่ได้สิทธิ์รอบก่อนที่พื้นที่นี้มีโอกาส 2 เท่า · สำรองกลุ่มละไม่เกิน {r.WaitlistPerGroup} คน · "
              + $"รหัสล็อคสุ่มสลับก่อนมอบให้ผู้ได้สิทธิ์";
    }

    public static string Compose(AnnouncementFacts f, string method)
    {
        var lines = new List<string>
        {
            $"ประกาศผลการจัดสรรพื้นที่ค้าขาย · {f.SpaceName}",
            $"รอบ {Date(f.StartsOn)} – {Date(f.EndsOn)} · อนุมัติผลเมื่อ {Date(DateOnly.FromDateTime(f.ApprovedAt.ToOffset(Bangkok).DateTime))}",
            "",
            "วิธีจัดสรร",
            method.Trim(),
            "",
            $"ผลรวม ({f.Locks} ล็อค · ผู้ผ่านการตรวจสิทธิที่เข้าจับฉลาก {f.Drawn.Values.Sum()} คน)",
        };
        foreach (var q in f.Quota.Where(q => !f.PooledDraw || q.Allocated > 0))
            lines.Add($"- {Group[q.Category]}: ผู้เข้าจับฉลาก {f.Drawn.GetValueOrDefault(q.Category)} คน · "
                      + (f.PooledDraw ? $"ได้สิทธิ์ {q.Allocated}" : $"โควตา {q.Base} · ได้สิทธิ์ {q.Allocated}")
                      + (q.Allocated > q.Base && !f.PooledDraw ? $" (รับโอนโควตา +{q.Allocated - q.Base})" : "")
                      + (q.Allocated < q.Base ? $" (โอนโควตาออก {q.Base - q.Allocated})" : ""));
        lines.AddRange([
            "",
            "ตรวจซ้ำได้",
            $"เลขอ้างอิง {f.ReferenceNo} · เลขตั้งต้นการสุ่ม (seed) {f.Seed.ToString(CultureInfo.InvariantCulture)}",
            $"ค่าแฮชข้อมูลนำเข้า (SHA-256) {f.InputHash}",
            "รันจับฉลากซ้ำด้วย seed และข้อมูลชุดเดิมจะได้ผลเดิมทุกครั้ง ผู้ยื่นคำขอกด \"ตรวจซ้ำด้วย seed เดิม\" ได้เองที่หน้าคำขอในแอป",
            "",
            "ไม่มีช่องทางจ่ายเงินเพื่อได้แผง หากมีผู้เรียกรับเงิน แจ้งสำนักงานเขตบางพลัด",
            "(ข้อมูลจำลอง · ระบบต้นแบบ)",
        ]);
        return string.Join('\n', lines);
    }

    private static string Date(DateOnly d) => d.ToString("d MMM yyyy", Thai);

    private static string Pct(decimal share) => $"{Math.Round(share * 100m).ToString(CultureInfo.InvariantCulture)}%";
}
