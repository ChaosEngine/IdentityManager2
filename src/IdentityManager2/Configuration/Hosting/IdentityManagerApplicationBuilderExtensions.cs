using IdentityManager2.Assets;
using IdentityManager2.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Scalar.AspNetCore;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Builder;

public static class IdentityManagerApplicationBuilderExtensions
{
    internal const string DefaultRoute = IdentityManagerEndpointRouteBuilderExtensions.DefaultApiRoute;

    public static IIdentityManagerBuilder AddIdentityManagerUI(this IIdentityManagerBuilder builder)
    {
        builder.Services
            .AddControllersWithViews()
            .AddJsonOptions(static options =>
            {
                // TODO
                // options.JsonSerializerOptions.TypeInfoResolverChain.Add(PageModelParams_Context.Default);
            });

        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityManagerUI(this IEndpointRouteBuilder endpoints
        , [StringSyntax("Route")] string pattern = DefaultRoute)
    {
        var endpointGroup = endpoints.MapGroup(pattern);

        endpoints.MapIdentityManagerApis(pattern); // do not use endpointGroup here

        return endpoints;
    }

    public static IApplicationBuilder UseIdentityManager(this IApplicationBuilder app)
    {
        app.UseFileServer(new FileServerOptions
        {
            RequestPath = new PathString("/assets"),
            FileProvider = new EmbeddedFileProvider(typeof(EmbeddedHtmlResult).Assembly, "IdentityManager2.Assets")
        });

        return app;
    }
}