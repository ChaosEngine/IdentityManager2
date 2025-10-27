using System;
using Hosts.Shared.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hosts.CookieAuthentication;

public static class Startup
{
    public static void ConfigureServices(this IServiceCollection services)
    {
        // In-memory IdentityManagerService (demo only)
        services.AddIdentityManager(options =>
            {
                options.SecurityConfiguration.HostAuthenticationType = "cookie";
                options.SecurityConfiguration.HostChallengeType = "cookie";
            })
            .AddIdentityMangerService<InMemoryIdentityManagerService>()
            .AddIdentityManagerUI();

        var rand = new Random();
        services.AddSingleton(x => Users.Get(rand.Next(5000, 20000)));
        services.AddSingleton(x => Roles.Get(rand.Next(15)));

        services
            .AddAuthentication("cookie")
            .AddCookie("cookie", options =>
            {
                options.LoginPath = "/login";
            });
    }

    public static void Configure(this WebApplication app)
    {
        app.UseDeveloperExceptionPage();

        app.UseRouting();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseIdentityManager();

        // Map attribute-routed controllers first
        app.MapControllers();

        app.MapIdentityManagerUI(string.Empty); // set "launchUrl": "", in launchSettings.json
       
    }
}
