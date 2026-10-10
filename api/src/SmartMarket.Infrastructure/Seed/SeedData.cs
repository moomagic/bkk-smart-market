using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Infrastructure.Seed;

/// <summary>
/// Mock data for the เขตบางพลัด pilot (see CLAUDE.md › Seed data). All IDs are fixed constants:
/// the allocation engine sorts by VendorId, so random IDs would change who wins for a given seed.
/// Seeded through EF HasData, so it bypasses the audit log by design.
/// </summary>
public static class SeedData
{
    public static readonly Guid BangPhlat = new("10000000-0000-0000-0000-000000000001");

    public static readonly Guid PakSoiCharan79 = new("20000000-0000-0000-0000-000000000001");
    public static readonly Guid MrtBangPhlatExit1 = new("20000000-0000-0000-0000-000000000002");
    public static readonly Guid BangPhlatMarket = new("20000000-0000-0000-0000-000000000003");
    public static readonly Guid Rama8BridgeYard = new("20000000-0000-0000-0000-000000000004");

    public static readonly Guid Somsri = VendorId(1);

    private static readonly DateTimeOffset ConsentAt = new(2026, 9, 1, 2, 0, 0, TimeSpan.Zero);

    public static District[] Districts =>
    [
        new(BangPhlat, "บางพลัด"),
    ];

    public static Space[] Spaces =>
    [
        new(PakSoiCharan79, BangPhlat, "ปากซอยจรัญฯ 79", SpaceType.Relaxation,
            ["อาหาร"], "ทุกวัน 16:00–22:00", "docs/rules"),
        new(MrtBangPhlatExit1, BangPhlat, "หน้า MRT บางพลัด ทางออก 1", SpaceType.Relaxation,
            ["อาหาร", "เครื่องดื่ม"], "ทุกวัน 06:00–10:00, 16:00–21:00", "docs/rules"),
        new(BangPhlatMarket, BangPhlat, "ตลาดนัดเขตบางพลัด", SpaceType.DistrictMarket,
            ["ทั่วไป"], "ศ–อา 15:00–22:00", "docs/rules"),
        new(Rama8BridgeYard, BangPhlat, "ลานใต้สะพานพระราม 8", SpaceType.TrialArea,
            ["ทั่วไป"], "ส–อา 16:00–22:00", "docs/rules"),
    ];

    public static Lock[] Locks =>
    [
        .. LocksFor(1, PakSoiCharan79, 4),
        .. LocksFor(2, MrtBangPhlatExit1, 5),
        .. LocksFor(3, BangPhlatMarket, 20),
        .. LocksFor(4, Rama8BridgeYard, 6),
    ];

    /// <summary>ผู้สมัครปากซอยจรัญฯ 79 (CLAUDE.md). Names and phones are fictional.</summary>
    public static Vendor[] Vendors =>
    [
        Food(1, "สมศรี ใจดี", "ข้าวแกง"),
        Food(2, "บุญมี รักไทย", "ก๋วยเตี๋ยว"),
        Food(3, "มาลี สุขใจ", "ขนมไทย", "ผู้สูงอายุ"),
        Food(4, "อนุชา กล้าหาญ", "น้ำผลไม้", "ผู้พิการ"),
        Food(5, "วรรณา ทองดี", "ส้มตำ", "ผู้สูงอายุ"),
        Food(6, "ธนพล จันทร์เพ็ญ", "ข้าวมันไก่"),
        Food(7, "ปิยะ นาคสวัสดิ์", "หมูปิ้ง"),
        Food(8, "สุดา พรหมมา", "ข้าวเหนียวมะม่วง"),
        Food(9, "กิตติ วงศ์ใหญ่", "ลูกชิ้นทอด"),
        Food(10, "ศิริ มั่นคง", "กาแฟโบราณ"),
        Food(11, "ชัย ดวงดี", "ไก่ทอด"),
    ];

    // ---- Rounds (CLAUDE.md: current round 1 พ.ย. 2569 – 30 เม.ย. 2570, closes 31 ต.ค. 2569) ----

    public static readonly Guid CharanCurrentRound = RoundId(1);
    public static readonly Guid MrtCurrentRound = RoundId(2);
    public static readonly Guid MarketCurrentRound = RoundId(3);
    public static readonly Guid Rama8CurrentRound = RoundId(4);

    /// <summary>
    /// Previous round at ปากซอยจรัญฯ 79, where สมศรี ใ. and บุญมี ร. held locks — that is what makes them Legacy now.
    /// Hand-written (no AllocationRun); engine-generated history for the other spaces comes with the dashboard (#23).
    /// </summary>
    public static readonly Guid CharanPreviousRound = new("50000000-0000-0000-0001-000000000001");

    private static readonly TimeSpan Bangkok = TimeSpan.FromHours(7);
    public static readonly DateTimeOffset CurrentApplyOpensAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, Bangkok).ToUniversalTime();
    public static readonly DateTimeOffset CurrentApplyClosesAt = new DateTimeOffset(2026, 10, 31, 23, 59, 59, Bangkok).ToUniversalTime();

    public static Round[] Rounds =>
    [
        Current(CharanCurrentRound, PakSoiCharan79, months: 6),
        Current(MrtCurrentRound, MrtBangPhlatExit1, months: 6),
        Current(MarketCurrentRound, BangPhlatMarket, months: 6),
        Current(Rama8CurrentRound, Rama8BridgeYard, months: 1),
        new(CharanPreviousRound, PakSoiCharan79, new DateOnly(2026, 5, 1), new DateOnly(2026, 10, 31),
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, Bangkok).ToUniversalTime(),
            new DateTimeOffset(2026, 4, 30, 23, 59, 59, Bangkok).ToUniversalTime(), RoundStatus.Approved),
    ];

    /// <summary>
    /// ปากซอยจรัญฯ 79 current round: 10 of the 11 applicants. สมศรี ใ. (vendor 1) submits live in the demo.
    /// </summary>
    public static StallRequest[] StallRequests =>
    [
        new(RequestId(0, 1), CharanPreviousRound, VendorId(1), At(2026, 4, 3), Category.Legacy, StallRequestStatus.Confirmed),
        new(RequestId(0, 2), CharanPreviousRound, VendorId(2), At(2026, 4, 5), Category.Legacy, StallRequestStatus.Confirmed),
        Submitted(2, Category.Legacy, day: 2),
        Submitted(3, Category.Vulnerable, day: 3),
        Submitted(4, Category.Vulnerable, day: 3),
        Submitted(5, Category.Vulnerable, day: 6),
        Submitted(6, Category.New, day: 7),
        Submitted(7, Category.New, day: 9),
        Submitted(8, Category.New, day: 12),
        Submitted(9, Category.New, day: 14),
        Submitted(10, Category.New, day: 18),
        Submitted(11, Category.New, day: 20),
    ];

    public static Guid VendorId(int n) => new($"40000000-0000-0000-0000-{n:000000000000}");

    private static Vendor Food(int n, string name, string product, string? vulnerableReason = null) =>
        new(VendorId(n), name, $"080-000-{n:0000}", vulnerableReason, product,
            foodHygieneTrained: true, bannedUntil: null, ConsentAt);

    private static Guid RoundId(int n) => new($"50000000-0000-0000-0000-{n:000000000000}");

    private static Guid RequestId(int round, int vendor) => new($"60000000-0000-0000-{round:0000}-{vendor:000000000000}");

    private static DateTimeOffset At(int y, int m, int d) => new DateTimeOffset(y, m, d, 10, 0, 0, Bangkok).ToUniversalTime();

    private static Round Current(Guid id, Guid spaceId, int months)
    {
        var starts = new DateOnly(2026, 11, 1);
        return new Round(id, spaceId, starts, starts.AddMonths(months).AddDays(-1),
            CurrentApplyOpensAt, CurrentApplyClosesAt, RoundStatus.Open);
    }

    private static StallRequest Submitted(int vendor, Category category, int day) =>
        new(RequestId(1, vendor), CharanCurrentRound, VendorId(vendor), At(2026, 10, day), category);

    private static IEnumerable<Lock> LocksFor(int space, Guid spaceId, int count) =>
        Enumerable.Range(1, count).Select(i =>
            new Lock(new Guid($"30000000-0000-0000-{space:0000}-{i:000000000000}"), spaceId, $"B{i}"));
}
