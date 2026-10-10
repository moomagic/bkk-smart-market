namespace SmartMarket.Application.Common;

/// <summary>Who is issuing the current command (mock auth: X-Demo-User / X-Demo-Role).</summary>
public interface ICurrentActor
{
    string Actor { get; }
    string Role { get; }
}
