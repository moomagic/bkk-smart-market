using System.Security.Cryptography;
using SmartMarket.Application.Allocation;

namespace SmartMarket.Infrastructure.Allocation;

/// <summary>Seeds from the OS crypto RNG, so nobody — officer or developer — can pick a run's outcome (ADR-0002).</summary>
public sealed class CryptoSeedSource : ISeedSource
{
    public int NextSeed(Guid roundId, int priorRuns) => RandomNumberGenerator.GetInt32(int.MaxValue);
}
