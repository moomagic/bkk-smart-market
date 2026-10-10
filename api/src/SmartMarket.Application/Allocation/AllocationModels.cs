using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <summary>One stall request entering the draw. Weight = carry-over weight (1, or 2 after missing out last round).</summary>
public sealed record Candidate(Guid VendorId, Category Category, int Weight, bool Eligible);

/// <summary>Rule values used for a run; stored on the AllocationRun and covered by the input hash (ADR-0002).</summary>
public sealed record RulesSnapshot(decimal LegacyMax, decimal VulnerableMin, decimal NewMin, int WaitlistPerGroup);

/// <param name="LockCodes">Codes of the space's locks; N = their count.</param>
public sealed record AllocationInput(IReadOnlyList<string> LockCodes, IReadOnlyList<Candidate> Candidates, int Seed, RulesSnapshot Rules);

public enum OutcomeKind
{
    Won,
    Waitlisted,
    NotSelected,
    Forfeited,
}

/// <param name="ViaRollover">Won a seat moved here from another group's unfilled quota.</param>
/// <param name="Reason">Thai explanation shown to the vendor and officer.</param>
public sealed record Outcome(
    Guid VendorId, Category Category, OutcomeKind Kind, string? LockCode, int? WaitlistPosition, bool ViaRollover, string Reason);

/// <param name="Base">Seats the quota rule reserves for the group.</param>
/// <param name="Allocated">Seats actually filled after rollover.</param>
public sealed record QuotaLine(Category Category, int Base, int Allocated);

/// <param name="Outcomes">Ordered Legacy → Vulnerable → New, each in draw order.</param>
/// <param name="PooledOrder">N=1 only: every vendor in the single pooled draw order (decides promotion); empty otherwise.</param>
public sealed record AllocationResult(
    string InputHash,
    bool PooledDraw,
    IReadOnlyList<QuotaLine> Quota,
    IReadOnlyList<Outcome> Outcomes,
    IReadOnlyList<string> UnassignedLockCodes,
    IReadOnlyList<Guid> PooledOrder);

public sealed record PromotionResult(AllocationResult Result, Guid? PromotedVendorId);
