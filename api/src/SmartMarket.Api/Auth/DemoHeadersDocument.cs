using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SmartMarket.Api.Auth;

/// <summary>Documents the mock auth headers so the API can be tried from /docs.</summary>
public sealed class DemoHeadersDocument : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info.Title = "BKK Smart Market API (prototype · ข้อมูลจำลอง)";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[DemoAuthenticationHandler.RoleHeader] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = DemoAuthenticationHandler.RoleHeader,
            Description = "vendor | officer | executive",
        };
        document.Components.SecuritySchemes[DemoAuthenticationHandler.UserHeader] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = DemoAuthenticationHandler.UserHeader,
            Description = "VendorId for a vendor (e.g. สมศรี ใ. = 40000000-0000-0000-0000-000000000001), any name otherwise",
        };
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(DemoAuthenticationHandler.RoleHeader, document)] = [],
            [new OpenApiSecuritySchemeReference(DemoAuthenticationHandler.UserHeader, document)] = [],
        });
        return Task.CompletedTask;
    }
}
