using Microsoft.AspNetCore.Builder;

namespace Hosts.IdentityServerAuthentication;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Startup.ConfigureServices(builder.Services);

        var app = builder.Build();
        app.Configure();

        app.Run();
    }
}
