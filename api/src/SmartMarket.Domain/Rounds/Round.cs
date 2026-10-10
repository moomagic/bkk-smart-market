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

    public bool AcceptsRequestsAt(DateTimeOffset now) =>
        Status == RoundStatus.Open && ApplyOpensAt <= now && now <= ApplyClosesAt;

    public bool Overlaps(Round other) => StartsOn <= other.EndsOn && other.StartsOn <= EndsOn;
}
