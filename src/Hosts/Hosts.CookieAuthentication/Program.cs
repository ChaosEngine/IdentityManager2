using Microsoft.AspNetCore.Builder;

namespace Hosts.CookieAuthentication;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureServices();

        var app = builder.Build();
        app.Configure();

        app.Run();
    }

}
