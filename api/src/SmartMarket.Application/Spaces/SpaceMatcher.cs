using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartMarket.Application.Spaces;

public enum PreferredTime
{
    Morning,
    Evening,
}

public sealed record MatchQuery(string ProductType, string? PreferredArea, PreferredTime? PreferredTime);

/// <param name="RequestCount">Requests already in the open round — shown so the vendor sees the competition.</param>
public sealed record MatchCandidate(
    Guid SpaceId, string Name, IReadOnlyList<string> ProductTypes, string OpenHours, OpenRoundSummary? OpenRound,
    int LockCount, int RequestCount);

public sealed record ScoreParts(int Product, int FreeLock, int Area, int Hours);

/// <param name="Reasons">Plain Thai, one per part that scored or ruled the space down, in part order.</param>
public sealed record SpaceMatch(Guid SpaceId, string Name, int Score, ScoreParts Parts, IReadOnlyList<string> Reasons);

/// <summary>
/// Space Matching (CLAUDE.md › AI components): rule-based, not AI. Product type 40 · free lock (open round) 30 ·
/// chosen area 20 · opening hours 10. A space that doesn't take the vendor's goods is never recommended.
/// </summary>
public static partial class SpaceMatcher
{
    public const int TopCount = 3;

    private const string Food = "อาหาร";
    private const string Drink = "เครื่องดื่ม";
    private const string General = "ทั่วไป";

    // Product words that make goods food or drink; anything else counts as general goods. Thai has no word breaks, so
    // each word is matched as a substring: keep them specific enough not to sit inside unrelated words
    // ("นม" would hit ขนม, "ชา" would hit ชาม, "เนื้อ" would hit เนื้อผ้า).
    private static readonly string[] FoodWords =
    [
        "อาหาร", "ข้าว", "แกง", "ก๋วยเตี๋ยว", "ส้มตำ", "หมู", "ไก่", "ปลา", "เนื้อวัว", "เนื้อย่าง", "ขนม", "ทอด", "ปิ้ง",
        "ย่าง", "ลูกชิ้น", "ผัด", "ต้ม", "โจ๊ก", "ผลไม้", "มะม่วง", "ไอศกรีม", "เบเกอรี่", "น้ำพริก", "ติ่มซำ",
    ];

    private static readonly string[] DrinkWords =
    [
        "เครื่องดื่ม", "กาแฟ", "ชาเย็น", "ชาไทย", "ชานม", "ชาเขียว", "ชามะนาว", "โกโก้", "นมสด", "นมเย็น", "น้ำผลไม้", "น้ำดื่ม",
        "น้ำปั่น", "น้ำส้ม", "น้ำมะพร้าว", "น้ำอัดลม", "สมูทตี้",
    ];

    /// <summary>Words people put around an area that are not part of its name.</summary>
    private static readonly string[] AreaFillers = ["ย่าน", "แถว", "ใกล้", "ฯ"];

    private static readonly CultureInfo Thai = new("th-TH");

    public static IReadOnlyList<SpaceMatch> Rank(MatchQuery query, IEnumerable<MatchCandidate> spaces) =>
        [.. spaces
            .Select(s => Score(query, s))
            .Where(m => m.Parts.Product > 0)
            .OrderByDescending(m => m.Score).ThenBy(m => m.Name, StringComparer.Ordinal)
            .Take(TopCount)];

    public static SpaceMatch Score(MatchQuery query, MatchCandidate space)
    {
        var reasons = new List<string>();
        var product = ProductScore(query.ProductType.Trim(), space, reasons);
        var freeLock = FreeLockScore(space, reasons);
        var area = AreaScore(query.PreferredArea, space, reasons);
        var hours = HoursScore(query.PreferredTime, space, reasons);
        var parts = new ScoreParts(product, freeLock, area, hours);
        return new SpaceMatch(space.SpaceId, space.Name, product + freeLock + area + hours, parts, reasons);
    }

    private static int ProductScore(string product, MatchCandidate space, List<string> reasons)
    {
        var kinds = KindsOf(product);
        if (space.ProductTypes.FirstOrDefault(kinds.Contains) is { } kind)
        {
            var hygiene = kind is Food or Drink ? " (ต้องผ่านอบรมสุขาภิบาลอาหาร)" : "";
            reasons.Add($"รับสินค้าประเภท{kind} ตรงกับ{product}{hygiene}");
            return 40;
        }
        if (space.ProductTypes.Contains(General))
        {
            reasons.Add($"พื้นที่สินค้าทั่วไป ไม่ได้เน้น{string.Join("/", kinds)}");
            return 20;
        }
        reasons.Add($"พื้นที่นี้รับเฉพาะ{string.Join("/", space.ProductTypes)}");
        return 0;
    }

    private static int FreeLockScore(MatchCandidate space, List<string> reasons)
    {
        if (space.OpenRound is not { } round)
        {
            reasons.Add("ยังไม่มีรอบที่เปิดรับคำขอ");
            return 0;
        }
        var closes = round.ApplyClosesAt.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", Thai);
        reasons.Add($"เปิดรับคำขอถึง {closes} · ยื่นแล้ว {space.RequestCount} คำขอ ต่อ {space.LockCount} ล็อค");
        return 30;
    }

    private static int AreaScore(string? preferred, MatchCandidate space, List<string> reasons)
    {
        var area = NormalizeArea(preferred);
        if (area.Length == 0) return 0;
        if (NormalizeArea(space.Name).Contains(area, StringComparison.Ordinal))
        {
            reasons.Add($"อยู่ย่าน{preferred!.Trim()}ที่เลือก");
            return 20;
        }
        return 0;
    }

    private static int HoursScore(PreferredTime? preferred, MatchCandidate space, List<string> reasons)
    {
        if (preferred is not { } time) return 0;
        var ranges = TimeRange().Matches(space.OpenHours)
            .Select(m => (Start: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                          End: int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)))
            .ToList();
        var fits = time == PreferredTime.Morning ? ranges.Any(r => r.Start < 12) : ranges.Any(r => r.End > 17);
        var label = time == PreferredTime.Morning ? "ช่วงเช้า" : "ช่วงเย็น";
        if (!fits) return 0;
        reasons.Add($"เปิด{label} ({space.OpenHours}) ตรงกับเวลาที่สะดวก");
        return 10;
    }

    private static IReadOnlyList<string> KindsOf(string product)
    {
        List<string> kinds = [];
        if (FoodWords.Any(product.Contains)) kinds.Add(Food);
        if (DrinkWords.Any(product.Contains)) kinds.Add(Drink);
        return kinds.Count > 0 ? kinds : [General];
    }

    private static string NormalizeArea(string? text)
    {
        var s = (text ?? "").ToLowerInvariant();
        foreach (var filler in AreaFillers) s = s.Replace(filler, "", StringComparison.Ordinal);
        return string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
    }

    [GeneratedRegex(@"(\d{1,2}):(\d{2})\s*[–-]\s*(\d{1,2}):(\d{2})")]
    private static partial Regex TimeRange();
}
