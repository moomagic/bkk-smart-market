using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Spaces;
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

    public static Guid VendorId(int n) => new($"40000000-0000-0000-0000-{n:000000000000}");

    private static Vendor Food(int n, string name, string product, string? vulnerableReason = null) =>
        new(VendorId(n), name, $"080-000-{n:0000}", vulnerableReason, product,
            foodHygieneTrained: true, bannedUntil: null, ConsentAt);

    private static IEnumerable<Lock> LocksFor(int space, Guid spaceId, int count) =>
        Enumerable.Range(1, count).Select(i =>
            new Lock(new Guid($"30000000-0000-0000-{space:0000}-{i:000000000000}"), spaceId, $"B{i}"));
}
