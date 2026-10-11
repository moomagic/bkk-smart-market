using System.Linq.Expressions;

namespace SmartMarket.Domain.Allocation;

/// <summary>A winner's lock for a round, created when the run is approved. The winner must confirm by <see cref="ConfirmBy"/>.</summary>
public sealed class Assignment
{
    public Guid Id { get; private set; }
    public Guid RoundId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid LockId { get; private set; }
    public DateTimeOffset ConfirmBy { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Set when the winner gave the lock up or let the deadline pass; the row stays as history.</summary>
    public DateTimeOffset? ForfeitedAt { get; private set; }

    public Assignment(Guid id, Guid roundId, Guid vendorId, Guid lockId, DateTimeOffset confirmBy)
    {
        Id = id;
        RoundId = roundId;
        VendorId = vendorId;
        LockId = lockId;
        ConfirmBy = confirmBy;
    }

    // Rules as expressions so EF queries and in-memory checks share one definition (as Round does).

    public static readonly Expression<Func<Assignment, bool>> Active = a => a.ForfeitedAt == null;

    /// <summary>Unconfirmed past the deadline: it is forfeited the next time anyone reads the round (no background job).</summary>
    public static Expression<Func<Assignment, bool>> ExpiredAt(DateTimeOffset now) =>
        a => a.ForfeitedAt == null && a.ConfirmedAt == null && a.ConfirmBy < now;

    public bool IsActive => Active.Compile()(this);

    public bool IsExpiredAt(DateTimeOffset now) => ExpiredAt(now).Compile()(this);

    public void Confirm(DateTimeOffset at)
    {
        if (!IsActive || ConfirmedAt is not null || IsExpiredAt(at))
            throw new InvalidOperationException("Only an active, unconfirmed assignment within its deadline can be confirmed.");
        ConfirmedAt = at;
    }

    public void Forfeit(DateTimeOffset at)
    {
        if (!IsActive || ConfirmedAt is not null)
            throw new InvalidOperationException("Only an active, unconfirmed assignment can be forfeited.");
        ForfeitedAt = at;
    }
}
