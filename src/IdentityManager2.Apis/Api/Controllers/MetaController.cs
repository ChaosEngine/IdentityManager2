using IdentityManager2.Api.Models;
using IdentityManager2.Core.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IdentityManager2.Api.Controllers
{
    // TOOD: [Route("api/[area:exists]/[controller]")]
    [Route(IdentityManagerConstants.MetadataRoutePrefix)]
    public class MetaController : BaseApiController
    {
        #region Constructors

        public MetaController(IIdentityManagerService service, ILogger<MetaController> logger) : base(service, logger) { }

        #endregion

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
            logger.LogInformation("Get metadata called by user: {Username}", User.Identity?.Name);
            
            logger.LogDebug("Retrieving metadata from service");
            var meta = await GetMetadataAsync();
            
            logger.LogDebug("Building metadata response. User supports: Create={SupportsUserCreate}, Delete={SupportsUserDelete}, Claims={SupportsClaims}", 
                meta.UserMetadata.SupportsCreate, 
                meta.UserMetadata.SupportsDelete, 
                meta.UserMetadata.SupportsClaims);
            
            logger.LogDebug("Role supports: Listing={SupportsRoleListing}, Create={SupportsRoleCreate}, Delete={SupportsRoleDelete}", 
                meta.RoleMetadata.SupportsListing, 
                meta.RoleMetadata.SupportsCreate, 
                meta.RoleMetadata.SupportsDelete);
            
            var data = new Dictionary<string, object> { { "currentUser", new AnonymousUserName { username = User.Identity.Name } } };

            var links = new Dictionary<string, object> { ["users"] = Url.Link("GetUsers", null) };

            if (meta.RoleMetadata.SupportsListing)
            {
                logger.LogDebug("Adding roles link to metadata");
                links["roles"] = Url.Link("GetRoles", null);
            }
            if (meta.UserMetadata.SupportsCreate)
            {
                logger.LogDebug("Adding createUser link to metadata");
                links["createUser"] = new CreateUserLink(Url, meta.UserMetadata);
            }
            if (meta.RoleMetadata.SupportsCreate)
            {
                logger.LogDebug("Adding createRole link to metadata");
                links["createRole"] = new CreateRoleLink(Url, meta.RoleMetadata);
            }

            logger.LogInformation("Successfully retrieved metadata with {LinkCount} links", links.Count);
            return Ok(new MetaResult
            {
                Data = data,
                Links = links
            });
        }

        #endregion
    }
}
