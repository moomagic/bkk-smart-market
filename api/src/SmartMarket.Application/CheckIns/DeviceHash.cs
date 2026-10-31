using System.Security.Cryptography;
using System.Text;

namespace SmartMarket.Application.CheckIns;

/// <summary>
/// What is kept of a check-in device (PDPA: no more than needed): a SHA-256 of the id, enough to tell two check-ins
/// came from one device, never the id itself.
/// </summary>
public static class DeviceHash
{
    private const string Domain = "bkk-smart-market/check-in-device/";

    /// <returns>Null when the device sent no id.</returns>
    public static string? Of(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId)
            ? null
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Domain + deviceId.Trim())));
}
