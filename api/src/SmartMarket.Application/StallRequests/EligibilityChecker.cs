using System.Globalization;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.StallRequests;

/// <summary>Everything the eligibility rules need about one stall request, gathered by the caller.</summary>
/// <param name="PreviousRoundOutcomeHere">Status of the vendor's request in the previous round at the same space, if any.</param>
public sealed record EligibilityFacts(
    bool HasIdentity,
    bool FoodHygieneTrained,
    DateOnly? BannedUntil,
    bool SpaceSellsFood,
    DateOnly RoundStartsOn,
    bool IsTrialArea,
    int TrialAreaRequestsThisYear,
    int OtherOpenRequestsInPeriod,
    StallRequestStatus? PreviousRoundOutcomeHere);

public sealed record EligibilityLimits(int TrialAreaRequestsPerYear, int MaxOpenRequestsInPeriod, int CarryOverWeight);

/// <param name="Reason">Thai, listing every failed rule; null when eligible.</param>
public sealed record EligibilityDecision(bool Eligible, string? Reason, int Weight);

/// <summary>
/// Rule-based eligibility (CLAUDE.md › Business rules, ADR-0003) and carry-over weight. Pure: no I/O.
/// AI never decides this (Guardrail 1).
/// </summary>
public static class EligibilityChecker
{
    private static readonly CultureInfo Thai = new("th-TH"); // Buddhist calendar: 31 ม.ค. 2570

    /// <summary>Ending a round in one of these at the same space doubles the weight next round.</summary>
    private static readonly StallRequestStatus[] MissedOut = [StallRequestStatus.NotSelected, StallRequestStatus.Waitlisted];

    public static EligibilityDecision Check(EligibilityFacts facts, EligibilityLimits limits)
    {
        var failures = new List<string>();
        if (!facts.HasIdentity)
            failures.Add("ไม่มีข้อมูลยืนยันตัวตน");
        if (facts.SpaceSellsFood && !facts.FoodHygieneTrained)
            failures.Add("ยังไม่ผ่านการอบรมสุขาภิบาลอาหาร");
        if (facts.BannedUntil is { } until && until >= facts.RoundStartsOn)
            failures.Add($"อยู่ระหว่างงดสิทธิ์ถึง {until.ToString("d MMM yyyy", Thai)}");
        if (facts.IsTrialArea && facts.TrialAreaRequestsThisYear >= limits.TrialAreaRequestsPerYear)
            failures.Add($"สมัครพื้นที่ทดลองค้าได้ {limits.TrialAreaRequestsPerYear} ครั้งต่อปี และใช้สิทธิ์ปีนี้แล้ว");
        if (facts.OtherOpenRequestsInPeriod >= limits.MaxOpenRequestsInPeriod)
            failures.Add("มีคำขออื่นที่ยังไม่สิ้นสุดในช่วงรอบเดียวกัน");

        if (failures.Count > 0)
            return new EligibilityDecision(false, string.Join(" · ", failures), 1);

        return new EligibilityDecision(true, null, Weight(facts.PreviousRoundOutcomeHere, limits.CarryOverWeight));
    }

    /// <summary>Carry-over weight: ×<paramref name="carryOverWeight"/> after missing out at the same space last round, else 1.</summary>
    public static int Weight(StallRequestStatus? previousRoundOutcomeHere, int carryOverWeight) =>
        previousRoundOutcomeHere is { } previous && MissedOut.Contains(previous) ? carryOverWeight : 1;
}
