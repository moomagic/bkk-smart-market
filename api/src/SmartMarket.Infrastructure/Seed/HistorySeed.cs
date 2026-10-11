using Microsoft.EntityFrameworkCore;
using SmartMarket.Application.Allocation;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.CheckIns;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Persistence;

namespace SmartMarket.Infrastructure.Seed;

/// <summary>
/// The previous round (mock) of หน้า MRT, ตลาดนัดเขต and ลานใต้สะพานพระราม 8, drawn by the real
/// <see cref="AllocationEngine"/> with fixed seeds, approved and confirmed, plus check-ins for the last three months —
/// so the dashboard's numbers have sources that replay (CLAUDE.md › Seed data).
/// Everything is a constant (ids, seeds, dates — never the clock), so a reset reproduces it exactly. It runs after
/// migrations, once: the MRT round is the sentinel. Written without audit entries, like HasData seeding.
/// </summary>
public static class HistorySeed
{
    public static readonly Guid MrtPreviousRound = new("50000000-0000-0000-0002-000000000001");
    public static readonly Guid MarketPreviousRound = new("50000000-0000-0000-0003-000000000001");
    public static readonly Guid Rama8PreviousRound = new("50000000-0000-0000-0004-000000000001");

    private static readonly TimeSpan Bangkok = TimeSpan.FromHours(7);

    /// <summary>Check-ins stop the day before Demo:Now (25 ต.ค. 2569).</summary>
    private static readonly DateOnly CheckInsUntil = new(2026, 10, 24);

    private static readonly DateOnly CheckInsFrom = new(2026, 8, 1);

    private const string Officer = "officer-bangphlat";

    private sealed record Applicants(Category Category, int Count, int Weight = 1);

    /// <param name="Spaces">Space index used in the fixed ids of this space's rows.</param>
    /// <param name="SellingDays">Days of the week the space trades — check-ins happen only then.</param>
    private sealed record History(
        int Spaces, Guid RoundId, Guid SpaceId, DateOnly StartsOn, DateOnly EndsOn, DateTime ApplyOpens, DateTime ApplyCloses,
        DateTime RanAt, DateTime DecidedAt, string ReferenceNo, int Seed, int FirstVendor, Applicants[] Applying,
        DayOfWeek[] SellingDays);

    private static readonly DayOfWeek[] EveryDay = Enum.GetValues<DayOfWeek>();

    /// <summary>
    /// LeadTime (close → approval): MRT 2 days, market 4, พระราม 8 3 — median 3.
    /// Expected before the demo (worked out once with the engine): 31 winners all confirmed of 35 locks;
    /// winners by group MRT L1 V2 N2 · market L8 V7 N5 · พระราม 8 V2 N4.
    /// </summary>
    private static readonly History[] Rounds =
    [
        new(2, MrtPreviousRound, SeedData.MrtBangPhlatExit1, new(2026, 5, 1), new(2026, 10, 31),
            new(2026, 4, 1, 8, 0, 0), new(2026, 4, 20, 17, 0, 0), new(2026, 4, 22, 10, 0, 0), new(2026, 4, 22, 17, 0, 0),
            "BPL-2569-0422-01", 25690501, 101,
            [new(Category.Legacy, 2), new(Category.Vulnerable, 2), new(Category.New, 3), new(Category.New, 2, Weight: 2)],
            EveryDay),
        new(3, MarketPreviousRound, SeedData.BangPhlatMarket, new(2026, 5, 1), new(2026, 10, 31),
            new(2026, 4, 1, 8, 0, 0), new(2026, 4, 20, 17, 0, 0), new(2026, 4, 22, 11, 0, 0), new(2026, 4, 24, 17, 0, 0),
            "BPL-2569-0422-02", 25690502, 111,
            [new(Category.Legacy, 8), new(Category.Vulnerable, 7), new(Category.New, 9), new(Category.New, 2, Weight: 2)],
            [DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday]),
        new(4, Rama8PreviousRound, SeedData.Rama8BridgeYard, new(2026, 10, 1), new(2026, 10, 31),
            new(2026, 9, 1, 8, 0, 0), new(2026, 9, 20, 17, 0, 0), new(2026, 9, 22, 10, 0, 0), new(2026, 9, 23, 17, 0, 0),
            "BPL-2569-0922-01", 25691001, 141,
            [new(Category.Vulnerable, 2), new(Category.New, 6)],
            [DayOfWeek.Saturday, DayOfWeek.Sunday]),
    ];

    private static readonly string[] FirstNames =
    [
        "อรุณ", "บุษบา", "ชาญ", "ดารา", "เอกชัย", "ฟ้าใส", "กมล", "หทัย", "อิทธิ", "จันทร์", "กานดา", "ลำดวน", "มานะ", "นงลักษณ์",
        "อำนาจ", "พรทิพย์", "เกศินี", "รุ่งโรจน์", "สมชาย", "ทองใบ", "อุไร", "วิไล", "ยุพา", "สายสุนีย์", "ประเสริฐ", "เพ็ญศรี",
        "ชูชาติ", "สมหญิง", "บุญเรือน", "จำเนียร", "สุนทร", "ละเอียด", "สมพร", "ประยูร", "สำราญ", "ศรีนวล", "ทวี", "นิภา",
        "สุวรรณ", "เยาวลักษณ์", "ชนิดา", "วีระ", "อัมพร", "สมบัติ", "ปราณี", "ไพโรจน์", "ลัดดา", "สมจิตร",
    ];

    private static readonly string[] LastNames =
    [
        "แสงทอง", "ใจงาม", "มีสุข", "ศรีสวัสดิ์", "บุญมา", "พึ่งตน", "ทองคำ", "รุ่งเรือง", "สุขสม", "เจริญผล", "วงศ์ไทย", "นาคดี",
    ];

    public static async Task ApplyAsync(SmartMarketDbContext db, CancellationToken ct)
    {
        if (await db.Rounds.AnyAsync(r => r.Id == MrtPreviousRound, ct)) return;

        // The rules in force back then, frozen like any run's RulesSnapshot — not today's appsettings, which may change.
        var rules = new RulesSnapshot(0.50m, 0.25m, 0.25m, 3);
        foreach (var h in Rounds)
            await Seed(db, h, rules, ct);
        await db.SaveSeedAsync(ct);
    }

    private static async Task Seed(SmartMarketDbContext db, History h, RulesSnapshot rules, CancellationToken ct)
    {
        var round = new Round(h.RoundId, h.SpaceId, h.StartsOn, h.EndsOn, At(h.ApplyOpens), At(h.ApplyCloses), RoundStatus.Drawn);
        db.Rounds.Add(round);

        var applicants = h.Applying.SelectMany(a => Enumerable.Repeat(a, a.Count)).ToList();
        var requests = new Dictionary<Guid, StallRequest>();
        for (var i = 0; i < applicants.Count; i++)
        {
            var n = h.FirstVendor + i;
            var vendor = new Vendor(SeedData.VendorId(n), $"{FirstNames[n % FirstNames.Length]} {LastNames[n % LastNames.Length]}",
                $"081-100-{n:0000}", applicants[i].Category == Category.Vulnerable ? "ผู้สูงอายุ" : null, "ทั่วไป",
                foodHygieneTrained: true, bannedUntil: null, At(h.ApplyOpens));
            db.Vendors.Add(vendor);
            var request = new StallRequest(Id(7, h.Spaces, n), h.RoundId, vendor.Id, At(h.ApplyOpens.AddDays(1 + i % 10)),
                applicants[i].Category);
            request.MarkEligible(applicants[i].Weight, At(h.RanAt.AddHours(-1)));
            db.StallRequests.Add(request);
            requests[vendor.Id] = request;
        }

        var locks = await db.Locks.Where(l => l.SpaceId == h.SpaceId).ToDictionaryAsync(l => l.Code, l => l.Id, ct);
        var result = AllocationEngine.Allocate(new AllocationInput(
            [.. locks.Keys], [.. requests.Values.Select(q => new Candidate(q.VendorId, q.Category, q.CarryOverWeight, true))],
            h.Seed, rules));
        var json = AllocationJson.Serialize(result);
        var run = new AllocationRun(Id(8, h.Spaces, 1), h.RoundId, h.ReferenceNo, h.Seed, result.InputHash,
            AllocationJson.Serialize(rules), At(h.RanAt), Officer, json);
        run.Approve(At(h.DecidedAt), Officer);
        round.MarkApproved();
        db.AllocationRuns.Add(run);

        var confirmBy = At(h.DecidedAt).AddDays(3);
        var attendance = new Random(h.Seed);
        var assignmentNo = 0;
        foreach (var o in result.Outcomes)
        {
            var request = requests[o.VendorId];
            if (o.Kind == OutcomeKind.Waitlisted) { request.MarkWaitlisted(o.WaitlistPosition!.Value); continue; }
            if (o.Kind == OutcomeKind.NotSelected) { request.MarkNotSelected(); continue; }

            // Every winner confirmed in time — an unconfirmed one would be forfeited (ADR-0004) the first time anyone looks.
            request.MarkWon();
            request.MarkConfirmed();
            var assignment = new Assignment(Id(9, h.Spaces, ++assignmentNo), h.RoundId, o.VendorId, locks[o.LockCode!], confirmBy);
            assignment.Confirm(At(h.DecidedAt).AddDays(1));
            db.Assignments.Add(assignment);
            AddCheckIns(db, h, assignment, assignmentNo, attendance);
        }
    }

    /// <summary>
    /// Selling days from 1 Aug (or the round's start — พระราม 8 is a one-month TrialArea round, so October only) to 24 Oct;
    /// most winners come ~9 days in 10, one in each space far less (material for the usage flags in #25).
    /// </summary>
    private static void AddCheckIns(SmartMarketDbContext db, History h, Assignment assignment, int assignmentNo, Random rng)
    {
        var rate = assignmentNo == 2 ? 0.55 : 0.9;
        var day = h.StartsOn > CheckInsFrom ? h.StartsOn : CheckInsFrom;
        var n = 0;
        for (; day <= CheckInsUntil && day <= h.EndsOn; day = day.AddDays(1))
        {
            if (!h.SellingDays.Contains(day.DayOfWeek) || rng.NextDouble() >= rate) continue;
            var at = new DateTimeOffset(day.ToDateTime(new TimeOnly(16, 0)), Bangkok).AddMinutes(rng.Next(0, 90)).ToUniversalTime();
            db.CheckIns.Add(new CheckIn(new Guid($"71000000-0000-{h.Spaces:0000}-{assignmentNo:0000}-{++n:000000000000}"),
                assignment.Id, at, CheckInMethod.QR, withinGeofence: rng.NextDouble() >= 0.03));
        }
    }

    private static DateTimeOffset At(DateTime bangkok) => new DateTimeOffset(bangkok, Bangkok).ToUniversalTime();

    private static Guid Id(int kind, int space, int n) => new($"{kind}0000000-0000-0000-{space:0000}-{n:000000000000}");
}
