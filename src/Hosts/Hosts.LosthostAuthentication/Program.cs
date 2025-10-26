using Hosts.Shared.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Hosts.LosthostAuthentication;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // In-memory IdentityManagerService (demo only)
        builder.Services
            .AddIdentityManager()
            .AddIdentityMangerService<InMemoryIdentityManagerService>()
            .AddIdentityManagerUI();

        var rand = new Random();
        builder.Services.AddSingleton(x => Users.Get(rand.Next(5000, 20000)));
        builder.Services.AddSingleton(x => Roles.Get(rand.Next(15)));

        var app = builder.Build();
        app.UseStaticFiles();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseIdentityManager();
        app.MapIdentityManagerUI("idmgr2"); // set "launchUrl": "idmgr2", in launchSettings.json

        app.Run();
    }
}
