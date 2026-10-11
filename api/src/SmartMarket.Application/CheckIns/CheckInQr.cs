namespace SmartMarket.Application.CheckIns;

/// <summary>
/// What the (simulated) QR posted at each lock encodes. The vendor app reads it when scanning; the check-in must carry
/// the QR of the vendor's own lock.
/// </summary>
public static class CheckInQr
{
    public static string For(Guid spaceId, string lockCode) => $"bkk-smart-market:lock:{spaceId:D}:{lockCode}";
}
