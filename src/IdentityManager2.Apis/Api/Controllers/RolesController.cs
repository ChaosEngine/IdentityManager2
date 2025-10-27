using IdentityManager2.Api.Models;
using IdentityManager2.Core;
using IdentityManager2.Core.Metadata;
using IdentityManager2.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.String;

namespace IdentityManager2.Api.Controllers
{
    // TOOD: [Route("api/[area:exists]/[controller]")]
    [Route(IdentityManagerConstants.RoleRoutePrefix)]
    public class RolesController : BaseApiController
    {
        #region Constructors

        public RolesController(IIdentityManagerService service, ILogger<RolesController> logger) : base(service, logger) { }

        #endregion

        #region Endpoints

        // GET api/roles
        [HttpGet]
        [Route("", Name = IdentityManagerConstants.RouteNames.GetRoles)]
        [EndpointName("roles-get-roles")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<RoleQueryResultResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetRolesAsync(string filter = null, int start = 0, int count = 100)
        {
            logger.LogInformation("GetRolesAsync called with filter: {Filter}, start: {Start}, count: {Count}", filter, start, count);
            
            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsListing)
            {
                logger.LogWarning("Role listing is not supported");
                return MethodNotAllowed();
            }

            logger.LogDebug("Querying roles from service");
            var result = await service.QueryRolesAsync(filter, start, count);
            if (result.IsSuccess)
            {
                try
                {
                    logger.LogInformation("Successfully retrieved {Count} roles", result.Result?.Items?.Count ?? 0);
                    return Ok(new RoleQueryResultResource(result.Result, Url, meta.RoleMetadata));
                }
                catch (Exception exp)
                {
                    logger.LogError(exp, "Exception occurred while creating RoleQueryResultResource");
                    throw new ArgumentNullException(exp.ToString());
                }
            }

            logger.LogWarning("Failed to query roles. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        // POST 
        [HttpPost]
        [Route("", Name = IdentityManagerConstants.RouteNames.CreateRole)]
        [EndpointName("roles-create-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<AnonymousCreatedRole>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> CreateRoleAsync([FromBody] PropertyValue[] properties)
        {
            logger.LogInformation("CreateRoleAsync called with {PropertyCount} properties", properties?.Length ?? 0);
            
            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsCreate)
            {
                logger.LogWarning("Role creation is not supported");
                return MethodNotAllowed();
            }

            logger.LogDebug("Validating create properties");
            var errors = ValidateCreateProperties(meta.RoleMetadata, properties);

            foreach (var error in errors)
            {
                logger.LogWarning("Validation error: {Error}", error);
                ModelState.AddModelError("", error);
            }

            if (ModelState.IsValid)
            {
                logger.LogDebug("Creating role via service");
                var result = await service.CreateRoleAsync(properties);
                if (result.IsSuccess)
                {
                    logger.LogInformation("Successfully created role with subject: {Subject}", result.Result.Subject);
                    var url = Url.Link(IdentityManagerConstants.RouteNames.GetRole, new AnonymousSubject { subject = result.Result.Subject });

                    var resource = new AnonymousCreatedRole
                    {
                        Data = new AnonymousSubject { subject = result.Result.Subject },
                        Links = new AnonymousDetail { detail = url }
                    };
                    return Created(url, resource);
                }

                logger.LogWarning("Failed to create role. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
                ModelState.AddModelError("", errors.ToString());
            }

            logger.LogWarning("ModelState is invalid when creating role");
            return BadRequest(ModelState.ToError());
        }

        [HttpGet("{subject}", Name = IdentityManagerConstants.RouteNames.GetRole)]
        [EndpointName("roles-get-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<RoleDetailResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetRoleAsync(string subject)
        {
            logger.LogInformation("GetRoleAsync called for subject: {Subject}", subject);
            
            if (IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning("Subject is null or whitespace");
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("ModelState is invalid");
                return BadRequest(ModelState);
            }

            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsListing)
            {
                logger.LogWarning("Role listing is not supported");
                return MethodNotAllowed();
            }

            logger.LogDebug("Getting role from service");
            var result = await service.GetRoleAsync(subject);

            if (result.IsSuccess)
            {
                if (result.Result == null)
                {
                    logger.LogWarning("Role not found for subject: {Subject}", subject);
                    return NotFound();
                }

                logger.LogInformation("Successfully retrieved role: {RoleName}", result.Result.Name);
                var response = Ok(new RoleDetailResource(result.Result, Url, meta.RoleMetadata));
                return response;
            }
            
            logger.LogWarning("Failed to get role. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpDelete]
        [Route("{subject}", Name = IdentityManagerConstants.RouteNames.DeleteRole)]
        [EndpointName("roles-delete-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> DeleteRoleAsync(string subject)
        {
            logger.LogInformation("DeleteRoleAsync called for subject: {Subject}", subject);
            
            var meta = await GetMetadataAsync();
            if (!meta.RoleMetadata.SupportsDelete)
            {
                logger.LogWarning("Role deletion is not supported");
                return MethodNotAllowed();
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("ModelState is invalid");
                return BadRequest(ModelState.ToError());
            }

            logger.LogDebug("Deleting role via service");
            var result = await service.DeleteRoleAsync(subject);
            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully deleted role with subject: {Subject}", subject);
                return NoContent();
            }

            logger.LogWarning("Failed to delete role. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpPut]
        [Route("{subject}/properties/{type}", Name = IdentityManagerConstants.RouteNames.UpdateRoleProperty)]
        [EndpointName("roles-set-property")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["roles"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> SetPropertyAsync(string subject, string type)
        {
            logger.LogInformation("SetPropertyAsync called for subject: {Subject}, type: {Type}", subject, type);
            
            if (IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning("Subject is null or whitespace");
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            type = type.FromBase64UrlEncoded();
            var value = await Request.Body.ReadAsStringAsync();

            logger.LogDebug("Decoded property type: {Type}, value length: {ValueLength}", type, value?.Length ?? 0);

            var meta = await GetMetadataAsync();

            ValidateUpdateProperty(meta.RoleMetadata, type, value);

            if (ModelState.IsValid)
            {
                logger.LogDebug("Setting role property via service");
                var result = await service.SetRolePropertyAsync(subject, type, value);

                if (result.IsSuccess)
                {
                    logger.LogInformation("Successfully set property {Type} for role {Subject}", type, subject);
                    return NoContent();
                }

                logger.LogWarning("Failed to set role property. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
                ModelState.AddErrors(result);
            }

            logger.LogWarning("ModelState is invalid when setting role property");
            return BadRequest(ModelState.ToError());
        }

        #endregion

        #region Helpers

        [NonAction]
        private IEnumerable<string> ValidateCreateProperties(RoleMetadata roleMetadata, IEnumerable<PropertyValue> properties)
        {
            if (roleMetadata == null) throw new ArgumentNullException(nameof(roleMetadata));
            properties = properties ?? Enumerable.Empty<PropertyValue>();

            var meta = roleMetadata.GetCreateProperties();
            return meta.Validate(properties);
        }

        [NonAction]
        private void ValidateUpdateProperty(RoleMetadata roleMetadata, string type, string value)
        {
            if (roleMetadata == null) throw new ArgumentNullException(nameof(roleMetadata));

            if (IsNullOrWhiteSpace(type))
            {
                logger.LogWarning("Property type is required but was not provided");
                ModelState.AddModelError("", Messages.PropertyTypeRequired);
                return;
            }

            var prop = roleMetadata.UpdateProperties.SingleOrDefault(x => x.Type == type);
            if (prop == null)
            {
                logger.LogWarning("Invalid property type: {Type}", type);
                ModelState.AddModelError("", Format(Messages.PropertyInvalid, type));
            }
            else
            {
                var error = prop.Validate(value);
                if (error != null)
                {
                    logger.LogWarning("Property validation failed for type {Type}: {Error}", type, error);
                    ModelState.AddModelError("", error);
                }
            }
        }

        [NonAction]
        private IActionResult MethodNotAllowed()
        {
            return StatusCode(405);
        }

        #endregion
    }
}
