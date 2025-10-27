namespace IdentityManager2.Api.Models;

public class PageModel
{
    public string ApiPathBase { get; set; }
    public string PathBase { get; set; }
    public string Model { get; set; }
}

class PageModelParams
{
    public string ApiPathBase { get; set; }
    public string PathBase { get; set; }
    public bool ShowLoginButton { get; set; }
    public string TitleNavBarLinkTarget { get; set; }
    public string LoginPath { get; set; }
    public string LogoutPath { get; set; }
}