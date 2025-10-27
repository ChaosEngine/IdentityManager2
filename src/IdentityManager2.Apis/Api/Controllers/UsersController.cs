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
    [Route(IdentityManagerConstants.UserRoutePrefix)]
    public class UsersController : BaseApiController
    {
        #region Constructors

        public UsersController(IIdentityManagerService service, ILogger<UsersController> logger) : base(service, logger) { }

        #endregion

        #region Endpoints

        [HttpGet]
        [Route("", Name = IdentityManagerConstants.RouteNames.GetUsers)]
        [EndpointName("users-get-users")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<UserQueryResultResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<ErrorModel>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetUsersAsync(string filter = null, int start = 0, int count = 100)
        {
            logger.LogInformation("GetUsersAsync called with filter: {Filter}, start: {Start}, count: {Count}", filter, start, count);
            
            logger.LogDebug("Querying users from service");
            var result = await service.QueryUsersAsync(filter, start, count);
            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully retrieved {Count} users", result.Result?.Items?.Count ?? 0);
                var meta = await GetMetadataAsync();

                var resource = new UserQueryResultResource(result.Result, Url, meta.UserMetadata);
                return Ok(resource);
            }

            logger.LogWarning("Failed to query users. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpPost("", Name = IdentityManagerConstants.RouteNames.CreateUser)]
        [EndpointName("users-create-user")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<AnonymousCreatedUser>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> CreateUserAsync([FromBody] PropertyValue[] properties)
        {
            logger.LogInformation("CreateUserAsync called with {PropertyCount} properties", properties?.Length ?? 0);
            
            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsCreate)
            {
                logger.LogWarning("User creation is not supported");
                return MethodNotAllowed();
            }

            logger.LogDebug("Validating create properties");
            var errors = ValidateCreateProperties(meta.UserMetadata, properties);

            foreach (var error in errors)
            {
                logger.LogWarning("Validation error: {Error}", error);
                ModelState.AddModelError("", error);
            }

            if (ModelState.IsValid)
            {
                logger.LogDebug("Creating user via service");
                var result = await service.CreateUserAsync(properties);
                if (result.IsSuccess)
                {
                    logger.LogInformation("Successfully created user with subject: {Subject}", result.Result.Subject);
                    var url = Url.Link(IdentityManagerConstants.RouteNames.GetUser, new AnonymousSubject { subject = result.Result.Subject });
                    var resource = new AnonymousCreatedUser
                    {
                        Data = new AnonymousSubject { subject = result.Result.Subject },
                        Links = new AnonymousDetail { detail = url }
                    };

                    return Created(url, resource);
                }

                logger.LogWarning("Failed to create user. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
                ModelState.AddModelError("errors", result.Errors.Aggregate((workingSentence, next) => workingSentence + " " + next));
                if (result.Errors.Count > 0)
                    return BadRequest(ModelState);
            }

            logger.LogWarning("ModelState is invalid when creating user");
            return BadRequest(400);
        }

        [HttpGet("{subject}", Name = IdentityManagerConstants.RouteNames.GetUser)]
        [EndpointName("users-get-user")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<UserDetailResource>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status405MethodNotAllowed, "application/problem+json")]
        public async Task<IActionResult> GetUserAsync(string subject)
        {
            logger.LogInformation("GetUserAsync called for subject: {Subject}", subject);
            
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

            logger.LogDebug("Getting user from service");
            var result = await service.GetUserAsync(subject);
            if (result.IsSuccess)
            {
                if (result.Result == null)
                {
                    logger.LogWarning("User not found for subject: {Subject}", subject);
                    return NotFound();
                }

                logger.LogInformation("Successfully retrieved user: {Username}", result.Result.Username);
                var meta = await GetMetadataAsync();
                RoleSummary[] roles = null;
                if (!IsNullOrWhiteSpace(meta.RoleMetadata.RoleClaimType))
                {
                    logger.LogDebug("Querying roles for user detail");
                    var roleResult = await service.QueryRolesAsync(null, -1, -1);
                    if (!roleResult.IsSuccess)
                    {
                        logger.LogWarning("Failed to query roles. Errors: {Errors}", string.Join(", ", roleResult.Errors ?? new List<string>()));
                        return BadRequest(roleResult.Errors);
                    }

                    roles = roleResult.Result.Items.ToArray();
                    logger.LogDebug("Retrieved {RoleCount} roles for user detail", roles.Length);
                }

                return Ok(new UserDetailResource(result.Result, Url, meta, roles));
            }

            logger.LogWarning("Failed to get user. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpDelete]
        [Route("{subject}", Name = IdentityManagerConstants.RouteNames.DeleteUser)]
        [EndpointName("users-delete-user")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> DeleteUserAsync(string subject)
        {
            logger.LogInformation("DeleteUserAsync called for subject: {Subject}", subject);
            
            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsDelete)
            {
                logger.LogWarning("User deletion is not supported");
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning("Subject is null or whitespace");
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("ModelState is invalid");
                return BadRequest(ModelState.ToError());
            }

            logger.LogDebug("Deleting user via service");
            var result = await service.DeleteUserAsync(subject);
            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully deleted user with subject: {Subject}", subject);
                return NoContent();
            }

            logger.LogWarning("Failed to delete user. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpPut]
        [Route("{subject}/properties/{type}", Name = IdentityManagerConstants.RouteNames.UpdateUserProperty)]
        [EndpointName("users-set-property")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
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
            ValidateUpdateProperty(meta.UserMetadata, type, value);

            if (ModelState.IsValid)
            {
                logger.LogDebug("Setting user property via service");
                var result = await service.SetUserPropertyAsync(subject, type, value);
                if (result.IsSuccess)
                {
                    logger.LogInformation("Successfully set property {Type} for user {Subject}", type, subject);
                    return NoContent();
                }

                logger.LogWarning("Failed to set user property. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
                ModelState.AddErrors(result);
            }

            logger.LogWarning("ModelState is invalid when setting user property");
            return BadRequest(ModelState.ToError());
        }

        #region Claims and Roles

        [HttpPost]
        [Route("{subject}/claims", Name = IdentityManagerConstants.RouteNames.AddClaim)]
        [EndpointName("users-add-claim")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> AddClaimAsync(string subject, [FromBody] ClaimValue model)
        {
            logger.LogInformation("AddClaimAsync called for subject: {Subject}, claim type: {ClaimType}", subject, model?.Type);
            
            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsClaims)
            {
                logger.LogWarning("User claims are not supported");
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning("Subject is null or whitespace");
                ModelState["subject.String"]?.Errors.Clear();
                ModelState.AddModelError("", Messages.SubjectRequired);
            }

            if (model == null)
            {
                logger.LogWarning("Claim data is null");
                ModelState.AddModelError("", Messages.ClaimDataRequired);
            }

            if (ModelState.IsValid)
            {
                logger.LogDebug("Adding claim via service: type={ClaimType}, value={ClaimValue}", model.Type, model.Value);
                // ReSharper disable once PossibleNullReferenceException
                var result = await service.AddUserClaimAsync(subject, model.Type, model.Value);
                if (result.IsSuccess)
                {
                    logger.LogInformation("Successfully added claim {ClaimType} to user {Subject}", model.Type, subject);
                    return NoContent();
                }

                logger.LogWarning("Failed to add claim. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
                ModelState.AddErrors(result);
            }

            logger.LogWarning("ModelState is invalid when adding claim");
            return BadRequest(ModelState.ToError());
        }

        [HttpDelete]
        [Route("{subject}/claims/{type}/{value}", Name = IdentityManagerConstants.RouteNames.RemoveClaim)]
        [EndpointName("users-delete-claim")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> RemoveClaimAsync(string subject, string type, string value)
        {
            logger.LogInformation("RemoveClaimAsync called for subject: {Subject}, type: {Type}", subject, type);
            
            type = type.FromBase64UrlEncoded();
            value = value.FromBase64UrlEncoded();

            logger.LogDebug("Decoded claim - type: {Type}, value: {Value}", type, value);

            var meta = await GetMetadataAsync();
            if (!meta.UserMetadata.SupportsClaims)
            {
                logger.LogWarning("User claims are not supported");
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject) ||
                IsNullOrWhiteSpace(type) ||
                IsNullOrWhiteSpace(value))
            {
                logger.LogWarning("Subject, type, or value is null or whitespace");
                return NotFound();
            }

            logger.LogDebug("Removing claim via service");
            var result = await service.RemoveUserClaimAsync(subject, type, value);
            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully removed claim {ClaimType} from user {Subject}", type, subject);
                return NoContent();
            }

            logger.LogWarning("Failed to remove claim. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpPost]
        [Route("{subject}/roles/{role}", Name = IdentityManagerConstants.RouteNames.AddRole)]
        [EndpointName("users-add-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> AddRoleAsync(string subject, string role)
        {
            logger.LogInformation("AddRoleAsync called for subject: {Subject}, role: {Role}", subject, role);
            
            var meta = await GetMetadataAsync();
            if (IsNullOrWhiteSpace(meta.RoleMetadata.RoleClaimType))
            {
                logger.LogWarning("Role claim type is not configured");
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning("Subject is null or whitespace");
                return NotFound();
            }

            role = role.FromBase64UrlEncoded();
            logger.LogDebug("Decoded role: {Role}", role);

            logger.LogDebug("Adding role via claim service");
            var result = await service.AddUserClaimAsync(subject, meta.RoleMetadata.RoleClaimType, role);
            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully added role {Role} to user {Subject}", role, subject);
                return NoContent();
            }

            logger.LogWarning("Failed to add role. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        [HttpDelete]
        [Route("{subject}/roles/{role}", Name = IdentityManagerConstants.RouteNames.RemoveRole)]
        [EndpointName("users-delete-role")]
        [EndpointSummary("TODO.")]
        [EndpointDescription("TODO.")]
        [Tags(["users"])]
        // [Consumes]
        [ProducesResponseType<string>(StatusCodes.Status204NoContent, "application/json")]
        [ProducesResponseType<string>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> RemoveRoleAsync(string subject, string role)
        {
            logger.LogInformation("RemoveRoleAsync called for subject: {Subject}, role: {Role}", subject, role);
            
            var meta = await GetMetadataAsync();
            if (IsNullOrWhiteSpace(meta.RoleMetadata.RoleClaimType))
            {
                logger.LogWarning("Role claim type is not configured");
                return MethodNotAllowed();
            }

            if (IsNullOrWhiteSpace(subject))
            {
                logger.LogWarning("Subject is null or whitespace");
                return NotFound();
            }

            role = role.FromBase64UrlEncoded();
            logger.LogDebug("Decoded role: {Role}", role);

            logger.LogDebug("Removing role via claim service");
            var result = await service.RemoveUserClaimAsync(subject, meta.RoleMetadata.RoleClaimType, role);
            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully removed role {Role} from user {Subject}", role, subject);
                return NoContent();
            }

            logger.LogWarning("Failed to remove role. Errors: {Errors}", string.Join(", ", result.Errors ?? new List<string>()));
            return BadRequest(result.ToError());
        }

        #endregion

        #endregion

        #region Helpers

        [NonAction]
        private IEnumerable<string> ValidateCreateProperties(UserMetadata userMetadata, IEnumerable<PropertyValue> properties)
        {
            if (userMetadata == null) throw new ArgumentNullException(nameof(userMetadata));
            properties = properties ?? Enumerable.Empty<PropertyValue>();

            var meta = userMetadata.GetCreateProperties();
            return meta.Validate(properties);
        }

        [NonAction]
        private void ValidateUpdateProperty(UserMetadata userMetadata, string type, string value)
        {
            if (userMetadata == null) throw new ArgumentNullException(nameof(userMetadata));

            if (IsNullOrWhiteSpace(type))
            {
                logger.LogWarning("Property type is required but was not provided");
                ModelState.AddModelError("", Messages.PropertyTypeRequired);
                return;
            }

            var prop = userMetadata.UpdateProperties.SingleOrDefault(x => x.Type == type);
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
