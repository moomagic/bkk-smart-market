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

    /// <summary>When eligibility last decided this request; starts the review window for an Ineligible one.</summary>
    public DateTimeOffset? EligibilityCheckedAt { get; private set; }

    /// <summary>A Corrected review overrode the eligibility check; later checks leave this request alone.</summary>
    public bool CorrectedByReview { get; private set; }

    /// <summary>Position in the waitlist of the request's own category (1..3) while Waitlisted.</summary>
    public int? WaitlistPosition { get; private set; }

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

    public void MarkEligible(int carryOverWeight, DateTimeOffset at)
    {
        EnsureAwaitingDraw();
        if (Status != StallRequestStatus.Eligible) EligibilityCheckedAt = at; // a re-check with the same result keeps the date
        Status = StallRequestStatus.Eligible;
        IneligibleReason = null;
        CarryOverWeight = carryOverWeight;
    }

    public void MarkIneligible(string reason, DateTimeOffset at)
    {
        EnsureAwaitingDraw();
        // A re-check with the same result keeps the date, so re-running eligibility doesn't stretch the review window.
        if (Status != StallRequestStatus.Ineligible || IneligibleReason != reason) EligibilityCheckedAt = at;
        Status = StallRequestStatus.Ineligible;
        IneligibleReason = reason;
        CarryOverWeight = 1;
    }

    // A Corrected review fixes an eligibility mistake. Before approval the request joins the next draw; after approval
    // it goes to the end of its group's waitlist, leaving the winners as they are.

    public void CorrectToEligible(int carryOverWeight)
    {
        Correct(carryOverWeight);
        Status = StallRequestStatus.Eligible;
    }

    public void CorrectToWon(int carryOverWeight)
    {
        Correct(carryOverWeight);
        Status = StallRequestStatus.Won;
    }

    public void CorrectToWaitlist(int carryOverWeight, int position)
    {
        Correct(carryOverWeight);
        Status = StallRequestStatus.Waitlisted;
        WaitlistPosition = position;
    }

    private void Correct(int carryOverWeight)
    {
        if (Status != StallRequestStatus.Ineligible)
            throw new InvalidOperationException($"Only an ineligible request can be corrected; this one is {Status}.");
        IneligibleReason = null;
        CarryOverWeight = carryOverWeight;
        CorrectedByReview = true;
    }

    // Outcomes of an approved run. Only an Eligible request was drawn, so only it can receive one.

    public void MarkWon() => Decide(StallRequestStatus.Won, null);

    public void MarkWaitlisted(int position) => Decide(StallRequestStatus.Waitlisted, position);

    public void MarkNotSelected() => Decide(StallRequestStatus.NotSelected, null);

    // After approval: the winner confirms or forfeits; a forfeit promotes someone from a waitlist.

    public void MarkConfirmed() => Move(StallRequestStatus.Won, StallRequestStatus.Confirmed);

    public void MarkForfeited() => Move(StallRequestStatus.Won, StallRequestStatus.Forfeited);

    public void PromoteToWon()
    {
        Move(StallRequestStatus.Waitlisted, StallRequestStatus.Won);
        WaitlistPosition = null;
    }

    /// <summary>Someone ahead in the same waitlist was promoted.</summary>
    public void MoveUpWaitlist(int position)
    {
        if (Status != StallRequestStatus.Waitlisted)
            throw new InvalidOperationException($"Only a waitlisted request has a waitlist position; this one is {Status}.");
        WaitlistPosition = position;
    }

    private void Move(StallRequestStatus from, StallRequestStatus to)
    {
        if (Status != from)
            throw new InvalidOperationException($"Only a {from} request can become {to}; this one is {Status}.");
        Status = to;
    }

    private void Decide(StallRequestStatus outcome, int? waitlistPosition)
    {
        if (Status != StallRequestStatus.Eligible)
            throw new InvalidOperationException($"Only an eligible request receives a draw outcome; this one is {Status}.");
        Status = outcome;
        WaitlistPosition = waitlistPosition;
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
