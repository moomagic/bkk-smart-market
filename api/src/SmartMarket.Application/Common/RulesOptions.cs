namespace SmartMarket.Application.Common;

/// <summary>Business-rule defaults (appsettings section "Rules"). See CLAUDE.md › Business rules.</summary>
public sealed class RulesOptions
{
    public const string Section = "Rules";

    public int MaxOpenRequestsPerVendorInDistrict { get; init; } = 1;
    public QuotaRules Quota { get; init; } = new();
    public int CarryOverWeight { get; init; } = 2;
    public RoundRules Rounds { get; init; } = new();
    public int MaxConsecutiveRenewals { get; init; } = 2;
    public int ConfirmWithinDays { get; init; } = 3;
    public int WaitlistPerGroup { get; init; } = 3;
    public RejectRules Reject { get; init; } = new();
    public UsageRules Usage { get; init; } = new();
    public int SubletBanRounds { get; init; } = 1;
    public ReviewRules Review { get; init; } = new();
}

public sealed class QuotaRules
{
    public decimal LegacyMax { get; init; } = 0.50m;
    public decimal VulnerableMin { get; init; } = 0.25m;
    public decimal NewMin { get; init; } = 0.25m;
}

public sealed class RoundRules
{
    public int RelaxationMonths { get; init; } = 6;
    public int DistrictMarketMonths { get; init; } = 6;
    public int TrialAreaMonths { get; init; } = 1;
    public int TrialAreaRequestsPerYear { get; init; } = 1;
}

public sealed class RejectRules
{
    public int MaxPerRoundWithoutExecutive { get; init; } = 2;
}

public sealed class UsageRules
{
    public decimal MinCheckInRatio { get; init; } = 0.80m;
    public int AbsentDaysWarning { get; init; } = 7;
    public int AbsentDaysRevoke { get; init; } = 14;
}

public sealed class ReviewRules
{
    public int FileWithinDays { get; init; } = 7;
    public int RespondWithinDays { get; init; } = 7;
}
