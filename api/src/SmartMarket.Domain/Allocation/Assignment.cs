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

    public Assignment(Guid id, Guid roundId, Guid vendorId, Guid lockId, DateTimeOffset confirmBy)
    {
        Id = id;
        RoundId = roundId;
        VendorId = vendorId;
        LockId = lockId;
        ConfirmBy = confirmBy;
    }
}
