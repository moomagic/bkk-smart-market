namespace SmartMarket.Application.Common;

/// <summary>
/// Business rules, bound from appsettings section "Rules" — the single source of the values
/// (see CLAUDE.md › Business rules). Validated at startup so a missing section fails fast.
/// </summary>
public sealed class RulesOptions
{
    public const string Section = "Rules";

    public int MaxOpenRequestsPerVendorInDistrict { get; init; }
    public QuotaRules Quota { get; init; } = new();
    public int CarryOverWeight { get; init; }
    public RoundRules Rounds { get; init; } = new();
    public int MaxConsecutiveRenewals { get; init; }
    public int ConfirmWithinDays { get; init; }
    public int WaitlistPerGroup { get; init; }
    public RejectRules Reject { get; init; } = new();
    public UsageRules Usage { get; init; } = new();
    public int SubletBanRounds { get; init; }
    public ReviewRules Review { get; init; } = new();

    public bool IsComplete() =>
        MaxOpenRequestsPerVendorInDistrict > 0 && CarryOverWeight is >= 1 and <= 2 /* ×2 at most */ && MaxConsecutiveRenewals > 0
        && ConfirmWithinDays > 0 && WaitlistPerGroup > 0 && SubletBanRounds > 0
        && Quota.LegacyMax > 0 && Quota.VulnerableMin > 0 && Quota.NewMin > 0
        && Rounds.RelaxationMonths > 0 && Rounds.DistrictMarketMonths > 0 && Rounds.TrialAreaMonths > 0
        && Rounds.TrialAreaRequestsPerYear > 0 && Reject.MaxPerRoundWithoutExecutive > 0
        && Usage.MinCheckInRatio > 0 && Usage.AbsentDaysWarning > 0 && Usage.AbsentDaysRevoke > 0
        && Review.FileWithinDays > 0 && Review.RespondWithinDays > 0;
}

public sealed class QuotaRules
{
    public decimal LegacyMax { get; init; }
    public decimal VulnerableMin { get; init; }
    public decimal NewMin { get; init; }
}

public sealed class RoundRules
{
    public int RelaxationMonths { get; init; }
    public int DistrictMarketMonths { get; init; }
    public int TrialAreaMonths { get; init; }
    public int TrialAreaRequestsPerYear { get; init; }
}

public sealed class RejectRules
{
    public int MaxPerRoundWithoutExecutive { get; init; }
}

public sealed class UsageRules
{
    public decimal MinCheckInRatio { get; init; }
    public int AbsentDaysWarning { get; init; }
    public int AbsentDaysRevoke { get; init; }
}

public sealed class ReviewRules
{
    public int FileWithinDays { get; init; }
    public int RespondWithinDays { get; init; }
}
