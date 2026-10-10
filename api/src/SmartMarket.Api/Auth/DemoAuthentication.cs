using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;

namespace SmartMarket.Api.Auth;

/// <summary>Mock auth for the prototype: trusts X-Demo-Role / X-Demo-User. No header = anonymous.</summary>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";
    public const string RoleHeader = "X-Demo-Role";
    public const string UserHeader = "X-Demo-User";

    public static readonly string[] Roles = ["vendor", "officer", "executive"];

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers[RoleHeader].ToString().Trim().ToLowerInvariant();
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        if (!Roles.Contains(role)) return Task.FromResult(AuthenticateResult.Fail($"Unknown {RoleHeader}"));

        var user = Request.Headers[UserHeader].ToString().Trim();
        if (user.Length == 0) user = $"demo-{role}";

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user), new Claim(ClaimTypes.Role, role)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Actor for audit entries: the demo user on HTTP requests, "system" elsewhere (startup, tests).</summary>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public const string System = "system";
    public const string Anonymous = "anonymous";

    public string Actor => accessor.HttpContext switch
    {
        null => System,
        { User.Identity: { IsAuthenticated: true, Name: { } name } } => name,
        _ => Anonymous,
    };

    public string Role => accessor.HttpContext switch
    {
        null => System,
        { User.Identity.IsAuthenticated: true } ctx => ctx.User.FindFirstValue(ClaimTypes.Role) ?? Anonymous,
        _ => Anonymous,
    };
}

public static class DemoPolicies
{
    public const string Vendor = "vendor";
    public const string Officer = "officer";
    public const string Executive = "executive";
}
