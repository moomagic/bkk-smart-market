using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;

namespace SmartMarket.Infrastructure.Time;

public sealed class DemoOptions
{
    public const string Section = "Demo";

    /// <summary>
    /// Demo start time (e.g. "2026-10-25T09:00:00+07:00"). When set, the clock starts here and
    /// advances in real time; when empty, the real clock is used.
    /// </summary>
    public DateTimeOffset? Now { get; set; }
}

/// <summary>Real time, shifted so the app starts at <see cref="DemoOptions.Now"/>. Always UTC.</summary>
public sealed class DemoClock : IDemoClock
{
    private readonly TimeProvider _time;
    private TimeSpan _offset;

    public DemoClock(TimeProvider time, IOptions<DemoOptions> options)
    {
        _time = time;
        _offset = options.Value.Now is { } start ? start - time.GetUtcNow() : TimeSpan.Zero;
    }

    public DateTimeOffset Now => (_time.GetUtcNow() + _offset).ToUniversalTime();

    public void Set(DateTimeOffset now) => _offset = now - _time.GetUtcNow();
}
