using SmartMarket.Application.Common;

namespace SmartMarket.Api.Tests;

/// <summary>Frozen clock the tests move by hand. Starts at the demo default (25 ต.ค. 2569, inside the request window).</summary>
public sealed class TestClock : IClock
{
    public static readonly DateTimeOffset DemoNow = new(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(7));

    private DateTimeOffset _now = DemoNow;

    public DateTimeOffset Now => _now.ToUniversalTime().AddTicks(10 * Interlocked.Increment(ref _tick)); // +1µs per read keeps audit timestamps strictly ordered in Postgres;

    private long _tick;

    public void Set(DateTimeOffset now) => _now = now;
    public void Reset() => _now = DemoNow;
}
