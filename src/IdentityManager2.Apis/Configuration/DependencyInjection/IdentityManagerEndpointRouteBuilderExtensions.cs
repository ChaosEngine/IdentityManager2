using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("IdentityManager2")]

namespace Microsoft.AspNetCore.Builder;

public static class IdentityManagerEndpointRouteBuilderExtensions
{
    internal const string DefaultApiRoute = "/idmgr2";

    /// <summary>
    /// .
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
    /// <param name="pattern">The route to register the endpoint on. Must include the route parameter.</param>
    /// <returns>An <see cref="IEndpointRouteBuilder"/> that can be used to further customize the endpoint.</returns>
    public static IEndpointRouteBuilder MapIdentityManagerApis(this IEndpointRouteBuilder endpoints
        , [StringSyntax("Route")] string pattern = DefaultApiRoute)
    {
        var endpointGroup = endpoints.MapGroup(pattern);

        endpointGroup.MapOpenApi().CacheOutput();
        endpointGroup.MapScalarApiReference();

        return endpoints;
    }
}