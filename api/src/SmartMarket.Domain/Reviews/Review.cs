using SmartMarket.Domain.Common;

namespace SmartMarket.Domain.Reviews;

public enum ReviewOutcome
{
    /// <summary>The result stands.</summary>
    Upheld,

    /// <summary>The eligibility check was wrong and has been fixed (CONTEXT.md › Review).</summary>
    Corrected,
}

/// <summary>A vendor's request to re-check the result of their own stall request; the district answers once.</summary>
public sealed class Review
{
    public Guid Id { get; private set; }
    public Guid StallRequestId { get; private set; }

    /// <summary>Free text from the vendor — may mention health or family, so it is masked in the audit trail.</summary>
    [PersonalData]
    public string Reason { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }
    public ReviewOutcome? Outcome { get; private set; }

    /// <summary>The district's answer; it may repeat the vendor's details, so it is masked in the audit trail too.</summary>
    [PersonalData]
    public string? Response { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public string? RespondedBy { get; private set; }

    public Review(Guid id, Guid stallRequestId, string reason, DateTimeOffset submittedAt)
    {
        Id = id;
        StallRequestId = stallRequestId;
        Reason = reason;
        SubmittedAt = submittedAt;
    }

    public bool IsOpen => Outcome is null;

    public void Answer(ReviewOutcome outcome, string response, DateTimeOffset at, string by)
    {
        if (!IsOpen)
            throw new InvalidOperationException("This review has already been answered.");
        Outcome = outcome;
        Response = response;
        RespondedAt = at;
        RespondedBy = by;
    }
}
