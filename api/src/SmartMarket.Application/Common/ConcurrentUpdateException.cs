namespace SmartMarket.Application.Common;

/// <summary>
/// A save lost a race with another command — e.g. a unique index already holds the row it tried to add. Not a business
/// error the caller could have checked for, so it is an exception; the API answers 409, never 500.
/// </summary>
public sealed class ConcurrentUpdateException(string? constraint, Exception inner)
    : Exception($"Concurrent update ({constraint ?? "unknown constraint"}).", inner)
{
    public string? Constraint { get; } = constraint;
}
