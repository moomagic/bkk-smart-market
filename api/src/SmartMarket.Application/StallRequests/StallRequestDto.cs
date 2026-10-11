using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.StallRequests;

/// <summary>
/// A vendor's own request. The outcome fields (<paramref name="LockCode"/>, <paramref name="ResultReason"/>,
/// <paramref name="ReferenceNo"/>, <paramref name="ConfirmBy"/>) come from the approved run only — a draft never leaks.
/// </summary>
/// <param name="Runs">Every run of the round, newest first, so the vendor can see none was hidden (ADR-0002).</param>
public sealed record StallRequestDto(
    Guid Id, Guid RoundId, Guid SpaceId, string SpaceName, DateOnly RoundStartsOn, DateOnly RoundEndsOn,
    DateTimeOffset SubmittedAt, Category Category, StallRequestStatus Status, string? IneligibleReason,
    int? WaitlistPosition, string? LockCode, string? ResultReason, string? ReferenceNo, DateTimeOffset? ConfirmBy,
    IReadOnlyList<RunReference> Runs);

public sealed record RunReference(string ReferenceNo, AllocationRunStatus Status, DateTimeOffset RanAt, string? RejectReason);
