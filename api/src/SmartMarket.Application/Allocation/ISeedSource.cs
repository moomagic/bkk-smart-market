namespace SmartMarket.Application.Allocation;

/// <summary>
/// Where a run's seed comes from. Never the officer (ADR-0002): production draws from a crypto RNG;
/// the demo seed is the only exception and is injected through seed data.
/// </summary>
public interface ISeedSource
{
    /// <param name="priorRuns">How many runs the round already has (rejected ones included).</param>
    int NextSeed(Guid roundId, int priorRuns);
}
