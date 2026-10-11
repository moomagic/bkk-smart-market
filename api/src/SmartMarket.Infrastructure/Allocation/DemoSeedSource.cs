using SmartMarket.Application.Allocation;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Infrastructure.Allocation;

/// <summary>
/// The demo seed from seed data for the first run of the demo round only; every other run — including a re-run of
/// that round after a reject — gets a seed from <paramref name="fallback"/> (ADR-0002).
/// </summary>
public sealed class DemoSeedSource(ISeedSource fallback) : ISeedSource
{
    public int NextSeed(Guid roundId, int priorRuns) =>
        roundId == SeedData.CharanCurrentRound && priorRuns == 0 ? SeedData.CharanDemoSeed : fallback.NextSeed(roundId, priorRuns);
}
