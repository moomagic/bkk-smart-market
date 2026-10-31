using SmartMarket.Domain.Common;

namespace SmartMarket.Domain.CheckIns;

public enum CheckInMethod
{
    QR,
}

/// <summary>
/// A winner present at their lock on a selling day. At most one per assignment per <see cref="Day"/>.
/// Check-in opens at confirmation, so rows can predate the round's StartsOn (demo choice) — usage ratios must
/// count only days within StartsOn..EndsOn.
/// </summary>
public sealed class CheckIn
{
    public Guid Id { get; private set; }
    public Guid AssignmentId { get; private set; }
    public DateTimeOffset At { get; private set; }

    /// <summary>Bangkok calendar day of <see cref="At"/> — the unit of "one check-in per selling day" and of usage ratios.</summary>
    public DateOnly Day { get; private set; }

    public CheckInMethod Method { get; private set; }

    /// <summary>Simulated: the phone's location check. Outside is recorded and flagged, never refused.</summary>
    public bool WithinGeofence { get; private set; }

    /// <summary>
    /// Simulated: a hash of the id the vendor's device sent, kept only to spot one device checking in for several winners
    /// (sublease). Null for check-ins recorded before devices were (and for a device that sent none).
    /// </summary>
    [PersonalData]
    public string? DeviceHash { get; private set; }

    private static readonly TimeSpan BangkokOffset = TimeSpan.FromHours(7);

    public CheckIn(Guid id, Guid assignmentId, DateTimeOffset at, CheckInMethod method, bool withinGeofence, string? deviceHash = null)
    {
        Id = id;
        AssignmentId = assignmentId;
        At = at;
        Day = DayOf(at);
        Method = method;
        WithinGeofence = withinGeofence;
        DeviceHash = deviceHash;
    }

    public static DateOnly DayOf(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(BangkokOffset).DateTime);
}
