using System.Globalization;

namespace SmartMarket.Application.Reporting;

public enum InsightKind
{
    OutsideGeofence,
    Absent,
    LowCheckIn,
    SharedSurname,
    SharedPhone,
    SharedDevice,
}

public enum InsightSeverity
{
    Info,
    Warning,
    Critical,
}

/// <param name="VendorName">Masked name(s) (Guardrail 5); for a shared surname/phone, everyone in the group.</param>
public sealed record InsightFlag(InsightKind Kind, InsightSeverity Severity, string VendorName, string LockCode, string Message);

/// <param name="SharedDeviceMinDays">Days a device must have checked in for each of two winners to flag them.</param>
/// <param name="SharedDeviceWindowDays">Calendar days back from today the shared-device check looks at.</param>
public sealed record UsageThresholds(
    decimal MinCheckInRatio, int AbsentDaysWarning, int AbsentDaysRevoke, int SharedDeviceMinDays = 3, int SharedDeviceWindowDays = 30);

/// <param name="DeviceHash">Compared only, never shown (personal data); null when the check-in carried no device.</param>
public sealed record InsightCheckIn(DateOnly Day, bool WithinGeofence, string? DeviceHash = null);

/// <summary>A check-in anywhere in the district that carried a device: the other side of a shared-device flag.</summary>
/// <param name="DeviceHash">Compared only, never shown (personal data).</param>
public sealed record DeviceCheckIn(Guid VendorId, string MaskedName, string SpaceName, string LockCode, DateOnly Day, string DeviceHash);

/// <param name="Surname">Compared only, never shown (personal data).</param>
/// <param name="Phone">Compared only, never shown (personal data).</param>
public sealed record InsightHolder(
    Guid VendorId, string MaskedName, string Surname, string Phone, string LockCode, IReadOnlyList<InsightCheckIn> CheckIns);

/// <param name="Holders">Winners who hold a confirmed lock in the round.</param>
/// <param name="DistrictCheckIns">Check-ins with a device across the district (every space, every round) — what the
/// holders' devices are compared with; this round's own check-ins may be among them.</param>
public sealed record InsightRound(
    DateOnly StartsOn, DateOnly EndsOn, IReadOnlyList<DayOfWeek> SellingDays, IReadOnlyList<InsightHolder> Holders,
    IReadOnlyList<DeviceCheckIn>? DistrictCheckIns = null);

/// <summary>
/// Allocation Insight (CLAUDE.md › AI components): rule-based, read-only flags for an officer to look into. A flag never
/// changes anything — not even at the revoke line; deciding is the officer's job (Guardrail 1). Pure.
/// <list type="bullet">
/// <item>Usage is judged on <b>selling days</b> (the space's trading days) within the round, up to yesterday, from the
/// first day the round has any check-in on record — earlier days have no data, not absences — or from the round's start
/// if nobody has checked in at all. There is no record of notified leave, so a notified absence is flagged too.</item>
/// <item><b>Absent</b>: selling days in a row without a check-in, counted back from yesterday — warning at
/// <see cref="UsageThresholds.AbsentDaysWarning"/>, critical (the revoke line) at <see cref="UsageThresholds.AbsentDaysRevoke"/>.</item>
/// <item><b>LowCheckIn</b>: in any calendar month, check-ins under <see cref="UsageThresholds.MinCheckInRatio"/> of its
/// selling days (the current month once a week of it has passed).</item>
/// <item><b>OutsideGeofence</b>: any check-in recorded outside the space.</item>
/// <item><b>SharedSurname / SharedPhone</b>: two or more holders in the round with the same surname or phone.</item>
/// <item><b>SharedDevice</b>: in the last <see cref="UsageThresholds.SharedDeviceWindowDays"/> days, one device checked in
/// for a holder and for another winner anywhere in the district, on at least <see cref="UsageThresholds.SharedDeviceMinDays"/>
/// days <i>each</i> — so borrowing a neighbour's phone once or twice flags neither of them (CONTEXT.md › Shared device).</item>
/// </list>
/// </summary>
public static class InsightRules
{
    private static readonly CultureInfo Thai = new("th-TH");

    /// <summary>A month still running counts once this many of its selling days have passed.</summary>
    private const int MinDaysForRunningMonth = 7;

    public static IReadOnlyList<InsightFlag> Evaluate(InsightRound round, DateOnly today, UsageThresholds rules)
    {
        var flags = new List<InsightFlag>();
        var last = Min(today.AddDays(-1), round.EndsOn);
        bool InRound(InsightCheckIn c) => c.Day >= round.StartsOn && c.Day <= last;
        var inRound = round.Holders.SelectMany(h => h.CheckIns).Where(InRound).Select(c => c.Day).ToList();
        // No check-in from anyone yet means nobody has come, not that nothing was recorded: judge from the round's start.
        var recordsFrom = inRound.Count == 0 ? round.StartsOn : inRound.Min();
        var selling = SellingDaysBetween(round.SellingDays, recordsFrom, last);

        foreach (var h in round.Holders.OrderBy(h => h.LockCode.Length).ThenBy(h => h.LockCode, StringComparer.Ordinal))
        {
            var came = h.CheckIns.Where(InRound).Select(c => c.Day).ToHashSet();

            var outside = h.CheckIns.Count(c => !c.WithinGeofence && InRound(c));
            if (outside > 0)
                flags.Add(new(InsightKind.OutsideGeofence, InsightSeverity.Info, h.MaskedName, h.LockCode,
                    $"เช็คอินนอกพื้นที่ {outside} ครั้ง — ตรวจสอบว่ามาขายที่ล็อคจริง"));

            var streak = selling.AsEnumerable().Reverse().TakeWhile(d => !came.Contains(d)).Count();
            if (streak >= rules.AbsentDaysRevoke)
                flags.Add(new(InsightKind.Absent, InsightSeverity.Critical, h.MaskedName, h.LockCode,
                    $"ไม่เช็คอิน {streak} วันขายติดกัน — ถึงเกณฑ์ยกเลิกสิทธิ์ ({rules.AbsentDaysRevoke} วัน) ให้เจ้าหน้าที่ตรวจสอบและตัดสิน"));
            else if (streak >= rules.AbsentDaysWarning)
                flags.Add(new(InsightKind.Absent, InsightSeverity.Warning, h.MaskedName, h.LockCode,
                    $"ไม่เช็คอิน {streak} วันขายติดกัน — ถึงเกณฑ์เตือน ({rules.AbsentDaysWarning} วัน)"));

            var low = selling
                .GroupBy(d => (d.Year, d.Month))
                .Where(m => m.Count() >= MinDaysForRunningMonth || m.Key != (last.Year, last.Month))
                .Select(m => (Month: m.Key, Days: m.Count(), Came: m.Count(came.Contains)))
                .Where(m => m.Came < rules.MinCheckInRatio * m.Days)
                .Select(m => $"{MonthLabel(m.Month)} {m.Came}/{m.Days} ({Math.Round(100m * m.Came / m.Days)}%)")
                .ToList();
            if (low.Count > 0)
                flags.Add(new(InsightKind.LowCheckIn, InsightSeverity.Warning, h.MaskedName, h.LockCode,
                    $"เช็คอินต่ำกว่า {Math.Round(100m * rules.MinCheckInRatio)}% ของวันขาย: {string.Join(" · ", low)}"));
        }

        flags.AddRange(Shared(round.Holders, h => h.Surname, InsightKind.SharedSurname, "นามสกุลเดียวกัน"));
        flags.AddRange(Shared(round.Holders, h => h.Phone, InsightKind.SharedPhone, "เบอร์โทรเดียวกัน"));
        flags.AddRange(SharedDevices(round, today, rules));
        return flags;
    }

    /// <summary>One flag per holder and other winner; the device itself is personal data and never shown.</summary>
    private static IEnumerable<InsightFlag> SharedDevices(InsightRound round, DateOnly today, UsageThresholds rules)
    {
        var from = today.AddDays(1 - rules.SharedDeviceWindowDays);
        bool InWindow(DateOnly d) => d >= from && d <= today;
        var byDevice = (round.DistrictCheckIns ?? []).Where(c => InWindow(c.Day)).ToLookup(c => c.DeviceHash);

        foreach (var h in round.Holders.OrderBy(h => h.LockCode.Length).ThenBy(h => h.LockCode, StringComparer.Ordinal))
        {
            var own = h.CheckIns.Where(c => c.DeviceHash is not null && InWindow(c.Day))
                .GroupBy(c => c.DeviceHash!).ToDictionary(g => g.Key, g => g.Select(c => c.Day).Distinct().Count());
            var shared = own
                .SelectMany(d => byDevice[d.Key].Where(c => c.VendorId != h.VendorId).GroupBy(c => c.VendorId)
                    .Select(g => (Other: g.First(), Days: Math.Min(d.Value, g.Select(c => c.Day).Distinct().Count()))))
                .Where(x => x.Days >= rules.SharedDeviceMinDays)
                .GroupBy(x => x.Other.VendorId)
                .Select(g => g.MaxBy(x => x.Days))
                .OrderByDescending(x => x.Days).ThenBy(x => x.Other.MaskedName, StringComparer.Ordinal);
            foreach (var (other, days) in shared)
                yield return new InsightFlag(InsightKind.SharedDevice, InsightSeverity.Warning, h.MaskedName, h.LockCode,
                    $"อุปกรณ์เดียวกันเช็คอินให้ {other.MaskedName} ({other.SpaceName} ล็อค {other.LockCode}) ด้วย อย่างน้อย {days} วันใน {rules.SharedDeviceWindowDays} วันล่าสุด — ตรวจสอบการเช่าช่วงหน้างานก่อน ระบบไม่ตัดสิทธิ์เอง");
        }
    }

    /// <summary>The shared value itself is personal data: the flag names who, never what.</summary>
    private static IEnumerable<InsightFlag> Shared(
        IReadOnlyList<InsightHolder> holders, Func<InsightHolder, string> key, InsightKind kind, string what) =>
        holders
            .Where(h => !string.IsNullOrWhiteSpace(key(h)))
            .GroupBy(key)
            .Where(g => g.Count() > 1)
            .Select(g =>
            {
                var group = g.OrderBy(h => h.LockCode.Length).ThenBy(h => h.LockCode, StringComparer.Ordinal).ToList();
                var names = string.Join(", ", group.Select(h => h.MaskedName));
                return new InsightFlag(kind, InsightSeverity.Warning, names, string.Join(", ", group.Select(h => h.LockCode)),
                    $"ผู้ได้สิทธิ์ {group.Count} คนใช้{what}: {names} — ตรวจสอบการถือสิทธิ์แทนกันหรือเช่าช่วง");
            });

    private static List<DateOnly> SellingDaysBetween(IReadOnlyList<DayOfWeek> sellingDays, DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (sellingDays.Contains(d.DayOfWeek)) days.Add(d);
        return days;
    }

    private static string MonthLabel((int Year, int Month) m) => new DateOnly(m.Year, m.Month, 1).ToString("MMM yyyy", Thai);

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
