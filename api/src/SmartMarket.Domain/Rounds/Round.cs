using System.Linq.Expressions;

namespace SmartMarket.Domain.Rounds;

/// <summary>One period of use of a space, with its own window for stall requests.</summary>
public sealed class Round
{
    public Guid Id { get; private set; }
    public Guid SpaceId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public DateTimeOffset ApplyOpensAt { get; private set; }
    public DateTimeOffset ApplyClosesAt { get; private set; }
    public RoundStatus Status { get; private set; }

    public Round(
        Guid id, Guid spaceId, DateOnly startsOn, DateOnly endsOn,
        DateTimeOffset applyOpensAt, DateTimeOffset applyClosesAt, RoundStatus status)
    {
        Id = id;
        SpaceId = spaceId;
        StartsOn = startsOn;
        EndsOn = endsOn;
        ApplyOpensAt = applyOpensAt;
        ApplyClosesAt = applyClosesAt;
        Status = status;
    }

    // Rules as expressions so EF queries and in-memory checks share one definition.

    public static Expression<Func<Round, bool>> AcceptingRequestsAt(DateTimeOffset now) =>
        r => r.Status == RoundStatus.Open && r.ApplyOpensAt <= now && now <= r.ApplyClosesAt;

    /// <summary>Rounds whose period overlaps [startsOn, endsOn].</summary>
    public static Expression<Func<Round, bool>> Overlapping(DateOnly startsOn, DateOnly endsOn) =>
        r => r.StartsOn <= endsOn && startsOn <= r.EndsOn;

    /// <summary>Eligibility can be (re)checked until the draw.</summary>
    public bool CanCheckEligibility => Status is RoundStatus.Open or RoundStatus.Closed;

    /// <summary>A round is drawn at most once; a reject (#16) re-runs it without returning to Open.</summary>
    public bool CanBeDrawn => Status is RoundStatus.Open or RoundStatus.Closed;

    /// <summary>Drawing closes the round to new requests and freezes eligibility.</summary>
    public void MarkDrawn()
    {
        if (!CanBeDrawn)
            throw new InvalidOperationException($"A round that is already {Status} cannot be drawn.");
        Status = RoundStatus.Drawn;
    }

    public void MarkApproved()
    {
        if (Status != RoundStatus.Drawn)
            throw new InvalidOperationException($"Only a drawn round can be approved; this one is {Status}.");
        Status = RoundStatus.Approved;
    }

    public bool AcceptsRequestsAt(DateTimeOffset now) => AcceptingRequestsAt(now).Compile()(this);
}
