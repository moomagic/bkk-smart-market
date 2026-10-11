namespace SmartMarket.Domain.Allocation;

/// <summary>
/// One draw of a round (CONTEXT.md › Allocation Run). Holds everything needed to replay it:
/// the seed, the rules in force and the hash of the input. Starts as a Draft until an officer decides.
/// </summary>
public sealed class AllocationRun
{
    public Guid Id { get; private set; }
    public Guid RoundId { get; private set; }

    /// <summary>e.g. BPL-2569-1025-01 — what vendors and officers quote.</summary>
    public string ReferenceNo { get; private set; }

    public int Seed { get; private set; }
    public string InputHash { get; private set; }

    /// <summary>JSON of the rule values used (ADR-0002), so a config change cannot quietly change the result.</summary>
    public string RulesSnapshot { get; private set; }

    public DateTimeOffset RanAt { get; private set; }
    public string RanBy { get; private set; }

    /// <summary>JSON of the engine's result: quota, outcome and reason per vendor.</summary>
    public string ResultJson { get; private set; }

    public AllocationRunStatus Status { get; private set; }
    public string? RejectReason { get; private set; }
    public bool RejectCountsTowardLimit { get; private set; }

    public AllocationRun(
        Guid id, Guid roundId, string referenceNo, int seed, string inputHash, string rulesSnapshot,
        DateTimeOffset ranAt, string ranBy, string resultJson)
    {
        Id = id;
        RoundId = roundId;
        ReferenceNo = referenceNo;
        Seed = seed;
        InputHash = inputHash;
        RulesSnapshot = rulesSnapshot;
        RanAt = ranAt;
        RanBy = ranBy;
        ResultJson = resultJson;
        Status = AllocationRunStatus.Draft;
    }

    /// <summary>Statuses that stop a round from being run again: a run awaiting a decision, or one already in force.</summary>
    public static readonly AllocationRunStatus[] Standing = [AllocationRunStatus.Draft, AllocationRunStatus.Approved];
}
