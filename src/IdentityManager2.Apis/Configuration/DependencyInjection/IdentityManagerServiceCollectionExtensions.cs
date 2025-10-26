using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IdentityManager2;
using IdentityManager2.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IdentityManagerServiceCollectionExtensions
    {
        [RequiresUnreferencedCode("Contains trimming unsafe calls")]
        public static IIdentityManagerBuilder AddIdentityManager(this IServiceCollection services, Action<IdentityManagerOptions> optionsAction = null)
        {
            services.Configure(optionsAction ?? (options => { }));

            var identityManagerOptions = services.BuildServiceProvider().GetRequiredService<IOptions<IdentityManagerOptions>>().Value;
            identityManagerOptions.Validate();

            services
                .AddControllersWithViews()
                .AddJsonOptions(static options =>
                {
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(ArrayPropertyValue_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(ClaimValue_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(UserQueryResultResource_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(ErrorModel_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(MetaResult_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(UserDetailResource_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(ListStringErrors_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(ModelStateDictionary_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(AnonymousCreatedUser_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(MetaResult_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(RoleQueryResultResource_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(AnonymousCreatedRole_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(RoleDetailResource_Context.Default);
                    options.JsonSerializerOptions.TypeInfoResolverChain.Add(SerializableError_Context.Default);
                });

            if (!string.IsNullOrEmpty(identityManagerOptions.SecurityConfiguration.AuthenticationScheme))
            {
                // IdentityManager API authentication scheme
                services.AddAuthentication()
                    .AddCookie(identityManagerOptions.SecurityConfiguration.AuthenticationScheme, options =>
                    {
                        options.Cookie.Name = identityManagerOptions.SecurityConfiguration.AuthenticationScheme;
                        options.Cookie.SameSite = SameSiteMode.Strict;
                        options.Cookie.HttpOnly = true;
                        options.Cookie.IsEssential = true;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                        // TODO: API Cookie: SlidingExpiration
                        // TODO: API Cookie: ExpireTimeSpan

                        options.LoginPath = identityManagerOptions.SecurityConfiguration.LoginPath;
                        options.LogoutPath = identityManagerOptions.SecurityConfiguration.LogoutPath;

                        options.Events.OnRedirectToLogin = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        };
                        options.Events.OnRedirectToAccessDenied = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        };
                    });
            }

            // IdentityManager API authorization scheme
            services.AddAuthorization(options =>
            {
                var policy = options.GetPolicy(IdentityManagerConstants.IdMgrAuthPolicy);
                if (policy != null) throw new InvalidOperationException($"Authorization policy with name {IdentityManagerConstants.IdMgrAuthPolicy} already exists");

                options.AddPolicy(IdentityManagerConstants.IdMgrAuthPolicy, config =>
                {
                    // IdentityManager role
                    config.RequireClaim(identityManagerOptions.SecurityConfiguration.RoleClaimType, identityManagerOptions.SecurityConfiguration.AdminRoleName);

                    // IdentityManager authentication scheme
                    if (!string.IsNullOrEmpty(identityManagerOptions.SecurityConfiguration.AuthenticationScheme))
                        config.AddAuthenticationSchemes(identityManagerOptions.SecurityConfiguration.AuthenticationScheme);
                });
            });

            if (!string.IsNullOrEmpty(identityManagerOptions.SecurityConfiguration.AuthenticationScheme))
                identityManagerOptions.SecurityConfiguration.Configure(services);

            return new IdentityManagerBuilder(services);
        }

        [RequiresUnreferencedCode("Contains trimming unsafe calls")]
        public static IIdentityManagerBuilder AddIdentityMangerService<T>(this IIdentityManagerBuilder builder)
            where T : class, IIdentityManagerService
        {
            builder.Services.AddTransient<IIdentityManagerService, T>();
            return builder;
        }

        public static IIdentityManagerBuilder AddIdentityManagerBuilder(this IServiceCollection services)
        {
            return new IdentityManagerBuilder(services);
        }
    }
}