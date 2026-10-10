namespace SmartMarket.Application.Common;

/// <summary>The only source of "now" for business rules. Lets the demo run at a fixed point in time.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
