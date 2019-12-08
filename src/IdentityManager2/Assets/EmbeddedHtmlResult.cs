using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IdentityManager2.Api.Models;
using IdentityManager2.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManager2.Assets
{
    internal class EmbeddedHtmlResult : IActionResult
    {
        private readonly string path;
        private readonly string file;
        private readonly IdentityManagerOptions options;

        public EmbeddedHtmlResult(PathString pathBase, string file, IdentityManagerOptions options)
        {
            path = pathBase.Value;
            this.file = file;
            this.options = options;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var html = AssetManager.LoadResourceString(file,
                new
                {
                    pathBase = path,
                    model = JsonSerializer.Serialize(new PageModelParams
                    {
                        PathBase = path,
                        ShowLoginButton = options.SecurityConfiguration.ShowLoginButton,
                        TitleNavBarLinkTarget = options.TitleNavBarLinkTarget
                    })
                });

            context.HttpContext.Response.ContentType = "text/html";
            await context.HttpContext.Response.WriteAsync(html, Encoding.UTF8);
        }
    }
}
