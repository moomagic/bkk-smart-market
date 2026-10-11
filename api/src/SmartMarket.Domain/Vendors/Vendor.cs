using SmartMarket.Domain.Common;

namespace SmartMarket.Domain.Vendors;

public sealed class Vendor
{
    public Guid Id { get; private set; }

    [PersonalData]
    public string DisplayName { get; private set; }

    /// <summary>Name with surname abbreviated (e.g. "สมศรี ใ."). Safe to show on every screen.</summary>
    public string MaskedName { get; private set; }

    [PersonalData]
    public string Phone { get; private set; }

    [PersonalData]
    public string? VulnerableReason { get; private set; }

    public string ProductType { get; private set; }
    public bool FoodHygieneTrained { get; private set; }
    public DateOnly? BannedUntil { get; private set; }
    public DateTimeOffset ConsentAt { get; private set; }

    public Vendor(
        Guid id, string displayName, string phone, string? vulnerableReason,
        string productType, bool foodHygieneTrained, DateOnly? bannedUntil, DateTimeOffset consentAt)
    {
        Id = id;
        DisplayName = displayName;
        MaskedName = Mask(displayName);
        Phone = phone;
        VulnerableReason = vulnerableReason;
        ProductType = productType;
        FoodHygieneTrained = foodHygieneTrained;
        BannedUntil = bannedUntil;
        ConsentAt = consentAt;
    }

    /// <summary>Enough identity on file to hold a lock (mock: name and phone present).</summary>
    public bool HasIdentity => !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(Phone);

    public void UpdateContact(string displayName, string phone)
    {
        DisplayName = displayName;
        MaskedName = Mask(displayName);
        Phone = phone;
    }

    /// <summary>"สมศรี ใจดี" → "ใจดี"; empty when there is none. Personal data — compare it, never show it.</summary>
    public static string SurnameOf(string displayName) =>
        displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is [_, var last] ? last : "";

    /// <summary>"สมศรี ใจดี" → "สมศรี ใ."</summary>
    public static string Mask(string displayName)
    {
        var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "",
            1 => parts[0],
            _ => $"{parts[0]} {char.ConvertFromUtf32(char.ConvertToUtf32(parts[1], 0))}.",
        };
    }
}
