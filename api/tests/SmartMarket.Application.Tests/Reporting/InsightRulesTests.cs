using FluentAssertions;
using SmartMarket.Application.Reporting;

namespace SmartMarket.Application.Tests.Reporting;

public sealed class InsightRulesTests
{
    private static readonly UsageThresholds Rules = new(MinCheckInRatio: 0.80m, AbsentDaysWarning: 7, AbsentDaysRevoke: 14);

    private static readonly DateOnly Start = new(2026, 8, 1); // a Saturday
    private static readonly DateOnly End = new(2026, 10, 31);
    private static readonly DateOnly Today = new(2026, 10, 25);

    private static InsightHolder Holder(string name, string lockCode, IEnumerable<DateOnly> days, string surname = "", string phone = "",
        IEnumerable<DateOnly>? outside = null) =>
        new(Guid.NewGuid(), name, surname.Length > 0 ? surname : Guid.NewGuid().ToString(), phone.Length > 0 ? phone : Guid.NewGuid().ToString(),
            lockCode, [.. days.Select(d => new InsightCheckIn(d, true)), .. (outside ?? []).Select(d => new InsightCheckIn(d, false))]);

    private static IEnumerable<DateOnly> Every(DayOfWeek[] selling, DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
            if (selling.Contains(d.DayOfWeek)) yield return d;
    }

    private static IReadOnlyList<InsightFlag> Evaluate(DayOfWeek[] selling, params InsightHolder[] holders) =>
        InsightRules.Evaluate(new InsightRound(Start, End, selling, holders), Today, Rules);

    [Theory]
    [InlineData("ทุกวัน 16:00–22:00", new[] { 0, 1, 2, 3, 4, 5, 6 })]
    [InlineData("ทุกวัน 06:00–10:00, 16:00–21:00", new[] { 0, 1, 2, 3, 4, 5, 6 })]
    [InlineData("ศ–อา 15:00–22:00", new[] { 5, 6, 0 })]
    [InlineData("ส–อา 16:00–22:00", new[] { 6, 0 })]
    [InlineData("จ–ศ 06:00–09:00", new[] { 1, 2, 3, 4, 5 })]
    [InlineData("พฤ 17:00–21:00", new[] { 4 })]
    public void Selling_days_come_from_the_spaces_opening_hours(string openHours, int[] expected)
    {
        SellingDays.Parse(openHours).Select(d => (int)d).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void A_holder_who_comes_every_selling_day_raises_no_flag()
    {
        Evaluate(SellingDays.EveryDay, Holder("ครบ ท.", "B1", Every(SellingDays.EveryDay, Start, Today.AddDays(-1)))).Should().BeEmpty();
    }

    [Fact]
    public void Seven_selling_days_absent_in_a_row_is_a_warning_fourteen_is_the_revoke_line_and_neither_acts()
    {
        var weekends = new[] { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var lastSeven = Every(weekends, Start, Today.AddDays(-1)).TakeLast(7).ToHashSet();
        var flags = Evaluate(weekends,
            Holder("เจ็ด ว.", "B1", Every(weekends, Start, Today.AddDays(-1)).Where(d => !lastSeven.Contains(d))),
            Holder("สิบสี่ ย.", "B2", Every(weekends, Start, Today.AddDays(-1)).SkipLast(14)));

        flags.Should().ContainSingle(f => f.Kind == InsightKind.Absent && f.LockCode == "B1" && f.Severity == InsightSeverity.Warning)
            .Which.Message.Should().Contain("7 วันขายติดกัน");
        flags.Should().ContainSingle(f => f.Kind == InsightKind.Absent && f.LockCode == "B2" && f.Severity == InsightSeverity.Critical)
            .Which.Message.Should().Contain("เจ้าหน้าที่").And.Contain("ยกเลิกสิทธิ์");
    }

    [Fact]
    public void Under_80_percent_of_a_months_selling_days_is_low_usage()
    {
        var september = Every(SellingDays.EveryDay, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)).ToList();
        var october = Every(SellingDays.EveryDay, new DateOnly(2026, 10, 1), Today.AddDays(-1)).ToList();
        var flags = Evaluate(SellingDays.EveryDay,
            Holder("น้อย ม.", "B1", [.. september.Take(20), .. october]),   // 20/30 in September
            Holder("พอดี ด.", "B2", [.. september.Take(24), .. october]));  // 24/30 = 80%

        flags.Should().ContainSingle(f => f.Kind == InsightKind.LowCheckIn).Which.Should().Match<InsightFlag>(f =>
            f.LockCode == "B1" && f.Message.Contains("20/30") && f.Message.Contains("ก.ย."));
    }

    [Fact]
    public void Months_before_check_ins_were_kept_and_check_ins_before_the_round_do_not_count()
    {
        // May–July have no records at all (history starts in August): judging them would flag everyone.
        var round = new InsightRound(new DateOnly(2026, 5, 1), End, SellingDays.EveryDay,
            [Holder("ครบ ท.", "B1", [new DateOnly(2026, 4, 30), .. Every(SellingDays.EveryDay, Start, Today.AddDays(-1))])]);

        InsightRules.Evaluate(round, Today, Rules).Should().BeEmpty();
    }

    [Fact]
    public void Check_ins_outside_the_geofence_are_flagged_with_their_count()
    {
        var days = Every(SellingDays.EveryDay, Start, Today.AddDays(-1)).ToList();
        var flags = Evaluate(SellingDays.EveryDay,
            Holder("นอก พ.", "B3", days.Skip(2), outside: days.Take(2)));

        flags.Should().ContainSingle(f => f.Kind == InsightKind.OutsideGeofence).Which.Message.Should().Contain("2 ครั้ง");
    }

    [Fact]
    public void Holders_sharing_a_surname_or_a_phone_are_flagged_together_without_showing_either()
    {
        var days = Every(SellingDays.EveryDay, Start, Today.AddDays(-1)).ToList();
        var flags = Evaluate(SellingDays.EveryDay,
            Holder("หนึ่ง ซ.", "B1", days, surname: "ซ้ำกัน", phone: "081-000-0001"),
            Holder("สอง ซ.", "B2", days, surname: "ซ้ำกัน", phone: "081-000-0001"),
            Holder("สาม ต.", "B3", days, surname: "ต่างกัน", phone: "081-000-0003"));

        var surname = flags.Should().ContainSingle(f => f.Kind == InsightKind.SharedSurname).Subject;
        surname.Message.Should().Contain("หนึ่ง ซ.").And.Contain("สอง ซ.").And.NotContain("ซ้ำกัน");
        flags.Should().ContainSingle(f => f.Kind == InsightKind.SharedPhone).Which.Message.Should().NotContain("081-000-0001");
    }

    [Fact]
    public void A_round_where_nobody_has_checked_in_flags_every_holder_as_absent()
    {
        var flags = Evaluate(SellingDays.EveryDay, Holder("ไม่มา ก.", "B1", []), Holder("ไม่มา ข.", "B2", []));

        flags.Where(f => f.Kind == InsightKind.Absent).Should().HaveCount(2).And.OnlyContain(f => f.Severity == InsightSeverity.Critical);
    }

    // ---- Shared device: one device checking in for several winners across the district ----

    private static readonly Guid Other = Guid.NewGuid();

    private static InsightHolder OnDevice(string device, IEnumerable<DateOnly> days) =>
        new(Guid.NewGuid(), "ถือ ส.", Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "B1",
            [.. days.Select(d => new InsightCheckIn(d, true, device))]);

    private static IEnumerable<DeviceCheckIn> OtherOn(string device, IEnumerable<DateOnly> days, Guid? vendor = null) =>
        days.Select(d => new DeviceCheckIn(vendor ?? Other, "อีก ค.", "ตลาดนัดเขตบางพลัด", "B7", d, device));

    private static IReadOnlyList<InsightFlag> Devices(InsightHolder holder, IEnumerable<DeviceCheckIn> district) =>
        InsightRules.Evaluate(new InsightRound(Start, End, SellingDays.EveryDay, [holder], [.. district]), Today, Rules)
            .Where(f => f.Kind == InsightKind.SharedDevice).ToList();

    private static IEnumerable<DateOnly> LastDays(int count, int skip = 0) =>
        Enumerable.Range(1 + skip, count).Select(i => Today.AddDays(-i));

    [Fact]
    public void A_device_used_three_days_each_for_two_winners_raises_a_warning_that_names_the_other_stall_not_the_device()
    {
        var flags = Devices(OnDevice("d1", LastDays(10)), OtherOn("d1", LastDays(3)));

        var flag = flags.Should().ContainSingle().Subject;
        flag.Severity.Should().Be(InsightSeverity.Warning);
        flag.VendorName.Should().Be("ถือ ส.");
        flag.LockCode.Should().Be("B1");
        flag.Message.Should().Contain("อีก ค.").And.Contain("ตลาดนัดเขตบางพลัด ล็อค B7").And.Contain("3 วัน")
            .And.Contain("ไม่ตัดสิทธิ์เอง").And.NotContain("d1");
    }

    [Fact]
    public void Borrowing_a_neighbours_phone_twice_raises_nothing_on_either_side()
    {
        Devices(OnDevice("d1", LastDays(20)), OtherOn("d1", LastDays(2))).Should().BeEmpty("the other used it only 2 days");
        Devices(OnDevice("d1", LastDays(2)), OtherOn("d1", LastDays(20))).Should().BeEmpty("the holder used it only 2 days");
    }

    [Fact]
    public void Days_before_the_window_do_not_count()
    {
        // 3 days each, but the other's fall 30–32 days back: outside the 30-day window (today counts as day 1)
        Devices(OnDevice("d1", LastDays(5)), OtherOn("d1", LastDays(3, skip: 29))).Should().BeEmpty();
        Devices(OnDevice("d1", LastDays(5)), OtherOn("d1", LastDays(3, skip: 26))).Should().ContainSingle();
    }

    [Fact]
    public void The_holders_own_check_ins_elsewhere_and_check_ins_without_a_device_never_count()
    {
        var holder = OnDevice("d1", LastDays(10));
        Devices(holder, OtherOn("d1", LastDays(10), vendor: holder.VendorId)).Should().BeEmpty("one vendor on one device is normal");

        var noDevice = new InsightHolder(Guid.NewGuid(), "ไม่มี อ.", "x", "y", "B1", [.. LastDays(10).Select(d => new InsightCheckIn(d, true))]);
        Devices(noDevice, OtherOn("d1", LastDays(10))).Should().BeEmpty();
    }

    [Fact]
    public void Two_devices_shared_with_the_same_winner_make_one_flag()
    {
        var holder = new InsightHolder(Guid.NewGuid(), "ถือ ส.", "x", "y", "B1",
            [.. LastDays(5).Select(d => new InsightCheckIn(d, true, "d1")), .. LastDays(4, skip: 5).Select(d => new InsightCheckIn(d, true, "d2"))]);

        Devices(holder, [.. OtherOn("d1", LastDays(5)), .. OtherOn("d2", LastDays(4))])
            .Should().ContainSingle().Which.Message.Should().Contain("5 วัน");
    }

    [Fact]
    public void The_threshold_and_window_come_from_the_rules()
    {
        var strict = Rules with { SharedDeviceMinDays = 5 };
        InsightRules.Evaluate(new InsightRound(Start, End, SellingDays.EveryDay, [OnDevice("d1", LastDays(10))], [.. OtherOn("d1", LastDays(4))]),
            Today, strict).Should().NotContain(f => f.Kind == InsightKind.SharedDevice);
    }
}
