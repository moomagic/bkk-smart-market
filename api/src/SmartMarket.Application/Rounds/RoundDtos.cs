using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Rounds;

public sealed record RoundSummary(
    Guid Id, Guid SpaceId, string SpaceName, DateOnly StartsOn, DateOnly EndsOn, DateTimeOffset ApplyClosesAt,
    RoundStatus Status, int RequestCount, int EligibleCount, int IneligibleCount, int RejectsLeft);

/// <summary>One stall request as an officer sees it. Names are masked (Guardrail 5).</summary>
public sealed record RoundRequestRow(
    Guid RequestId, Guid VendorId, string VendorName, Category Category, StallRequestStatus Status,
    string? IneligibleReason, int CarryOverWeight);
