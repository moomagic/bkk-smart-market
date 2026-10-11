using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Allocation;

/// <summary>A run as officers see it: the quota per group and the outcome with its reason for every vendor drawn.</summary>
public sealed record AllocationRunDto(
    Guid Id, Guid RoundId, string SpaceName, string ReferenceNo, int Seed, string InputHash, AllocationRunStatus Status,
    DateTimeOffset RanAt, string RanBy, DateTimeOffset? DecidedAt, string? DecidedBy, string? RejectReason,
    bool RejectCountsTowardLimit, RulesSnapshot Rules, bool PooledDraw, IReadOnlyList<QuotaLine> Quota,
    IReadOnlyList<RunOutcomeRow> Outcomes, IReadOnlyList<string> UnassignedLockCodes);

/// <summary>One vendor's outcome. Names are masked (Guardrail 5).</summary>
public sealed record RunOutcomeRow(
    Guid VendorId, string VendorName, Category Category, int Weight, OutcomeKind Kind, string? LockCode,
    int? WaitlistPosition, bool ViaRollover, string Reason);
