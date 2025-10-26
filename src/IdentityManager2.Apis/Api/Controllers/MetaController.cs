using IdentityManager2.Api.Models;
using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IdentityManager2.Api.Controllers;

[ApiController]
[Route(IdentityManagerConstants.MetadataRoutePrefix)]
[Authorize(IdentityManagerConstants.IdMgrAuthPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MetaController : ControllerBase
{
    private readonly IIdentityManagerService userManager;
    private IdentityManagerMetadata metadata;

    #region Constructors

    public MetaController(IIdentityManagerService userManager)
    {
        this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    #endregion

    [NonAction]
    private async Task<IdentityManagerMetadata> GetMetadataAsync()
    {
        if (metadata == null)
        {
            metadata = await userManager.GetMetadataAsync();
            if (metadata == null) throw new InvalidOperationException("GetMetadataAsync returned null");
            metadata.Validate();
        }

        return metadata;
    }

    #region Endpoints

    [Route("")]
    [HttpGet]
    [EndpointName("metadata-list-get")]
    [EndpointSummary("This is a summary.")]
    [EndpointDescription("This is a description.")]
    [Tags(["metadata"])]
    // [Consumes]
    [ProducesResponseType<IdentityManagerMetadata>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<IdentityManagerMetadata>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Get()
    {
        var meta = await GetMetadataAsync();
        var data = new Dictionary<string, object> { { "currentUser", new AnonymousUserName { username = User.Identity.Name } } };

        var links = new Dictionary<string, object> { ["users"] = Url.Link("GetUsers", null) };

        if (meta.RoleMetadata.SupportsListing)
        {
            links["roles"] = Url.Link("GetRoles", null);
        }
        if (meta.UserMetadata.SupportsCreate)
        {
            links["createUser"] = new CreateUserLink(Url, meta.UserMetadata);
        }
        if (meta.RoleMetadata.SupportsCreate)
        {
            links["createRole"] = new CreateRoleLink(Url, meta.RoleMetadata);
        }

        return Ok(new MetaResult
        {
            Data = data,
            Links = links
        });
    }

    #endregion
}