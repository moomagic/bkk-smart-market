namespace SmartMarket.Domain.StallRequests;

/// <summary>A vendor's request to enter a round's draw. Submitting is not winning a lock (Guardrail 4).</summary>
public sealed class StallRequest
{
    /// <summary>Statuses after which the request no longer blocks the vendor from another request.</summary>
    public static readonly StallRequestStatus[] Finished =
        [StallRequestStatus.Ineligible, StallRequestStatus.NotSelected, StallRequestStatus.Forfeited];

    /// <summary>Statuses that mean the vendor held the lock (makes them Legacy at that space next round).</summary>
    public static readonly StallRequestStatus[] HeldLock = [StallRequestStatus.Won, StallRequestStatus.Confirmed];

    public Guid Id { get; private set; }
    public Guid RoundId { get; private set; }
    public Guid VendorId { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public Category Category { get; private set; }
    public StallRequestStatus Status { get; private set; }
    public string? IneligibleReason { get; private set; }
    public int CarryOverWeight { get; private set; }

    public StallRequest(
        Guid id, Guid roundId, Guid vendorId, DateTimeOffset submittedAt, Category category,
        StallRequestStatus status = StallRequestStatus.Submitted, int carryOverWeight = 1)
    {
        Id = id;
        RoundId = roundId;
        VendorId = vendorId;
        SubmittedAt = submittedAt;
        Category = category;
        Status = status;
        CarryOverWeight = carryOverWeight;
    }

    /// <summary>Statuses eligibility may (re)decide — before the draw.</summary>
    public static readonly StallRequestStatus[] AwaitingDraw =
        [StallRequestStatus.Submitted, StallRequestStatus.Eligible, StallRequestStatus.Ineligible];

    public void MarkEligible(int carryOverWeight)
    {
        EnsureAwaitingDraw();
        Status = StallRequestStatus.Eligible;
        IneligibleReason = null;
        CarryOverWeight = carryOverWeight;
    }

    public void MarkIneligible(string reason)
    {
        EnsureAwaitingDraw();
        Status = StallRequestStatus.Ineligible;
        IneligibleReason = reason;
        CarryOverWeight = 1;
    }

    private void EnsureAwaitingDraw()
    {
        if (!AwaitingDraw.Contains(Status))
            throw new InvalidOperationException($"Eligibility cannot change a request that is already {Status}.");
    }

    /// <summary>
    /// Category snapshot at submission (CONTEXT.md › Category): Vulnerable > Legacy > New.
    /// Legacy = held a lock at the same space in the previous round.
    /// </summary>
    public static Category CategoryFor(bool isVulnerable, bool heldLockHereLastRound) =>
        isVulnerable ? Category.Vulnerable
        : heldLockHereLastRound ? Category.Legacy
        : Category.New;
}
