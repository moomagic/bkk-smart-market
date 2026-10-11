namespace SmartMarket.Application.Common;

/// <summary>A clock the presenter can move (Development only — POST /api/demo/clock), e.g. past a confirm deadline.</summary>
public interface IDemoClock : IClock
{
    /// <summary>Jumps to <paramref name="now"/>; time keeps flowing from there.</summary>
    void Set(DateTimeOffset now);
}
