namespace SmartMarket.Application.Common;

public enum ErrorKind
{
    NotFound,
    Conflict,
    Forbidden,
    Invalid,
}

/// <summary>A business-rule failure. <see cref="Message"/> is Thai and safe to show to the user.</summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);
    public static Error Invalid(string code, string message) => new(ErrorKind.Invalid, code, message);
}

/// <summary>Business errors are values, not exceptions (CLAUDE.md › Conventions).</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result failed: {Error!.Code}");

    public static implicit operator Result<T>(T value) => new(value, null);
    public static implicit operator Result<T>(Error error) => new(default, error);
}
