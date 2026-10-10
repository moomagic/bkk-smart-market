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

    public bool AcceptsRequestsAt(DateTimeOffset now) => AcceptingRequestsAt(now).Compile()(this);
}
