using SmartMarket.Domain.Reviews;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Reviews;

public sealed record ReviewDto(
    Guid Id, Guid StallRequestId, string Reason, DateTimeOffset SubmittedAt, ReviewOutcome? Outcome, string? Response,
    DateTimeOffset? RespondedAt)
{
    public static ReviewDto From(Review r) => new(r.Id, r.StallRequestId, r.Reason, r.SubmittedAt, r.Outcome, r.Response, r.RespondedAt);
}

/// <summary>A review as officers see it in a round. The vendor's name is masked (Guardrail 5).</summary>
public sealed record RoundReviewRow(
    Guid Id, Guid StallRequestId, string VendorName, Category Category, StallRequestStatus RequestStatus, string? IneligibleReason,
    string Reason, DateTimeOffset SubmittedAt, DateTimeOffset RespondBy, ReviewOutcome? Outcome, string? Response,
    DateTimeOffset? RespondedAt);
